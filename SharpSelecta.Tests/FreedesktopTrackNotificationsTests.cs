using SharpSelecta.App.Services.Notifications;

namespace SharpSelecta.Tests;

public class FreedesktopTrackNotificationsTests
{
    [Test]
    public async Task ActionsFor_WithoutTheActionsCapability_IsEmpty()
    {
        var actions = FreedesktopTrackNotifications.ActionsFor(["body", "body-markup"]);

        await Assert.That(actions).IsEmpty();
    }

    [Test]
    public async Task ActionsFor_WithTheActionsCapability_OffersSkip()
    {
        var actions = FreedesktopTrackNotifications.ActionsFor(["body", "actions"]);

        await Assert.That(actions).IsEquivalentTo(["next", "Skip"]);
    }

    [Test]
    [Arguments(7u, "next", 7u, true)]
    [Arguments(6u, "next", 7u, false)]
    [Arguments(7u, "default", 7u, false)]
    public async Task IsSkip_OnlyForTheNextActionOfTheLatestNotification(uint id, string actionKey, uint latestId, bool expected)
    {
        await Assert.That(FreedesktopTrackNotifications.IsSkip(id, actionKey, latestId)).IsEqualTo(expected);
    }
}
