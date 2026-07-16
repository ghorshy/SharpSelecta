using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.App.Views;
using SharpSelecta.Audio;
using SharpSelecta.Core.Audio;

namespace SharpSelecta.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddSerilog(dispose: false));
            services.AddAudioEngine();
            services.AddSingleton<IFilePickerService>(new AvaloniaFilePickerService(mainWindow));
            var provider = services.BuildServiceProvider();

            var audioEngine = provider.GetRequiredService<IAudioEngine>();

            mainWindow.DataContext = new MainWindowViewModel(
                audioEngine,
                provider.GetRequiredService<IFilePickerService>(),
                provider.GetRequiredService<ILogger<MainWindowViewModel>>());
            desktop.MainWindow = mainWindow;

            // Task.Run escapes Avalonia's SynchronizationContext: at this point the classic desktop
            // lifetime hasn't started pumping its dispatcher loop yet, so blocking the UI thread here
            // while awaiting a continuation that expects that loop to be running would deadlock.
            Task.Run(() => audioEngine.InitializeAsync());
        }

        base.OnFrameworkInitializationCompleted();
    }
}