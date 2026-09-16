using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace SharpSelecta.App.Views;

public sealed class WaveformSliderView : Control
{
    public static readonly StyledProperty<IReadOnlyList<float>> PeaksProperty =
        AvaloniaProperty.Register<WaveformSliderView, IReadOnlyList<float>>(nameof(Peaks), []);

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<WaveformSliderView, double>(nameof(Value), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<WaveformSliderView, double>(nameof(Maximum), 1.0);

    public static readonly StyledProperty<double> BarWidthProperty =
        AvaloniaProperty.Register<WaveformSliderView, double>(nameof(BarWidth), 2.0);

    public static readonly StyledProperty<double> BarGapProperty =
        AvaloniaProperty.Register<WaveformSliderView, double>(nameof(BarGap), 1.0);

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<WaveformSliderView, bool>(nameof(IsLoading));

    private double? _hoverRatio;
    private bool _isPressed;

    static WaveformSliderView()
    {
        AffectsRender<WaveformSliderView>(PeaksProperty, ValueProperty, MaximumProperty, BarWidthProperty, BarGapProperty, IsLoadingProperty);
    }

    public IReadOnlyList<float> Peaks
    {
        get => GetValue(PeaksProperty);
        set => SetValue(PeaksProperty, value);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    // Constant per-bar footprint in pixels, so bar count adapts to width instead of bar width adapting to bar count.
    public double BarWidth
    {
        get => GetValue(BarWidthProperty);
        set => SetValue(BarWidthProperty, value);
    }

    public double BarGap
    {
        get => GetValue(BarGapProperty);
        set => SetValue(BarGapProperty, value);
    }

    // True while peaks are still being extracted - shows a flat placeholder line instead of a blank control.
    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        UpdateHoverRatio(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        UpdateHoverRatio(e);
        if (_isPressed)
        {
            SeekToPointer(e);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hoverRatio = null;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _isPressed = true;
        SeekToPointer(e);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isPressed = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isPressed = false;
    }

    private void UpdateHoverRatio(PointerEventArgs e)
    {
        if (Bounds.Width <= 0)
            return;

        _hoverRatio = Math.Clamp(e.GetPosition(this).X / Bounds.Width, 0, 1);
        InvalidateVisual();
    }

    private void SeekToPointer(PointerEventArgs e)
    {
        if (Bounds.Width <= 0 || Maximum <= 0)
            return;

        var ratio = Math.Clamp(e.GetPosition(this).X / Bounds.Width, 0, 1);
        Value = ratio * Maximum;
    }

    // Buckets `source` into `targetCount` values, each the max magnitude within its slice -
    // the same reduction FileSource.GetPeaks itself does, just applied client-side so bar
    // count can track the control's live width without a fresh decoder pass per resize.
    public static float[] Downsample(IReadOnlyList<float> source, int targetCount)
    {
        if (targetCount <= 0 || source.Count == 0)
            return [];

        var result = new float[targetCount];
        for (var i = 0; i < targetCount; i++)
        {
            var start = i * source.Count / targetCount;
            var end = Math.Max(start + 1, (i + 1) * source.Count / targetCount);
            var max = 0f;
            for (var j = start; j < end && j < source.Count; j++)
            {
                var v = Math.Abs(source[j]);
                if (v > max)
                    max = v;
            }

            result[i] = max;
        }

        return result;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var centerY = Bounds.Height / 2;
        var unplayedBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xA0, 0xA0, 0xA0));

        var stride = Math.Max(1.0, BarWidth + BarGap);
        var visibleBarCount = Math.Max(1, (int)(Bounds.Width / stride));
        var peaks = Downsample(Peaks, visibleBarCount);
        if (peaks.Length == 0)
        {
            // Nothing to draw yet: while extraction is still in flight, a flat line reads as
            // "loading" rather than "broken" - once IsLoading clears with still no peaks
            // (an empty/duration-less file), the control goes back to fully blank.
            if (IsLoading)
            {
                context.FillRectangle(unplayedBrush, new Rect(0, centerY - 0.5, Bounds.Width, 1));
            }

            return;
        }

        var playedRatio = Maximum > 0 ? Math.Clamp(Value / Maximum, 0, 1) : 0;
        var playedBarCount = (int)(playedRatio * peaks.Length);
        var hoverBarIndex = _hoverRatio is { } hoverRatio ? (int)(hoverRatio * peaks.Length) : -1;

        var lowPreview = Math.Min(playedBarCount, hoverBarIndex < 0 ? playedBarCount : hoverBarIndex);
        var highPreview = Math.Max(playedBarCount, hoverBarIndex);

        var accentColor = this.TryFindResource("SystemAccentColor", out var resource)
            ? resource switch
            {
                Color c => c,
                ISolidColorBrush b => b.Color,
                _ => Colors.DodgerBlue,
            }
            : Colors.DodgerBlue;

        var playedBrush = new SolidColorBrush(accentColor);
        var previewBrush = new SolidColorBrush(accentColor, 0.5);

        for (var i = 0; i < peaks.Length; i++)
        {
            var magnitude = Math.Abs(peaks[i]);
            var halfHeight = magnitude * centerY;
            var x = i * stride;
            var rect = new Rect(x, centerY - halfHeight, BarWidth, halfHeight * 2);

            var brush = hoverBarIndex >= 0 && i >= lowPreview && i < highPreview
                ? previewBrush
                : i < playedBarCount
                    ? playedBrush
                    : unplayedBrush;

            context.FillRectangle(brush, rect);
        }
    }
}
