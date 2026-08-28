using SharpSelecta.App.Services;
using SharpSelecta.App.Shortcuts;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.Tests;

public class KeyboardShortcutsViewModelTests
{
    private static string CreateTempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"sharpselecta-keyboard-shortcuts-vm-tests-{Guid.NewGuid():N}.json");

    private static KeyboardShortcutsViewModel CreateViewModel(out ShortcutSettingsService settings)
    {
        settings = new ShortcutSettingsService(CreateTempSettingsPath());
        return new KeyboardShortcutsViewModel(settings);
    }

    [Test]
    public async Task StartRecording_SetsRecordingRowAndFlagsItAsRecording()
    {
        var vm = CreateViewModel(out _);
        var row = vm.Rows[0];

        vm.StartRecording(row);

        await Assert.That(vm.RecordingRow).IsEqualTo(row);
        await Assert.That(row.IsRecording).IsTrue();
    }

    [Test]
    public async Task StartRecording_OnAnotherRow_StopsRecordingThePreviousOne()
    {
        var vm = CreateViewModel(out _);
        vm.StartRecording(vm.Rows[0]);

        vm.StartRecording(vm.Rows[1]);

        await Assert.That(vm.Rows[0].IsRecording).IsFalse();
        await Assert.That(vm.RecordingRow).IsEqualTo(vm.Rows[1]);
    }

    [Test]
    public async Task CancelRecording_ClearsTheRecordingRowWithoutChangingItsGesture()
    {
        var vm = CreateViewModel(out _);
        var row = vm.Rows[0];
        var originalGesture = row.Gesture;
        vm.StartRecording(row);

        vm.CancelRecording();

        await Assert.That(vm.RecordingRow).IsNull();
        await Assert.That(row.IsRecording).IsFalse();
        await Assert.That(row.Gesture).IsEqualTo(originalGesture);
    }

    [Test]
    public async Task CompleteRecording_SetsTheGestureAndEndsRecording()
    {
        var vm = CreateViewModel(out var settings);
        var row = vm.Rows[0];
        vm.StartRecording(row);

        vm.CompleteRecording("Ctrl+Shift+Alt+F");

        await Assert.That(row.Gesture).IsEqualTo("Ctrl+Shift+Alt+F");
        await Assert.That(row.IsRecording).IsFalse();
        await Assert.That(vm.RecordingRow).IsNull();
        await Assert.That(settings.GetEffectiveGesture(row.Id)).IsEqualTo("Ctrl+Shift+Alt+F");
    }

    [Test]
    public async Task CompleteRecording_WithAGestureAlreadyUsedByAnotherRow_SetsAConflictWarning()
    {
        var vm = CreateViewModel(out _);
        var otherRow = vm.Rows[1];
        var row = vm.Rows[0];
        vm.StartRecording(row);

        vm.CompleteRecording(otherRow.Gesture);

        await Assert.That(row.ConflictWarning).IsNotNull();
    }

    [Test]
    public async Task CompleteRecording_WithAUniqueGesture_HasNoConflictWarning()
    {
        var vm = CreateViewModel(out _);
        var row = vm.Rows[0];
        vm.StartRecording(row);

        vm.CompleteRecording("Ctrl+Shift+Alt+F");

        await Assert.That(row.ConflictWarning).IsNull();
    }

    [Test]
    public async Task ResetToDefault_RevertsTheGestureAndRemovesTheOverride()
    {
        var vm = CreateViewModel(out var settings);
        var row = vm.Rows[0];
        vm.StartRecording(row);
        vm.CompleteRecording("Ctrl+Shift+Alt+F");

        vm.ResetToDefault(row);

        await Assert.That(row.Gesture).IsEqualTo(row.DefaultGesture);
        await Assert.That(settings.GetEffectiveGesture(row.Id)).IsEqualTo(row.DefaultGesture);
    }

    [Test]
    public async Task ClearShortcut_SetsAnEmptyGestureAndPersistsIt()
    {
        var vm = CreateViewModel(out var settings);
        var row = vm.Rows[0];

        vm.ClearShortcut(row);

        await Assert.That(row.Gesture).IsEqualTo(string.Empty);
        await Assert.That(settings.GetEffectiveGesture(row.Id)).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task ClearShortcut_WhileRecordingThatRow_CancelsTheRecording()
    {
        var vm = CreateViewModel(out _);
        var row = vm.Rows[0];
        vm.StartRecording(row);

        vm.ClearShortcut(row);

        await Assert.That(vm.RecordingRow).IsNull();
        await Assert.That(row.IsRecording).IsFalse();
    }

    [Test]
    public async Task Rows_AreBuiltFromTheShortcutRegistryInOrder()
    {
        var vm = CreateViewModel(out _);

        await Assert.That(vm.Rows.Select(r => r.Id)).IsEquivalentTo(ShortcutRegistry.All.Select(d => d.Id));
    }
}
