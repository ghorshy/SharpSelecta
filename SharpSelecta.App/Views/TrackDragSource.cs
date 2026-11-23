using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Views;

// Library tracks dragged out of a track list, an album or an album tile; the queue takes them.
public static class TrackDrag
{
    public static readonly DataFormat<IReadOnlyList<Track>> Format =
        DataFormat.CreateInProcessFormat<IReadOnlyList<Track>>("SharpSelecta.Tracks");
}

// Turns a left press that then moves past the threshold (or, with StartWhenLeaving, leaves the
// control) into a drag of the tracks resolve gives for the pressed element. Resolved when the drag
// starts, so a selection the press just changed is what gets dragged.
internal sealed class TrackDragSource
{
    private const double DragThreshold = 4;

    private readonly Control _control;
    private readonly Func<Visual, IReadOnlyList<Track>?> _resolve;
    private PointerPressedEventArgs? _pressedArgs;
    private Visual? _pressedSource;
    private Point _pressedPoint;

    public TrackDragSource(Control control, Func<Visual, IReadOnlyList<Track>?> resolve)
    {
        _control = control;
        _resolve = resolve;
        control.AddHandler(InputElement.PointerPressedEvent, OnPressed, handledEventsToo: true);
        control.AddHandler(InputElement.PointerMovedEvent, OnMoved, handledEventsToo: true);
        control.AddHandler(InputElement.PointerReleasedEvent, (_, _) => Clear(), handledEventsToo: true);
    }

    public Action<Exception>? OnError { get; init; }

    // With the pressed element, so the view can mark what is being dragged until Finished.
    public event EventHandler<Visual>? Starting;

    public event EventHandler? Finished;

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        Clear();
        if (!e.GetCurrentPoint(_control).Properties.IsLeftButtonPressed || e.Source is not Visual source)
            return;

        _pressedArgs = e;
        _pressedSource = source;
        _pressedPoint = e.GetPosition(_control);
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedArgs is null)
            return;

        if (!e.GetCurrentPoint(_control).Properties.IsLeftButtonPressed)
        {
            Clear();
            return;
        }

        if (Point.Distance(e.GetPosition(_control), _pressedPoint) >= DragThreshold)
            _ = StartAsync();
    }

    private async Task StartAsync()
    {
        var pressedArgs = _pressedArgs!;
        var source = _pressedSource!;
        Clear();

        if (_resolve(source) is not { Count: > 0 } tracks)
            return;

        Starting?.Invoke(this, source);
        try
        {
            var dataTransfer = new DataTransfer();
            dataTransfer.Add(DataTransferItem.Create(TrackDrag.Format, tracks));
            // Copy into the queue; Move within a playlist reordering its own rows.
            await DragDrop.DoDragDropAsync(pressedArgs, dataTransfer, DragDropEffects.Copy | DragDropEffects.Move);
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
        }
        finally
        {
            Finished?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Clear()
    {
        _pressedArgs = null;
        _pressedSource = null;
    }
}
