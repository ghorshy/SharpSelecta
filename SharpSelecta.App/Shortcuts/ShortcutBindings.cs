using System.Windows.Input;
using Avalonia.Input;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Shortcuts;

public sealed class ShortcutBindings
{
    private readonly List<(WheelGesture Gesture, ShortcutScope Scope, ICommand Command)> _wheel = [];

    // Key commands are gated on focus at keypress time, so a same-key binding in another scope can win.
    public IReadOnlyList<(KeyGesture Gesture, ICommand Command)> Keys { get; }

    public ShortcutBindings(MainWindowViewModel vm, Func<ShortcutScope> focusedScopes)
    {
        var keys = new List<(KeyGesture, ICommand)>();
        foreach (var shortcut in ShortcutRegistry.All)
        {
            var text = vm.ShortcutSettings.GetEffectiveGesture(shortcut.Id);
            if (text.Length == 0)
                continue;

            if (WheelGesture.TryParse(text, out var wheel))
            {
                // A bare wheel would swallow plain scrolling; the recorder refuses it, so only a hand-edited file gets here.
                if (wheel.Modifiers != KeyModifiers.None)
                    _wheel.Add((wheel, shortcut.Scope, shortcut.Command(vm)));
                continue;
            }

            try
            {
                keys.Add((KeyGesture.Parse(text), new ScopedCommand(shortcut.Command(vm), shortcut.Scope, focusedScopes)));
            }
            catch (ArgumentException)
            {
                // Corrupted/hand-edited settings file - skip this shortcut rather than crash.
            }
        }

        Keys = keys;
    }

    public bool TryHandleWheel(WheelGesture gesture, ShortcutScope scopesUnderPointer, double steps)
    {
        foreach (var (wheelGesture, scope, command) in _wheel)
        {
            if (wheelGesture == gesture && scope.IsActiveIn(scopesUnderPointer) && command.CanExecute(steps))
            {
                command.Execute(steps);
                return true;
            }
        }

        return false;
    }

    private sealed class ScopedCommand(ICommand inner, ShortcutScope scope, Func<ShortcutScope> focusedScopes) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add => inner.CanExecuteChanged += value;
            remove => inner.CanExecuteChanged -= value;
        }

        public bool CanExecute(object? parameter) => scope.IsActiveIn(focusedScopes()) && inner.CanExecute(parameter);

        public void Execute(object? parameter) => inner.Execute(parameter);
    }
}
