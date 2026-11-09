using Microsoft.Extensions.Logging;
using SharpSelecta.Core.Audio;

namespace SharpSelecta.Audio;

public sealed class EngineOutputDeviceService(IAudioEngine audioEngine, ILogger<EngineOutputDeviceService> logger) : IOutputDeviceService, IDisposable
{
    private readonly Lock _gate = new();
    private EventHandler? _activeDeviceLost;
    private Task<IDisposable?>? _watcher;
    private string? _deviceName;

    public event EventHandler? ActiveDeviceLost
    {
        add
        {
            lock (_gate)
            {
                _activeDeviceLost += value;
                if (_watcher is null && OperatingSystem.IsWindows())
                {
                    _watcher = Task.Run(StartWindowsWatcher);
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

    public Task<IReadOnlyList<AudioOutputDevice>> GetOutputDevicesAsync() =>
        Task.Run(audioEngine.GetOutputDevices);

    public Task SetOutputDeviceAsync(string? deviceName) =>
        Task.Run(() =>
        {
            audioEngine.SetOutputDevice(deviceName);
            Task<IDisposable?>? watcher;
            lock (_gate)
            {
                _deviceName = deviceName;
                watcher = _watcher;
            }

            if (OperatingSystem.IsWindows() && watcher?.Result is WindowsEndpointWatcher windowsWatcher)
            {
                windowsWatcher.Follow(deviceName);
            }
        });

    public void Dispose()
    {
        Task<IDisposable?>? watcher;
        lock (_gate)
        {
            watcher = _watcher;
        }

        if (watcher is { IsCompletedSuccessfully: true, Result: { } started })
        {
            started.Dispose();
        }
    }

    // On a pool thread, so the COM objects live in the multithreaded apartment rather than on the UI thread.
    private IDisposable? StartWindowsWatcher()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            var watcher = new WindowsEndpointWatcher(OnActiveDeviceLost, logger);
            string? deviceName;
            lock (_gate)
            {
                deviceName = _deviceName;
            }

            watcher.Follow(deviceName);
            return watcher;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not start watching Windows audio endpoints");
            return null;
        }
    }

    private void OnActiveDeviceLost()
    {
        logger.LogInformation("The output device in use disappeared");
        _activeDeviceLost?.Invoke(this, EventArgs.Empty);
    }
}
