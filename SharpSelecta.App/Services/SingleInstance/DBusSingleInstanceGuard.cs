using Microsoft.Extensions.Logging;
using SharpSelecta.App.DBus;
using Tmds.DBus.Protocol;

namespace SharpSelecta.App.Services.SingleInstance;

public sealed class DBusSingleInstanceGuard(string busName, string? address, ILogger<DBusSingleInstanceGuard> logger)
    : ISingleInstanceGuard
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(3);

    private DBusConnection? _connection;

    public event EventHandler<IReadOnlyList<string>>? ActivationRequested;

    public async Task<SingleInstanceRole> StartAsync(IReadOnlyList<string> filePaths)
    {
        if (address is null)
        {
            logger.LogWarning("No D-Bus session bus address available - starting without the single-instance check");
            return SingleInstanceRole.Primary;
        }

        var connection = new DBusConnection(address);
        try
        {
            var role = await StartOnAsync(connection, filePaths).WaitAsync(StartTimeout);
            if (role == SingleInstanceRole.Primary)
            {
                _connection = connection;
            }
            else
            {
                connection.Dispose();
            }

            return role;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Single-instance check failed - starting as a separate instance");
            connection.Dispose();
            return SingleInstanceRole.Primary;
        }
    }

    private async Task<SingleInstanceRole> StartOnAsync(DBusConnection connection, IReadOnlyList<string> filePaths)
    {
        await connection.ConnectAsync();
        connection.AddMethodHandler(new ActivationHandler(connection, this));

        // None: never take the name from a running instance, and don't queue for it.
        if (await connection.TryRequestNameAsync(busName, RequestNameOptions.None))
        {
            return SingleInstanceRole.Primary;
        }

        await ActivateRunningInstanceAsync(connection, [.. filePaths]);
        return SingleInstanceRole.Secondary;
    }

    private Task ActivateRunningInstanceAsync(DBusConnection connection, string[] filePaths)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(
            destination: busName,
            path: AppIdentity.SingleInstanceObjectPath,
            @interface: AppIdentity.SingleInstanceInterface,
            signature: "as",
            member: "Activate");
        writer.WriteArray(filePaths);
        return connection.CallMethodAsync(writer.CreateMessage());
    }

    public void Dispose() => _connection?.Dispose();

    private sealed class ActivationHandler(DBusConnection connection, DBusSingleInstanceGuard guard)
        : DBusHandler(connection, AppIdentity.SingleInstanceObjectPath, handlesChildPaths: false, handleOnCapturedContext: false),
            ISingleInstanceHandler
    {
        public ValueTask ActivateAsync(string[] filePaths)
        {
            guard.ActivationRequested?.Invoke(guard, filePaths);
            return default;
        }
    }
}
