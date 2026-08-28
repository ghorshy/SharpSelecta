using System;
using System.Windows.Input;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Shortcuts;

// Id is persisted in settings and must stay stable even if DefaultGesture or Description change.
public sealed record ShortcutDefinition(string Id, string DefaultGesture, Func<string> Description, Func<MainWindowViewModel, ICommand> Command);
