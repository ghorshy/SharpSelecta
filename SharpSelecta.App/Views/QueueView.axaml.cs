using Avalonia.Controls;
using Avalonia.Input;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.App.Views;

public partial class QueueView : UserControl
{
    public QueueView()
    {
        InitializeComponent();
    }

    private void OnEntryDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: QueueEntry entry } && DataContext is QueueViewModel vm)
        {
            vm.PlayEntryCommand.Execute(entry);
        }
    }
}
