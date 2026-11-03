using Avalonia;
using Serilog;
using Serilog.Extensions.Logging;
using SharpSelecta.App.Services;
using SharpSelecta.App.Services.SingleInstance;
using SharpSelecta.Core.Library;
using Velopack;

namespace SharpSelecta.App;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Must run first: the Windows installer/updater may start the app just to run a hook and exit.
        if (OperatingSystem.IsWindows())
        {
            VelopackApp.Build()
                .OnAfterInstallFastCallback(_ => UpdateFileAssociations(register: true))
                .OnAfterUpdateFastCallback(_ => UpdateFileAssociations(register: true))
                .OnBeforeUninstallFastCallback(_ => UpdateFileAssociations(register: false))
                .Run();
        }

        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SharpSelecta", "logs");
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDirectory, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();

        var loggerFactory = new SerilogLoggerFactory(Log.Logger);
        var singleInstance = SingleInstanceGuard.CreateForCurrentPlatform(loggerFactory);
        var activations = new ActivationInbox(singleInstance);
        var openedPaths = OpenedFiles.ToAbsoluteArguments(args, Environment.CurrentDirectory);

        try
        {
            // No dispatcher or synchronization context exists yet, so blocking here can't deadlock.
            if (singleInstance.StartAsync(openedPaths).GetAwaiter().GetResult() == SingleInstanceRole.Secondary)
            {
                Log.Information("SharpSelecta is already running - asked it to come to the front, exiting");
                return;
            }

            BuildAvaloniaApp(() => new App { Activations = activations, OpenedPaths = openedPaths }).StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Unhandled exception crashed the application");
            throw;
        }
        finally
        {
            singleInstance.Dispose();
            loggerFactory.Dispose();
            Log.CloseAndFlush();
        }
    }

    // Runs before logging is set up, and a throwing hook would fail the install, so failures only skip the "Open with" entries.
    private static void UpdateFileAssociations(bool register)
    {
        if (!OperatingSystem.IsWindows() || Environment.ProcessPath is not { } exePath)
            return;

        try
        {
            var associations = WindowsFileAssociations.ForCurrentUser(exePath);
            if (register)
            {
                associations.Register();
            }
            else
            {
                associations.Unregister();
            }
        }
        catch (Exception)
        {
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(() => new App());

    private static AppBuilder BuildAvaloniaApp(Func<App> createApp)
    {
        var builder = AppBuilder.Configure(createApp)
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

        if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") is not null)
        {
            builder = builder.UseWayland();
        }

        return builder;
    }
}
