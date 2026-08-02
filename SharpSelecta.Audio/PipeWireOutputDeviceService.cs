using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SharpSelecta.Core.Audio;

namespace SharpSelecta.Audio;

public sealed class PipeWireOutputDeviceService(ILogger<PipeWireOutputDeviceService> logger) : IOutputDeviceService
{
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

        var processName = Process.GetCurrentProcess().ProcessName;
        var ownSinkInputs = PactlJson.FindSinkInputIndexes(sinkInputsJson, processName);
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
