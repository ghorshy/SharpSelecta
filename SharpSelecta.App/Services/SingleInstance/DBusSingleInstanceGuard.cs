using Microsoft.Extensions.Logging;
using Tmds.DBus;

namespace SharpSelecta.App.Services.SingleInstance;

public sealed class DBusSingleInstanceGuard(string busName, string? address, ILogger<DBusSingleInstanceGuard> logger)
    : ISingleInstanceGuard, ISingleInstanceActivation
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(3);

    private Connection? _connection;

    public ObjectPath ObjectPath { get; } = new("/io/github/sharpselecta/SingleInstance");

    public event EventHandler<IReadOnlyList<string>>? ActivationRequested;

    public async Task<SingleInstanceRole> StartAsync(IReadOnlyList<string> filePaths)
    {
        if (address is null)
        {
            logger.LogWarning("No D-Bus session bus address available - starting without the single-instance check");
            return SingleInstanceRole.Primary;
        }

        var connection = new Connection(address);
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

    private async Task<SingleInstanceRole> StartOnAsync(Connection connection, IReadOnlyList<string> filePaths)
    {
        await connection.ConnectAsync();
        await connection.RegisterObjectAsync(this);
        if (await TryOwnBusNameAsync(connection))
        {
            return SingleInstanceRole.Primary;
        }

        await connection.CreateProxy<ISingleInstanceActivation>(busName, ObjectPath).ActivateAsync([.. filePaths]);
        return SingleInstanceRole.Secondary;
    }

    private async Task<bool> TryOwnBusNameAsync(Connection connection)
    {
        try
        {
            // None: never take the name from a running instance, and don't queue for it.
            await connection.RegisterServiceAsync(busName, ServiceRegistrationOptions.None);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    Task ISingleInstanceActivation.ActivateAsync(string[] filePaths)
    {
        ActivationRequested?.Invoke(this, filePaths);
        return Task.CompletedTask;
    }

    public void Dispose() => _connection?.Dispose();
}
