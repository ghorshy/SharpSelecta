using Microsoft.Extensions.Logging;
using Tmds.DBus;

namespace SharpSelecta.App.Services.SingleInstance;

public enum SingleInstanceRole
{
    Primary,
    Secondary,
}

public interface ISingleInstanceGuard : IDisposable
{
    // Raised on a background thread.
    event EventHandler<IReadOnlyList<string>>? ActivationRequested;

    // Secondary: an instance is already running and got the activate request, so this process should exit.
    // A failure of the mechanism itself is logged and reported as Primary.
    Task<SingleInstanceRole> StartAsync(IReadOnlyList<string> filePaths);
}

public static class SingleInstanceGuard
{
    public static ISingleInstanceGuard CreateForCurrentPlatform(ILoggerFactory loggerFactory) =>
        OperatingSystem.IsLinux()
            ? new DBusSingleInstanceGuard(
                "io.github.sharpselecta.SharpSelecta", Address.Session, loggerFactory.CreateLogger<DBusSingleInstanceGuard>())
            : new NamedPipeSingleInstanceGuard(
                $"SharpSelecta-{Environment.UserName}", loggerFactory.CreateLogger<NamedPipeSingleInstanceGuard>());
}
