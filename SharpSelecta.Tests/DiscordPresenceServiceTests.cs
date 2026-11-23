using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using SharpSelecta.Core.Presence;
using SharpSelecta.Integrations.Discord;

namespace SharpSelecta.Tests;

public class DiscordPresenceServiceTests : IDisposable
{
    private const string ApplicationId = "1234567890";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    // A stand-in for the Discord client on a Unix socket: answers the handshake with READY and records frames.
    private sealed class FakeDiscord : IAsyncDisposable
    {
        private readonly Socket _listener = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        private readonly CancellationTokenSource _stop = new();
        private Stream? _connection;

        public FakeDiscord(string path)
        {
            _listener.Bind(new UnixDomainSocketEndPoint(path));
            _listener.Listen();
            _ = AcceptAsync();
        }

        public Channel<(DiscordIpc.Opcode Opcode, JsonElement Json)> Received { get; } = Channel.CreateUnbounded<(DiscordIpc.Opcode, JsonElement)>();

        private async Task AcceptAsync()
        {
            try
            {
                while (true)
                {
                    var socket = await _listener.AcceptAsync(_stop.Token);
                    var stream = new NetworkStream(socket, ownsSocket: true);
                    _connection = stream;
                    _ = ServeAsync(stream);
                }
            }
            catch (Exception exception) when (exception is OperationCanceledException or SocketException or ObjectDisposedException)
            {
            }
        }

        private async Task ServeAsync(Stream stream)
        {
            try
            {
                while (await DiscordIpc.ReadAsync(stream, _stop.Token) is { } frame)
                {
                    await Received.Writer.WriteAsync((frame.Opcode, JsonDocument.Parse(frame.Payload).RootElement.Clone()));
                    if (frame.Opcode == DiscordIpc.Opcode.Handshake)
                        await DiscordIpc.WriteAsync(stream, DiscordIpc.Opcode.Frame, """{"cmd":"DISPATCH","evt":"READY","data":{}}"""u8.ToArray(), _stop.Token);
                }
            }
            catch (Exception exception) when (exception is OperationCanceledException or IOException or ObjectDisposedException)
            {
            }
        }

        public Task PingAsync(string payload) =>
            DiscordIpc.WriteAsync(_connection!, DiscordIpc.Opcode.Ping, Encoding.UTF8.GetBytes(payload), CancellationToken.None);

        // Like quitting Discord: the client's connection drops.
        public void DropConnection() => _connection?.Dispose();

        public async Task<(DiscordIpc.Opcode Opcode, JsonElement Json)> NextAsync() =>
            await Received.Reader.ReadAsync().AsTask().WaitAsync(Timeout);

        public async Task<JsonElement> NextActivityAsync()
        {
            while (true)
            {
                var (opcode, json) = await NextAsync();
                if (opcode == DiscordIpc.Opcode.Frame && json.GetProperty("cmd").GetString() == "SET_ACTIVITY")
                    return json.GetProperty("args").GetProperty("activity");
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            _connection?.Dispose();
            _listener.Dispose();
        }
    }

    private string SocketPath() => _temp.FilePath("discord-ipc-0");

    private static DiscordPresenceService Service(string path) =>
        new(ApplicationId, async cancellationToken =>
        {
            if (!File.Exists(path))
                return null;

            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(path), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException)
            {
                socket.Dispose();
                return null;
            }
        }, TimeSpan.FromMilliseconds(50));

