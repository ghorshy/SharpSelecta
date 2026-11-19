using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class IntegrationsSettingsViewModelTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static void Apply(ISettingsCategoryViewModel vm) => vm.ApplyCommand.Execute(null);

    [Test]
    public async Task DiscordPresenceIsOffByDefault()
    {
        var vm = new IntegrationsSettingsViewModel(new SettingsStore(_temp.NewSettingsPath()));

        await Assert.That(vm.DiscordPresenceEnabled).IsFalse();
        await Assert.That(vm.IsDiscordPresenceEnabled).IsFalse();
        await Assert.That(vm.HasPendingChanges).IsFalse();
    }

    [Test]
    public async Task AnEditOnlyTakesEffectOnApply_WhichPersistsItAndRaisesTheEvent()
    {
        var path = _temp.NewSettingsPath();
        var vm = new IntegrationsSettingsViewModel(new SettingsStore(path));
        var raised = 0;
        vm.DiscordPresenceChanged += (_, _) => raised++;

        vm.DiscordPresenceEnabled = true;
        await Assert.That(vm.HasPendingChanges).IsTrue();
        await Assert.That(vm.IsDiscordPresenceEnabled).IsFalse();
        await Assert.That(raised).IsEqualTo(0);

        Apply(vm);

        await Assert.That(vm.IsDiscordPresenceEnabled).IsTrue();
        await Assert.That(vm.HasPendingChanges).IsFalse();
        await Assert.That(raised).IsEqualTo(1);
        await Assert.That(new IntegrationsSettingsViewModel(new SettingsStore(path)).DiscordPresenceEnabled).IsTrue();
    }

    [Test]
    public async Task CancelRevertsAnUnappliedEdit()
    {
        var vm = new IntegrationsSettingsViewModel(new SettingsStore(_temp.NewSettingsPath()));
        vm.DiscordPresenceEnabled = true;

        ((ISettingsCategoryViewModel)vm).CancelCommand.Execute(null);

        await Assert.That(vm.DiscordPresenceEnabled).IsFalse();
        await Assert.That(vm.HasPendingChanges).IsFalse();
    }

    [Test]
    public async Task TheDiscordOptions_DefaultToTodaysStatus_AndAnEditAppliesPersistsAndRaisesTheEvent()
    {
        var path = _temp.NewSettingsPath();
        var vm = new IntegrationsSettingsViewModel(new SettingsStore(path));
        var raised = 0;
        vm.DiscordPresenceChanged += (_, _) => raised++;
        await Assert.That(vm.AppliedDiscordOptions).IsEqualTo(new DiscordPresenceOptions(true, true, true, false));

        vm.DiscordShowAlbum = false;
        vm.DiscordShowWhilePaused = true;
        await Assert.That(vm.HasPendingChanges).IsTrue();
        await Assert.That(vm.AppliedDiscordOptions.ShowAlbum).IsTrue();

        Apply(vm);

        var expected = new DiscordPresenceOptions(ShowArtist: true, ShowAlbum: false, ShowElapsed: true, ShowWhilePaused: true);
        await Assert.That(vm.AppliedDiscordOptions).IsEqualTo(expected);
        await Assert.That(raised).IsEqualTo(1);
        await Assert.That(new IntegrationsSettingsViewModel(new SettingsStore(path)).AppliedDiscordOptions).IsEqualTo(expected);
    }

    [Test]
    public async Task CancelRevertsUnappliedDiscordOptions()
    {
        var vm = new IntegrationsSettingsViewModel(new SettingsStore(_temp.NewSettingsPath()));
        vm.DiscordShowArtist = false;

        ((ISettingsCategoryViewModel)vm).CancelCommand.Execute(null);

        await Assert.That(vm.DiscordShowArtist).IsTrue();
        await Assert.That(vm.HasPendingChanges).IsFalse();
    }

    [Test]
    public async Task ApplyingWithoutAChange_DoesNotRaiseTheEvent()
    {
        var vm = new IntegrationsSettingsViewModel(new SettingsStore(_temp.NewSettingsPath()));
        var raised = 0;
        vm.DiscordPresenceChanged += (_, _) => raised++;

        Apply(vm);

        await Assert.That(raised).IsEqualTo(0);
    }

    [Test]
    public async Task FanartApiKey_IsSavedTrimmedOnlyOnApply()
    {
        var path = _temp.NewSettingsPath();
        var vm = new IntegrationsSettingsViewModel(new SettingsStore(path));

        vm.FanartApiKey = "  abc123  ";
        await Assert.That(vm.HasPendingChanges).IsTrue();
        await Assert.That(new SettingsStore(path).Current.FanartApiKey).IsNull();

        Apply(vm);

        await Assert.That(vm.HasPendingChanges).IsFalse();
        await Assert.That(new SettingsStore(path).Current.FanartApiKey).IsEqualTo("abc123");
    }

    [Test]
    public async Task FanartApiKey_ClearingTheFieldRemovesTheKey()
    {
        var path = _temp.NewSettingsPath();
        var store = new SettingsStore(path);
        store.Update(s => s with { FanartApiKey = "abc123" });
        var vm = new IntegrationsSettingsViewModel(store);

        vm.FanartApiKey = "   ";
        Apply(vm);

        await Assert.That(new SettingsStore(path).Current.FanartApiKey).IsNull();
    }

    [Test]
    public async Task FanartApiKey_CancelRevertsTheField()
    {
        var store = new SettingsStore(_temp.NewSettingsPath());
        store.Update(s => s with { FanartApiKey = "abc123" });
        var vm = new IntegrationsSettingsViewModel(store);

        vm.FanartApiKey = "other";
        ((ISettingsCategoryViewModel)vm).CancelCommand.Execute(null);

        await Assert.That(vm.FanartApiKey).IsEqualTo("abc123");
        await Assert.That(vm.HasPendingChanges).IsFalse();
    }

    [Test]
    public async Task FanartApiKey_ApplyingOnlyADiscordChange_KeepsAKeySavedElsewhere()
    {
        var path = _temp.NewSettingsPath();
        var store = new SettingsStore(path);
        var vm = new IntegrationsSettingsViewModel(store);
        store.Update(s => s with { FanartApiKey = "abc123" });

        vm.DiscordPresenceEnabled = true;
        Apply(vm);

        await Assert.That(new SettingsStore(path).Current.FanartApiKey).IsEqualTo("abc123");
    }
}
