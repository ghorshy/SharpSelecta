using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Views;

public partial class PlaybackControlsView : UserControl
{
    private readonly DispatcherTimer _positionTimer;
    private EqualizerWindow? _equalizerWindow;
    private InterfaceSettingsViewModel? _interfaceSettings;
    private PlaybackBarLayout _layout = PlaybackBarLayout.Default;
    private bool _columnsBuilt;

    public PlaybackControlsView()
    {
        InitializeComponent();

        _positionTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background, OnPositionTimerTick);
        _positionTimer.Start();
        Unloaded += (_, _) => _positionTimer.Stop();

        VolumeSlider.AddHandler(InputElement.PointerReleasedEvent, OnVolumeSliderPointerReleased, handledEventsToo: true);
        ApplyLayout(_layout);
    }

    // The layout lives in Interface settings, edits included, so the bar previews them live.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (this.FindAncestorOfType<Window>()?.DataContext is not MainWindowViewModel { InterfaceSettings: var interfaceSettings })
            return;

        _interfaceSettings = interfaceSettings;
        _interfaceSettings.PropertyChanged += OnInterfaceSettingsChanged;
        ApplyLayout(_interfaceSettings.PlaybackBar);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_interfaceSettings is not null)
            _interfaceSettings.PropertyChanged -= OnInterfaceSettingsChanged;
        _interfaceSettings = null;
    }

    private void OnInterfaceSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InterfaceSettingsViewModel.PlaybackBar) && _interfaceSettings is not null)
            ApplyLayout(_interfaceSettings.PlaybackBar);
    }

    private Control ComponentControl(PlaybackBarComponent component) => component switch
    {
        PlaybackBarComponent.Previous => PreviousButton,
        PlaybackBarComponent.PlayPause => PlayPauseButton,
        PlaybackBarComponent.Next => NextButton,
        PlaybackBarComponent.Elapsed => ElapsedText,
        PlaybackBarComponent.SeekBar => SeekBar,
        PlaybackBarComponent.Length => LengthButton,
        PlaybackBarComponent.Volume => VolumeControls,
        PlaybackBarComponent.Repeat => RepeatButton,
        PlaybackBarComponent.Equalizer => EqualizerButton,
        PlaybackBarComponent.Settings => SettingsButton,
        _ => throw new ArgumentOutOfRangeException(nameof(component), component, null),
    };

    // One column per shown component, in the layout's order. In Fill the seek bar's column takes the free
    // width; in Fixed every column fits its content and the row sits left, centered or right.
    private void ApplyLayout(PlaybackBarLayout layout)
    {
        var rebuild = !_columnsBuilt || !layout.Components.SequenceEqual(_layout.Components) || layout.SeekBarWidth != _layout.SeekBarWidth;
        _layout = layout;
        TitleLine.IsVisible = layout.ShowTitle;
        TitleLine.TextAlignment = layout.TitleAlignment == TitleAlignment.Center ? TextAlignment.Center : TextAlignment.Left;

        var fill = layout.SeekBarWidth == SeekBarWidth.Fill;
        BarRow.HorizontalAlignment = fill ? HorizontalAlignment.Stretch : layout.Alignment switch
        {
            PlaybackBarAlignment.Left => HorizontalAlignment.Left,
            PlaybackBarAlignment.Right => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Center,
        };

        // Dragging the percent slider only needs the seek bar's width measured again.
        if (rebuild)
            RebuildColumns(fill);

        InvalidateMeasure();
    }

    private void RebuildColumns(bool fill)
    {
        foreach (var component in PlaybackBarLayout.AllComponents)
        {
            ComponentControl(component).IsVisible = false;
        }

        BarRow.ColumnDefinitions.Clear();
        for (var column = 0; column < _layout.Components.Count; column++)
        {
            var component = _layout.Components[column];
            var control = ComponentControl(component);
            control.IsVisible = true;
            Grid.SetColumn(control, column);
            BarRow.ColumnDefinitions.Add(new ColumnDefinition(
                fill && component == PlaybackBarComponent.SeekBar ? GridLength.Star : GridLength.Auto));
        }

        // Avalonia's Grid keeps its per-child column info until its children change, so after Fill a
        // now-Auto seek bar column would still be sized like the old star column (to nothing).
        var children = BarRow.Children.ToList();
        BarRow.Children.Clear();
        BarRow.Children.AddRange(children);

        SeekBar.Width = double.NaN;
        _columnsBuilt = true;
    }

    // Fixed: the seek bar's share of the bar's width, but never more than the other components leave,
    // so a large share on a narrow window shrinks the seek bar instead of pushing the rest off the edge.
    protected override Size MeasureOverride(Size availableSize)
    {
        if (_layout.SeekBarWidth == SeekBarWidth.Fixed && double.IsFinite(availableSize.Width))
        {
            var others = 0.0;
            foreach (var component in _layout.Components.Where(c => c != PlaybackBarComponent.SeekBar))
            {
                var control = ComponentControl(component);
                control.Measure(Size.Infinity);
                others += control.DesiredSize.Width + BarRow.ColumnSpacing;
            }

            var width = Math.Max(0, Math.Min(availableSize.Width * _layout.SeekBarPercent / 100, availableSize.Width - others));
            if (!SeekBar.Width.Equals(width))
                SeekBar.Width = width;
        }

        return base.MeasureOverride(availableSize);
    }

    private void OnPositionTimerTick(object? sender, EventArgs e) =>
        (DataContext as PlaybackControlsViewModel)?.RefreshPosition();

    private void OnVolumeSliderPointerReleased(object? sender, PointerReleasedEventArgs e) =>
        (DataContext as PlaybackControlsViewModel)?.Volume.Persist();

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (this.FindAncestorOfType<Window>() is not { DataContext: MainWindowViewModel mainWindowViewModel } window)
            return;

        new SettingsWindow
        {
            DataContext = new SettingsWindowViewModel(
                mainWindowViewModel.Library, mainWindowViewModel.PlaybackSettings, mainWindowViewModel.InterfaceSettings,
                mainWindowViewModel.Integrations, mainWindowViewModel.ShortcutSettings, mainWindowViewModel.Updates),
        }.ShowDialog(window);
    }

    private void OnEqualizerClick(object? sender, RoutedEventArgs e)
    {
        if (_equalizerWindow is not null)
        {
            _equalizerWindow.Activate();
            return;
        }

        if (this.FindAncestorOfType<Window>() is not { DataContext: MainWindowViewModel mainWindowViewModel } window)
            return;

        _equalizerWindow = new EqualizerWindow { DataContext = mainWindowViewModel.Equalizer };
        _equalizerWindow.Closed += (_, _) => _equalizerWindow = null;
        _equalizerWindow.Show(window);
    }
}
