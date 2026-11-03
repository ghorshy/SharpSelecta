using Avalonia.Input;
using SharpSelecta.App.Shortcuts;

namespace SharpSelecta.Tests;

public class WheelGestureTests
{
    [Test]
    public async Task ToString_FormatsModifiersLikeKeyGesture()
    {
        var gesture = new WheelGesture(KeyModifiers.Control | KeyModifiers.Shift, WheelDirection.Up);

        await Assert.That(gesture.ToString()).IsEqualTo("Ctrl+Shift+WheelUp");
    }

    [Test]
    [Arguments("Ctrl+WheelUp", KeyModifiers.Control, WheelDirection.Up)]
    [Arguments("Alt+Shift+WheelDown", KeyModifiers.Alt | KeyModifiers.Shift, WheelDirection.Down)]
    [Arguments("WheelDown", KeyModifiers.None, WheelDirection.Down)]
    public async Task TryParse_ReadsModifiersAndDirection(string text, KeyModifiers modifiers, WheelDirection direction)
    {
        await Assert.That(WheelGesture.TryParse(text, out var gesture)).IsTrue();
        await Assert.That(gesture).IsEqualTo(new WheelGesture(modifiers, direction));
    }

    [Test]
    [Arguments("Ctrl+OemPlus")]
    [Arguments("")]
    [Arguments("Bogus+WheelUp")]
    public async Task TryParse_RejectsAnythingThatIsNotAWheelGesture(string text)
    {
        await Assert.That(WheelGesture.TryParse(text, out _)).IsFalse();
    }
}
