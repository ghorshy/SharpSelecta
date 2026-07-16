using Avalonia.Controls;
using Avalonia.Controls.Templates;
using SharpSelecta.App.ViewModels;
using SharpSelecta.App.Views;

namespace SharpSelecta.App;

/// <summary>
/// Given a view model, returns the corresponding view. Explicit pattern matching, not reflection,
/// so this stays NativeAOT-compatible — add a new arm here whenever a new ViewModel/View pair is added.
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param) => param switch
    {
        MainWindowViewModel => new MainWindow(),
        null => null,
        _ => new TextBlock { Text = $"No view for {param.GetType().Name}" },
    };

    public bool Match(object? data) => data is ViewModelBase;
}
