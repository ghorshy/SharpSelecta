using System.Net.Sockets;
using System.Text.Json;
using SharpSelecta.Core.Presence;
using static SharpSelecta.Integrations.Discord.DiscordIpc;

namespace SharpSelecta.Integrations.Discord;

// Talks to the running Discord client over its local IPC socket/pipe; if Discord isn't running (yet)
// it keeps trying in the background, and a connection that comes up later gets the current status.
public sealed class DiscordPresenceService : IRichPresence, IDisposable
{
    // Discord rejects a text field that is shorter than 2 or longer than 128 characters.
    private const int MaxTextLength = 128;

    // The key of the logo uploaded under "Rich Presence > Art Assets" for this application.
    private const string LogoAssetKey = "logo";

    private const int ListeningType = 2;

    private readonly string _applicationId;
    private readonly Func<CancellationToken, Task<Stream?>> _connect;
    private readonly TimeSpan _retryDelay;
    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private Task? _loop;
    private Stream? _ready;
    private Activity? _current;
    private long _nonce;
    private bool _disposed;

    public DiscordPresenceService(string applicationId)
        : this(applicationId, DiscordIpc.ConnectAsync, TimeSpan.FromSeconds(10))
    {
    }

    public DiscordPresenceService(string applicationId, Func<CancellationToken, Task<Stream?>> connect, TimeSpan retryDelay)
    {
        _applicationId = applicationId;
        _connect = connect;
        _retryDelay = retryDelay;
    }

    public void Show(PresenceActivity activity) => Set(new Activity(
        ListeningType,
        Text(activity.Title),
        Text(activity.Artists),
        new ActivityAssets(LogoAssetKey, Text(activity.Album)),
        activity.StartedAtUtc is { } started
            ? new ActivityTimestamps(started.ToUnixTimeMilliseconds(), activity.EndsAtUtc?.ToUnixTimeMilliseconds())
            : null));

    public void Clear() => Set(null);

    private void Set(Activity? activity)
    {
        Stream? ready;
        lock (_lock)
        {
            if (_disposed)
                return;

            _current = activity;
            ready = _ready;
            _loop ??= Task.Run(() => RunAsync(_stop.Token));
        }

        if (ready is not null)
            _ = SendAsync(ready, activity);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (await _connect(cancellationToken).ConfigureAwait(false) is { } stream)
                {
                    await using (stream)
                    {
                        await SessionAsync(stream, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException or JsonException)
            {
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                lock (_lock)
                {
                    _ready = null;
                }
            }

            try
            {
                await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    // Handshake, wait for READY, send what's current, then answer pings until Discord goes away.
    private async Task SessionAsync(Stream stream, CancellationToken cancellationToken)
    {
        await DiscordIpc.WriteAsync(stream, Opcode.Handshake, Handshake(_applicationId), cancellationToken).ConfigureAwait(false);
        if (await DiscordIpc.ReadAsync(stream, cancellationToken).ConfigureAwait(false) is not { Opcode: Opcode.Frame } ready || EventOf(ready.Payload) != "READY")
            return;

        Activity? current;
        lock (_lock)
        {
            _ready = stream;
            current = _current;
        }

        if (current is not null)
            await SendAsync(stream, current).ConfigureAwait(false);

        while (await DiscordIpc.ReadAsync(stream, cancellationToken).ConfigureAwait(false) is { } frame)
        {
            if (frame.Opcode == Opcode.Close)
                return;

            if (frame.Opcode == Opcode.Ping)
                await WriteGuardedAsync(stream, Opcode.Pong, frame.Payload).ConfigureAwait(false);
        }
    }

    private Task SendAsync(Stream stream, Activity? activity) =>
        WriteGuardedAsync(stream, Opcode.Frame, SetActivity(activity, Interlocked.Increment(ref _nonce).ToString()));

    // A failed write only drops this message; the read loop notices the lost connection and reconnects.
    private async Task WriteGuardedAsync(Stream stream, Opcode opcode, byte[] payload)
    {
        await _writeGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await DiscordIpc.WriteAsync(stream, opcode, payload, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException)
        {
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private static string? Text(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length > MaxTextLength)
        {
            trimmed = trimmed[..(MaxTextLength - 1)] + "…";
        }

        return trimmed.Length < 2 ? trimmed + " " : trimmed;
    }

    // Clears the status before letting go, so it doesn't linger after the app closes.
    public void Dispose()
    {
        Stream? ready;
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            ready = _ready;
        }

        if (ready is not null)
            SendAsync(ready, null).Wait(TimeSpan.FromSeconds(1));

        _stop.Cancel();
        _loop?.Wait(TimeSpan.FromSeconds(1));
        _stop.Dispose();
    }
}
