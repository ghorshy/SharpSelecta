using System.IO.Pipes;
using Microsoft.Extensions.Logging;

namespace SharpSelecta.App.Services.SingleInstance;

public sealed class NamedPipeSingleInstanceGuard(
    string name, ILogger<NamedPipeSingleInstanceGuard> logger, TimeSpan? connectTimeout = null) : ISingleInstanceGuard
{
    private const int MaxMessageBytes = 16 * 1024 * 1024;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    private readonly TimeSpan _connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(3);
    private readonly CancellationTokenSource _stopListening = new();
    private Mutex? _mutex;

    public event EventHandler<IReadOnlyList<string>>? ActivationRequested;

    public async Task<SingleInstanceRole> StartAsync(IReadOnlyList<string> filePaths)
    {
        try
        {
            var mutex = new Mutex(false, $@"Local\{name}", out var createdNew);
            if (createdNew)
            {
                _mutex = mutex;
                _ = ListenAsync(_stopListening.Token);
                return SingleInstanceRole.Primary;
            }

            mutex.Dispose();
            await SendAsync(filePaths);
            return SingleInstanceRole.Secondary;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Single-instance check failed - starting as a separate instance");
            return SingleInstanceRole.Primary;
        }
    }

    private async Task SendAsync(IReadOnlyList<string> filePaths)
    {
        await using var client = new NamedPipeClientStream(".", name, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await client.ConnectAsync((int)_connectTimeout.TotalMilliseconds);
        await client.WriteAsync(Encode(filePaths));
        await client.FlushAsync();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(cancellationToken);

                using var message = new MemoryStream();
                var buffer = new byte[64 * 1024];
                int read;
                while ((read = await server.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    if (message.Length + read > MaxMessageBytes)
                    {
                        throw new InvalidDataException("Activate request is too large");
                    }

                    message.Write(buffer, 0, read);
                }

                ActivationRequested?.Invoke(this, Decode(message.ToArray()));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to handle an activate request from another instance");
                try
                {
                    await Task.Delay(RetryDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private static byte[] Encode(IReadOnlyList<string> filePaths)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(filePaths.Count);
            foreach (var path in filePaths)
            {
                writer.Write(path);
            }
        }

        return stream.ToArray();
    }

    private static IReadOnlyList<string> Decode(byte[] message)
    {
        using var reader = new BinaryReader(new MemoryStream(message));
        var count = reader.ReadInt32();
        var paths = new List<string>();
        for (var i = 0; i < count; i++)
        {
            paths.Add(reader.ReadString());
        }

        return paths;
    }

    public void Dispose()
    {
        _stopListening.Cancel();
        _stopListening.Dispose();
        _mutex?.Dispose();
    }
}
