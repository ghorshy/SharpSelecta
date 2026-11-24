using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using SharpSelecta.Core.Audio;

namespace SharpSelecta.Audio;

// IMMNotificationClient callbacks must return quickly, so their work is handed to the audio thread.
[SupportedOSPlatform("windows")]
[GeneratedComClass]
internal sealed partial class WindowsEndpointWatcher : IMMNotificationClient, IDisposable
{
    private const uint DeviceStateActive = 0x1;
    private const int RenderFlow = 0;
    private const int ConsoleRole = 0;
    private const int StorageRead = 0;
    private const ushort WideStringVariant = 31;
    private const uint InProcessServer = 0x1;

    private static readonly Guid MMDeviceEnumeratorClassId = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid MMDeviceEnumeratorInterfaceId = new("A95664D2-9614-4F35-A746-DE8DB63617E6");

    private static readonly PropertyKey FriendlyNameKey = new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);

    private readonly IMMDeviceEnumerator _enumerator = CreateEnumerator();
    private readonly DeviceLossTracker<string> _tracker = new();
    private readonly Lock _gate = new();
    private readonly Action _onLost;
    private readonly ILogger _logger;
    private string? _deviceName;
    private bool _disposed;

    public WindowsEndpointWatcher(Action onLost, ILogger logger)
    {
        _onLost = onLost;
        _logger = logger;
        _enumerator.RegisterEndpointNotificationCallback(this);
    }

    public void Follow(string? deviceName) => AudioThread.Invoke(() =>
    {
        lock (_gate)
        {
            _deviceName = deviceName;
            SwitchToRequestedEndpoint();
        }
    });

    public void Dispose() => AudioThread.Invoke(() =>
    {
        lock (_gate)
        {
            if (!_disposed)
            {
                _disposed = true;
                _enumerator.UnregisterEndpointNotificationCallback(this);
            }
        }
    });

    void IMMNotificationClient.OnDeviceStateChanged(string deviceId, uint newState)
    {
        if ((newState & DeviceStateActive) == 0)
        {
            Run(() => ReportIf(_tracker.DeviceRemoved(deviceId)));
        }
        else
        {
            Run(SwitchToRequestedEndpoint);
        }
    }

    void IMMNotificationClient.OnDeviceRemoved(string deviceId) => Run(() => ReportIf(_tracker.DeviceRemoved(deviceId)));

    void IMMNotificationClient.OnDefaultDeviceChanged(int flow, int role, string? defaultDeviceId)
    {
        if (flow == RenderFlow && role == ConsoleRole && defaultDeviceId is not null)
        {
            Run(() =>
            {
                if (_deviceName is null)
                {
                    ReportIf(_tracker.StreamMovedTo(defaultDeviceId, IsActive));
                }
            });
        }
    }

    void IMMNotificationClient.OnDeviceAdded(string deviceId)
    {
    }

    void IMMNotificationClient.OnPropertyValueChanged(string deviceId, PropertyKey key)
    {
    }

    private void SwitchToRequestedEndpoint()
    {
        var endpointId = _deviceName is null ? DefaultEndpointId() : EndpointIdFor(_deviceName);
        if (endpointId is null)
        {
            _logger.LogWarning("No active Windows audio endpoint named {DeviceName} - not watching it for removal", _deviceName ?? "default");
            return;
        }

        _tracker.StreamSwitchedTo(endpointId);
    }

    private static IMMDeviceEnumerator CreateEnumerator()
    {
        Marshal.ThrowExceptionForHR(CoCreateInstance(MMDeviceEnumeratorClassId, 0, InProcessServer, MMDeviceEnumeratorInterfaceId, out var instance));
        try
        {
            return (IMMDeviceEnumerator)new StrategyBasedComWrappers().GetOrCreateObjectForComInstance(instance, CreateObjectFlags.None);
        }
        finally
        {
            Marshal.Release(instance);
        }
    }

    private void Run(Action work) => AudioThread.InvokeAsync(() =>
    {
        try
        {
            lock (_gate)
            {
                if (!_disposed)
                {
                    work();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Watching Windows audio endpoints failed");
        }
    });

    private void ReportIf(bool lost)
    {
        if (lost)
        {
            _onLost();
        }
    }

    private string? DefaultEndpointId()
    {
        try
        {
            _enumerator.GetDefaultAudioEndpoint(RenderFlow, ConsoleRole, out var device);
            device.GetId(out var id);
            return id;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private string? EndpointIdFor(string friendlyName)
    {
        _enumerator.EnumAudioEndpoints(RenderFlow, DeviceStateActive, out var devices);
        devices.GetCount(out var count);
        for (uint i = 0; i < count; i++)
        {
            devices.Item(i, out var device);
            if (FriendlyName(device) == friendlyName)
            {
                device.GetId(out var id);
                return id;
            }
        }

        return null;
    }

    private bool IsActive(string endpointId)
    {
        try
        {
            _enumerator.GetDevice(endpointId, out var device);
            device.GetState(out var state);
            return (state & DeviceStateActive) != 0;
        }
        catch (COMException)
        {
            return false;
        }
    }

    private static string? FriendlyName(IMMDevice device)
    {
        device.OpenPropertyStore(StorageRead, out var properties);
        properties.GetValue(FriendlyNameKey, out var value);
        try
        {
            return value.ValueType == WideStringVariant ? Marshal.PtrToStringUni(value.Pointer) : null;
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }

    [LibraryImport("ole32.dll")]
    private static partial int PropVariantClear(ref PropVariant value);

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(in Guid classId, nint outer, uint context, in Guid interfaceId, out nint instance);
}

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct PropertyKey(Guid FormatId, int PropertyId);

[StructLayout(LayoutKind.Explicit, Size = 24)]
internal struct PropVariant
{
    [FieldOffset(0)] public ushort ValueType;
    [FieldOffset(8)] public IntPtr Pointer;
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16), Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
internal partial interface IMMDeviceEnumerator
{
    void EnumAudioEndpoints(int dataFlow, uint stateMask, out IMMDeviceCollection devices);
    void GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    void GetDevice(string id, out IMMDevice device);
    void RegisterEndpointNotificationCallback(IMMNotificationClient client);
    void UnregisterEndpointNotificationCallback(IMMNotificationClient client);
}

[GeneratedComInterface, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
internal partial interface IMMDeviceCollection
{
    void GetCount(out uint count);
    void Item(uint index, out IMMDevice device);
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16), Guid("D666063F-1587-4E43-81F1-B948E807363F")]
internal partial interface IMMDevice
{
    void Activate(in Guid interfaceId, int classContext, nint activationParams, out nint instance);
    void OpenPropertyStore(int storageAccess, out IPropertyStore properties);
    void GetId(out string id);
    void GetState(out uint state);
}

[GeneratedComInterface, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
internal partial interface IPropertyStore
{
    void GetCount(out uint count);
    void GetAt(uint index, out PropertyKey key);
    void GetValue(in PropertyKey key, out PropVariant value);
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16), Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0")]
internal partial interface IMMNotificationClient
{
    void OnDeviceStateChanged(string deviceId, uint newState);
    void OnDeviceAdded(string deviceId);
    void OnDeviceRemoved(string deviceId);
    void OnDefaultDeviceChanged(int flow, int role, string? defaultDeviceId);
    void OnPropertyValueChanged(string deviceId, PropertyKey key);
}
