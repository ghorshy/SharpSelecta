using Avalonia.Input;
using SharpSelecta.App.Shortcuts;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class ShortcutBindingsTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainWindowViewModel CreateMainWindowViewModel()
    {
        var vm = TestMainWindowViewModel.Create(_temp.NewSettingsPath(), out _);
        vm.Library.LibrarySection = LibrarySection.Library;
        return vm;
    }

    private static ShortcutBindings Bind(MainWindowViewModel vm, Func<ShortcutScope>? focusedScopes = null) =>
        new(vm, focusedScopes ?? (() => ShortcutScope.Global));

    // Mirrors Avalonia's KeyBinding handling: the first matching binding whose command can execute wins.
    private static bool Press(ShortcutBindings bindings, string gesture)
    {
        var key = KeyGesture.Parse(gesture);
        var binding = bindings.Keys.FirstOrDefault(b => b.Gesture.Equals(key) && b.Command.CanExecute(null));
        binding.Command?.Execute(null);
        return binding.Command is not null;
    }

    [Test]
    public async Task ScopedKeyShortcut_DoesNothingWhileFocusIsOutsideItsView()
    {
        var vm = CreateMainWindowViewModel();
        var requested = 0;
        vm.OpenPropertiesRequested += (_, _) => requested++;
        var focused = ShortcutScope.Global;
        var bindings = Bind(vm, () => focused);

        Press(bindings, "Alt+Enter");
        await Assert.That(requested).IsEqualTo(0);

        focused = ShortcutScope.Queue;
        Press(bindings, "Alt+Enter");
        await Assert.That(requested).IsEqualTo(1);
    }

    [Test]
    public async Task GlobalKeyShortcut_WorksWhereverFocusIs()
    {
        var vm = CreateMainWindowViewModel();
        await Assert.That(Press(Bind(vm, () => ShortcutScope.AlbumGrid), "F5")).IsTrue();
    }

    [Test]
    public async Task SameKeyInTwoScopes_RunsOnlyTheShortcutOfTheFocusedView()
    {
        var vm = CreateMainWindowViewModel();
        vm.ShortcutSettings.SetOverride("Library.IncreaseTileSize", "Alt+Enter");
        var requested = 0;
        vm.OpenPropertiesRequested += (_, _) => requested++;
        var tileSizeBefore = vm.Library.Grid.TileSize;
        var bindings = Bind(vm, () => ShortcutScope.TrackList);

        Press(bindings, "Alt+Enter");

        await Assert.That(requested).IsEqualTo(1);
        await Assert.That(vm.Library.Grid.TileSize).IsEqualTo(tileSizeBefore);
    }

    [Test]
    public async Task WheelShortcut_ResizesTilesByTheWheelDeltaWhenOverTheAlbumGrid()
    {
        var vm = CreateMainWindowViewModel();
        var bindings = Bind(vm);
        var before = vm.Library.Grid.TileSize;

        var handled = bindings.TryHandleWheel(new WheelGesture(KeyModifiers.Control, WheelDirection.Up), ShortcutScope.AlbumGrid, steps: 0.5);

        await Assert.That(handled).IsTrue();
        await Assert.That(vm.Library.Grid.TileSize).IsEqualTo(before + 0.5 * vm.Library.Grid.TileSizeStep);
    }

    [Test]
    public async Task WheelShortcut_OutsideItsView_IsNotHandledSoScrollingStillWorks()
    {
        var vm = CreateMainWindowViewModel();
        var before = vm.Library.Grid.TileSize;

        var handled = Bind(vm).TryHandleWheel(new WheelGesture(KeyModifiers.Control, WheelDirection.Up), ShortcutScope.Queue, steps: 1);

        await Assert.That(handled).IsFalse();
        await Assert.That(vm.Library.Grid.TileSize).IsEqualTo(before);
    }

    [Test]
    public async Task WheelShortcut_FollowsItsRebinding()
    {
        var vm = CreateMainWindowViewModel();
        vm.ShortcutSettings.SetOverride("Library.DecreaseTileSizeWheel", "Alt+WheelUp");
        var bindings = Bind(vm);
        var before = vm.Library.Grid.TileSize;

        var oldGestureHandled = bindings.TryHandleWheel(new WheelGesture(KeyModifiers.Control, WheelDirection.Down), ShortcutScope.AlbumGrid, 1);
        bindings.TryHandleWheel(new WheelGesture(KeyModifiers.Alt, WheelDirection.Up), ShortcutScope.AlbumGrid, 1);

        await Assert.That(oldGestureHandled).IsFalse();
        await Assert.That(vm.Library.Grid.TileSize).IsLessThan(before);
    }

    [Test]
    public async Task WheelGestures_AreNotTurnedIntoKeyBindings()
    {
        var vm = CreateMainWindowViewModel();

        var keyGestures = ShortcutRegistry.All.Count(s => s.DefaultGesture.Length > 0 && !WheelGesture.TryParse(s.DefaultGesture, out _));

        await Assert.That(keyGestures).IsLessThan(ShortcutRegistry.All.Count);
        await Assert.That(Bind(vm).Keys.Count).IsEqualTo(keyGestures);
    }

    [Test]
    public async Task UnmodifiedWheelGesture_FromAHandEditedSettingsFile_IsIgnored()
    {
        var vm = CreateMainWindowViewModel();
        vm.ShortcutSettings.SetOverride("Library.IncreaseTileSizeWheel", "WheelUp");

        var handled = Bind(vm).TryHandleWheel(new WheelGesture(KeyModifiers.None, WheelDirection.Up), ShortcutScope.AlbumGrid, 1);

        await Assert.That(handled).IsFalse();
    }
}
