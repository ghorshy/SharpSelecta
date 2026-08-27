using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Views;

public partial class PlaybackControlsView : UserControl
{
    private readonly DispatcherTimer _positionTimer;

    public PlaybackControlsView()
    {
        InitializeComponent();

        _positionTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background, OnPositionTimerTick);
        _positionTimer.Start();
        Unloaded += (_, _) => _positionTimer.Stop();

        VolumeSlider.AddHandler(InputElement.PointerReleasedEvent, OnVolumeSliderPointerReleased, handledEventsToo: true);
    }

    private void OnPositionTimerTick(object? sender, EventArgs e) =>
        (DataContext as PlaybackControlsViewModel)?.RefreshPosition();

    private void OnVolumeSliderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (this.FindAncestorOfType<Window>() is { DataContext: MainWindowViewModel mainWindowViewModel })
        {
            mainWindowViewModel.PersistVolume();
        }
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (this.FindAncestorOfType<Window>() is not { DataContext: MainWindowViewModel mainWindowViewModel } window)
            return;

        new SettingsWindow { DataContext = new SettingsWindowViewModel(mainWindowViewModel.Library, mainWindowViewModel.PlaybackSettings) }.ShowDialog(window);
    }
}
