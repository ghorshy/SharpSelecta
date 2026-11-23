using System.Buffers.Binary;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharpSelecta.Integrations.Discord;

// Discord's local RPC: a socket (Unix) or named pipe (Windows) called discord-ipc-0..9, carrying frames of
// opcode (uint32 LE), length (uint32 LE) and a UTF-8 JSON payload.
public static partial class DiscordIpc
{
    public enum Opcode : uint
    {
        Handshake = 0,
        Frame = 1,
        Close = 2,
        Ping = 3,
        Pong = 4,
    }

    public sealed record Frame(Opcode Opcode, byte[] Payload);

    private const int PipeCount = 10;

    // Flatpak and Snap Discord put the socket in a subdirectory of the runtime dir.
    private static readonly string[] PackageDirectories = ["", "app/com.discordapp.Discord", "snap.discord"];

    public static async Task<Stream?> ConnectAsync(CancellationToken cancellationToken)
    {
        for (var index = 0; index < PipeCount; index++)
        {
            var stream = OperatingSystem.IsWindows()
                ? await ConnectPipeAsync($"discord-ipc-{index}", cancellationToken).ConfigureAwait(false)
                : await ConnectSocketAsync(index, cancellationToken).ConfigureAwait(false);
            if (stream is not null)
                return stream;
        }

        return null;
    }

    private static async Task<Stream?> ConnectPipeAsync(string name, CancellationToken cancellationToken)
    {
        var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            return pipe;
        }
        catch (Exception exception) when (exception is TimeoutException or IOException)
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            return null;
        }
    }

    private static async Task<Stream?> ConnectSocketAsync(int index, CancellationToken cancellationToken)
    {
        foreach (var path in SocketPaths(index))
        {
            if (!File.Exists(path))
                continue;

            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(path), cancellationToken).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException)
            {
                socket.Dispose();
            }
        }

        return null;
    }

    public static IEnumerable<string> SocketPaths(int index)
    {
        var directories = new[] { "XDG_RUNTIME_DIR", "TMPDIR", "TMP", "TEMP" }
            .Select(Environment.GetEnvironmentVariable)
            .OfType<string>()
            .Append("/tmp")
            .Distinct();
        return directories.SelectMany(directory =>
            PackageDirectories.Select(package => Path.Combine(directory, package, $"discord-ipc-{index}")));
    }

    public static async Task WriteAsync(Stream stream, Opcode opcode, byte[] payload, CancellationToken cancellationToken)
    {
        var frame = new byte[8 + payload.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(frame, (uint)opcode);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4), (uint)payload.Length);
        payload.CopyTo(frame, 8);
        await stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    // Null once the other side has closed the connection.
    public static async Task<Frame?> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[8];
        try
        {
            await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
            var payload = new byte[BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4))];
            await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
            return new Frame((Opcode)BinaryPrimitives.ReadUInt32LittleEndian(header), payload);
        }
        catch (EndOfStreamException)
        {
            return null;
        }
    }

    public static byte[] Handshake(string applicationId) =>
        JsonSerializer.SerializeToUtf8Bytes(new HandshakePayload(1, applicationId), DiscordJsonContext.Default.HandshakePayload);

    public static byte[] SetActivity(Activity? activity, string nonce) =>
        JsonSerializer.SerializeToUtf8Bytes(
            new CommandPayload("SET_ACTIVITY", new ActivityArgs(Environment.ProcessId, activity), nonce),
            DiscordJsonContext.Default.CommandPayload);

    // The "evt" of a frame Discord sent (READY once the handshake is accepted), or null.
    public static string? EventOf(byte[] payload)
    {
        try
        {
            return JsonSerializer.Deserialize(payload, DiscordJsonContext.Default.ServerPayload)?.Evt;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Type 2 is "Listening to"; timestamps are Unix milliseconds.
    public sealed record Activity(int Type, string? Details, string? State, ActivityAssets? Assets, ActivityTimestamps? Timestamps);

    public sealed record ActivityAssets(string? LargeImage, string? LargeText);

    public sealed record ActivityTimestamps(long? Start, long? End);

    internal sealed record HandshakePayload(int V, string ClientId);

    internal sealed record CommandPayload(string Cmd, ActivityArgs Args, string Nonce);

    // A null activity clears the status, so it's written even when null.
    internal sealed record ActivityArgs(int Pid, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Activity? Activity);

    internal sealed record ServerPayload(string? Cmd, string? Evt);

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(HandshakePayload))]
    [JsonSerializable(typeof(CommandPayload))]
    [JsonSerializable(typeof(ServerPayload))]
    internal sealed partial class DiscordJsonContext : JsonSerializerContext
    {
    }
}
