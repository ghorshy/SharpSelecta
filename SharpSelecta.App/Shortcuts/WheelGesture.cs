using Avalonia.Input;

namespace SharpSelecta.App.Shortcuts;

public enum WheelDirection { Up, Down }

public readonly record struct WheelGesture(KeyModifiers Modifiers, WheelDirection Direction)
{
    private const string Prefix = "Wheel";

    public static WheelGesture? FromWheel(KeyModifiers modifiers, double deltaY) => deltaY switch
    {
        > 0 => new WheelGesture(modifiers, WheelDirection.Up),
        < 0 => new WheelGesture(modifiers, WheelDirection.Down),
        _ => null,
    };

    public override string ToString()
    {
        var wheel = Prefix + Direction;
        return Modifiers == KeyModifiers.None ? wheel : $"{new KeyGesture(Key.None, Modifiers)}+{wheel}";
    }

    public static bool TryParse(string text, out WheelGesture gesture)
    {
        gesture = default;
        var separator = text.LastIndexOf('+');
        var wheel = text[(separator + 1)..];
        if (!wheel.StartsWith(Prefix, StringComparison.Ordinal) ||
            !Enum.TryParse<WheelDirection>(wheel[Prefix.Length..], out var direction))
            return false;

        var modifiers = KeyModifiers.None;
        if (separator > 0)
        {
            // Borrow KeyGesture's modifier parsing so both gesture kinds accept the same spellings.
            try
            {
                modifiers = KeyGesture.Parse(text[..separator] + "+A").KeyModifiers;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        gesture = new WheelGesture(modifiers, direction);
        return true;
    }
}
