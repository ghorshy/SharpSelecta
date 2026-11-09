using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SharpSelecta.Core.Audio;

namespace SharpSelecta.Audio;

public sealed class PipeWireOutputDeviceService(ILogger<PipeWireOutputDeviceService> logger) : IOutputDeviceService, IDisposable
{
    private static readonly TimeSpan SubscribeRestartDelay = TimeSpan.FromSeconds(5);
    private static readonly string OwnProcessName = CurrentProcessName();

    private readonly CancellationTokenSource _monitoring = new();
    private readonly Lock _gate = new();
    private readonly DeviceLossTracker<long> _tracker = new();
    private EventHandler? _activeDeviceLost;
    private bool _monitorStarted;

    public event EventHandler? ActiveDeviceLost
    {
        add
        {
            lock (_gate)
            {
                _activeDeviceLost += value;
                if (!_monitorStarted)
                {
                    _monitorStarted = true;
                    _ = Task.Run(() => MonitorDeviceLossAsync(_monitoring.Token));
                }
            }
        }
        remove
        {
            lock (_gate)
            {
                _activeDeviceLost -= value;
            }
        }
    }

    public async Task SetOutputDeviceAsync(string? deviceName)
    {
        string? targetSinkName;
        if (deviceName is null)
        {
            targetSinkName = "@DEFAULT_SINK@";
        }
        else
        {
            var sinksJson = await RunPactlAsync("-f", "json", "list", "sinks");
            targetSinkName = sinksJson is null
                ? null
                : PactlJson.ParseSinks(sinksJson).FirstOrDefault(s => s.Description == deviceName).Name;
            if (targetSinkName is null)
            {
                logger.LogWarning("Output sink named {DeviceName} is no longer present - not moving the stream", deviceName);
                return;
            }
        }

        var sinkInputsJson = await RunPactlAsync("-f", "json", "list", "sink-inputs");
        if (sinkInputsJson is null)
        {
            return;
        }

        var ownSinkInputs = PactlJson.FindSinkInputIndexes(sinkInputsJson, OwnProcessName);
        if (ownSinkInputs.Count == 0)
        {
            logger.LogWarning("No live output stream found for this process - nothing to move to {DeviceName}", deviceName ?? "the default sink");
            return;
        }

        foreach (var sinkInputIndex in ownSinkInputs)
        {
            if (await RunPactlAsync("move-sink-input", sinkInputIndex.ToString(), targetSinkName) is not null)
            {
                logger.LogInformation("Moved output stream {SinkInput} to sink {DeviceName}", sinkInputIndex, deviceName ?? "system default");
            }
        }

        if (await RunPactlAsync("-f", "json", "list", "sink-inputs") is { } movedJson &&
            PactlJson.FindOwnStreamSink(movedJson, OwnProcessName) is { } sink)
        {
            lock (_gate)
            {
                _tracker.StreamSwitchedTo(sink);
            }
        }
    }

    public async Task<IReadOnlyList<AudioOutputDevice>> GetOutputDevicesAsync()
    {
        var sinksJson = await RunPactlAsync("-f", "json", "list", "sinks");
        if (sinksJson is null)
        {
            return [];
        }

        var defaultSinkName = (await RunPactlAsync("get-default-sink"))?.Trim();
        return PactlJson.ParseSinks(sinksJson)
            .Select(s => new AudioOutputDevice(s.Description, s.Name == defaultSinkName))
            .ToList();
    }

    public void Dispose()
    {
        _monitoring.Cancel();
        _monitoring.Dispose();
    }

    private async Task MonitorDeviceLossAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await WatchSubscriptionAsync(cancellationToken);
                logger.LogWarning("pactl subscribe exited - restarting it to keep watching the output device");
                await Task.Delay(SubscribeRestartDelay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Watching for the output device disappearing failed");
                return;
            }
        }
    }

    private async Task WatchSubscriptionAsync(CancellationToken cancellationToken)
    {
        using var subscription = Process.Start(new ProcessStartInfo("pactl", "subscribe") { RedirectStandardOutput = true });
        if (subscription is null)
        {
            return;
        }

        await using var stopOnCancel = cancellationToken.Register(() => subscription.Kill());
        var ownSinkInputs = await ObserveOwnStreamAsync();

        while (await subscription.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
        {
            switch (PactlEvent.Parse(line))
            {
                case { Kind: "remove", Facility: "sink" } removed:
                    bool lost;
                    lock (_gate)
                    {
                        lost = _tracker.DeviceRemoved(removed.Index);
                    }

                    if (lost)
                    {
                        OnActiveDeviceLost();
                    }

                    break;
                case { Kind: "new", Facility: "sink-input" }:
                case { Kind: "change", Facility: "sink-input" } changed when ownSinkInputs.Contains(changed.Index):
                    ownSinkInputs = await ObserveOwnStreamAsync();
                    break;
            }
        }
    }

    private async Task<IReadOnlyList<long>> ObserveOwnStreamAsync()
    {
        var sinkInputsJson = await RunPactlAsync("-f", "json", "list", "sink-inputs");
        if (sinkInputsJson is null)
        {
            return [];
        }

        if (PactlJson.FindOwnStreamSink(sinkInputsJson, OwnProcessName) is { } sink &&
            await RunPactlAsync("-f", "json", "list", "sinks") is { } sinksJson)
        {
            var sinks = PactlJson.ParseSinkIndexes(sinksJson);
            bool lost;
            lock (_gate)
            {
                lost = _tracker.StreamMovedTo(sink, sinks.Contains);
            }

            if (lost)
            {
                OnActiveDeviceLost();
            }
        }

        return PactlJson.FindSinkInputIndexes(sinkInputsJson, OwnProcessName);
    }

    private static string CurrentProcessName()
    {
        using var process = Process.GetCurrentProcess();
        return process.ProcessName;
    }

    private void OnActiveDeviceLost()
    {
        logger.LogInformation("The output device in use disappeared");
        _activeDeviceLost?.Invoke(this, EventArgs.Empty);
    }

    public static bool IsAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("pactl", "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            return process is not null && process.WaitForExit(2000) && process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<string?> RunPactlAsync(params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo("pactl")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                logger.LogWarning("pactl {Arguments} failed ({ExitCode}): {Error}", string.Join(' ', arguments), process.ExitCode, error.Trim());
                return null;
            }

            return output;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run pactl {Arguments}", string.Join(' ', arguments));
            return null;
        }
    }
}
