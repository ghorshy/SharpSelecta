using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SharpSelecta.App;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

internal static class TestMainWindowViewModel
{
    private static readonly int[] StandardEqualizerFrequencies = [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

    public static MainWindowViewModel Create(string settingsFilePath, out IAudioEngine audioEngine)
    {
        audioEngine = Substitute.For<IAudioEngine>();
        audioEngine.EqualizerBandFrequenciesHz.Returns(StandardEqualizerFrequencies);
        audioEngine.EqualizerBandGainsDb.Returns(new float[10]);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(audioEngine);
        services.AddSingleton(Substitute.For<IOutputDeviceService>());
        services.AddSingleton(Substitute.For<IFilePickerService>());
        services.AddSingleton(Substitute.For<IFileManagerService>());
        services.AddViewModels(new SettingsStore(settingsFilePath), TestThemeLayout.Default);

        return services.BuildServiceProvider().GetRequiredService<MainWindowViewModel>();
    }
}
