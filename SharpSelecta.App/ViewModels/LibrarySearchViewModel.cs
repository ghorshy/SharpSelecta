using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SharpSelecta.App.ViewModels;

public sealed partial class LibrarySearchViewModel : ViewModelBase
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(150);

    private CancellationTokenSource? _debounceCts;

    [ObservableProperty]
    public partial string Query { get; set; } = "";

    public bool IsActive => !string.IsNullOrWhiteSpace(Query);

    /// <summary>Completes once a pending debounced query has been applied - for tests.</summary>
    public Task DebounceTask { get; private set; } = Task.CompletedTask;

    public event EventHandler? Applied;

    public event EventHandler? FocusRequested;

    // Clearing is exempt from the debounce - "" needs no scoring and should feel instant.
    partial void OnQueryChanged(string value)
    {
        _debounceCts?.Cancel();

        if (!IsActive)
        {
            DebounceTask = Task.CompletedTask;
            Applied?.Invoke(this, EventArgs.Empty);
            return;
        }

        var cts = new CancellationTokenSource();
        _debounceCts = cts;
        DebounceTask = ApplyAfterDebounceAsync(cts.Token);
    }

    private async Task ApplyAfterDebounceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(DebounceDelay, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        Applied?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Clear() => Query = "";

    [RelayCommand]
    private void Focus() => FocusRequested?.Invoke(this, EventArgs.Empty);
}
