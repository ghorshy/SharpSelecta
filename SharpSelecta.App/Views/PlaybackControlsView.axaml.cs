using System;
using Avalonia.Controls;
using Avalonia.Threading;
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
}
