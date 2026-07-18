using System;
using Avalonia.Controls;
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

        // Polls the engine for the current playback position/duration so the seek slider
        // stays in sync without the ViewModel depending on Avalonia's dispatcher directly.
        _positionTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background, OnPositionTimerTick);
        _positionTimer.Start();
        Unloaded += (_, _) => _positionTimer.Stop();
    }

    private void OnPositionTimerTick(object? sender, EventArgs e) =>
        (DataContext as PlaybackControlsViewModel)?.RefreshPosition();

    private void OnOptionsClick(object? sender, RoutedEventArgs e)
    {
        if (this.FindAncestorOfType<Window>() is not { DataContext: MainWindowViewModel mainWindowViewModel } window)
            return;

        new SettingsWindow { DataContext = new SettingsWindowViewModel(mainWindowViewModel.Library) }.ShowDialog(window);
    }
}
