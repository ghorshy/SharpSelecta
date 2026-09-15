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

    private double? _hoverRatio;
    private bool _isPressed;

    static WaveformSliderView()
    {
        AffectsRender<WaveformSliderView>(PeaksProperty, ValueProperty, MaximumProperty);
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
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
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

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var peaks = Peaks;
        if (peaks.Count == 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var playedRatio = Maximum > 0 ? Math.Clamp(Value / Maximum, 0, 1) : 0;
        var playedBarCount = (int)(playedRatio * peaks.Count);
        var hoverBarIndex = _hoverRatio is { } hoverRatio ? (int)(hoverRatio * peaks.Count) : -1;

        var lowPreview = Math.Min(playedBarCount, hoverBarIndex < 0 ? playedBarCount : hoverBarIndex);
        var highPreview = Math.Max(playedBarCount, hoverBarIndex);

        var accentColor = this.TryFindResource("SystemAccentColor", out var resource) && resource is Color color
            ? color
            : Colors.DodgerBlue;

        var playedBrush = new SolidColorBrush(accentColor);
        var previewBrush = new SolidColorBrush(accentColor, 0.5);
        var unplayedBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x80, 0x80, 0x80));

        var barWidth = Bounds.Width / peaks.Count;
        var centerY = Bounds.Height / 2;

        for (var i = 0; i < peaks.Count; i++)
        {
            var magnitude = Math.Abs(peaks[i]);
            var halfHeight = magnitude * centerY;
            var x = i * barWidth;
            var rect = new Rect(x, centerY - halfHeight, Math.Max(1, barWidth - 1), halfHeight * 2);

            var brush = i < playedBarCount
                ? playedBrush
                : hoverBarIndex >= 0 && i >= lowPreview && i < highPreview
                    ? previewBrush
                    : unplayedBrush;

            context.FillRectangle(brush, rect);
        }
    }
}