    private static PresenceActivity Song(string title = "Song") => new(
        title, "The Artist", "The Album",
        DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000), DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_180_000));

    [Test]
    public async Task Show_HandshakesThenSendsAListeningActivity()
    {
        await using var discord = new FakeDiscord(SocketPath());
        using var service = Service(SocketPath());

        service.Show(Song());

        var (opcode, handshake) = await discord.NextAsync();
        await Assert.That(opcode).IsEqualTo(DiscordIpc.Opcode.Handshake);
        await Assert.That(handshake.GetProperty("v").GetInt32()).IsEqualTo(1);
        await Assert.That(handshake.GetProperty("client_id").GetString()).IsEqualTo(ApplicationId);

        var activity = await discord.NextActivityAsync();
        await Assert.That(activity.GetProperty("type").GetInt32()).IsEqualTo(2);
        await Assert.That(activity.GetProperty("details").GetString()).IsEqualTo("Song");
        await Assert.That(activity.GetProperty("state").GetString()).IsEqualTo("The Artist");
        await Assert.That(activity.GetProperty("assets").GetProperty("large_image").GetString()).IsEqualTo("logo");
        await Assert.That(activity.GetProperty("assets").GetProperty("large_text").GetString()).IsEqualTo("The Album");
        await Assert.That(activity.GetProperty("timestamps").GetProperty("start").GetInt64()).IsEqualTo(1_700_000_000_000);
        await Assert.That(activity.GetProperty("timestamps").GetProperty("end").GetInt64()).IsEqualTo(1_700_000_180_000);
    }

    [Test]
    public async Task Show_BeforeDiscordRuns_IsSentOnceItStarts()
    {
        using var service = Service(SocketPath());
        service.Show(Song("Early"));
        await Task.Delay(150);

        await using var discord = new FakeDiscord(SocketPath());

        var activity = await discord.NextActivityAsync();
        await Assert.That(activity.GetProperty("details").GetString()).IsEqualTo("Early");
    }

    [Test]
    public async Task AfterDiscordRestarts_TheCurrentActivityIsSentAgain()
    {
        await using var discord = new FakeDiscord(SocketPath());
        using var service = Service(SocketPath());
        service.Show(Song("Again"));
        await discord.NextActivityAsync();

        discord.DropConnection();

        var activity = await discord.NextActivityAsync();
        await Assert.That(activity.GetProperty("details").GetString()).IsEqualTo("Again");
    }

    [Test]
    public async Task Clear_SendsANullActivity()
    {
        await using var discord = new FakeDiscord(SocketPath());
        using var service = Service(SocketPath());
        service.Show(Song());
        await discord.NextActivityAsync();

        service.Clear();

        var activity = await discord.NextActivityAsync();
        await Assert.That(activity.ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    [Test]
    public async Task Dispose_ClearsTheStatus()
    {
        await using var discord = new FakeDiscord(SocketPath());
        var service = Service(SocketPath());
        service.Show(Song());
        await discord.NextActivityAsync();

        service.Dispose();

        var activity = await discord.NextActivityAsync();
        await Assert.That(activity.ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    [Test]
    public async Task APing_IsAnsweredWithAPongCarryingTheSamePayload()
    {
        await using var discord = new FakeDiscord(SocketPath());
        using var service = Service(SocketPath());
        service.Show(Song());
        await discord.NextActivityAsync();

        await discord.PingAsync("""{"n":7}""");

        var (opcode, json) = await discord.NextAsync();
        await Assert.That(opcode).IsEqualTo(DiscordIpc.Opcode.Pong);
        await Assert.That(json.GetProperty("n").GetInt32()).IsEqualTo(7);
    }

    [Test]
    public async Task Texts_AreKeptWithinDiscordsLimits()
    {
        await using var discord = new FakeDiscord(SocketPath());
        using var service = Service(SocketPath());

        service.Show(Song(new string('x', 200)) with { Artists = "A" });

        var activity = await discord.NextActivityAsync();
        await Assert.That(activity.GetProperty("details").GetString()!.Length).IsEqualTo(128);
        await Assert.That(activity.GetProperty("details").GetString()).EndsWith("…");
        await Assert.That(activity.GetProperty("state").GetString()).IsEqualTo("A ");
    }

    [Test]
    public async Task SocketPaths_LookInTheRuntimeDirAndTheFlatpakAndSnapFolders()
    {
        var paths = DiscordIpc.SocketPaths(3).ToList();

        await Assert.That(paths).Contains(Path.Combine("/tmp", "discord-ipc-3"));
        await Assert.That(paths).Contains(Path.Combine("/tmp", "app/com.discordapp.Discord", "discord-ipc-3"));
        await Assert.That(paths).Contains(Path.Combine("/tmp", "snap.discord", "discord-ipc-3"));
    }
}
