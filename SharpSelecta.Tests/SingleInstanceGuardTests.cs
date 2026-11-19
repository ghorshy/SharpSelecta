using Microsoft.Extensions.Logging.Abstractions;
using SharpSelecta.App.Services.SingleInstance;
using Tmds.DBus;

namespace SharpSelecta.Tests;

public class SingleInstanceGuardTests
{
    private static NamedPipeSingleInstanceGuard CreatePipeGuard(string name) =>
        new(name, NullLogger<NamedPipeSingleInstanceGuard>.Instance);

    private static string UniquePipeName() => $"SharpSelecta-Test-{Guid.NewGuid():N}";

    [Test]
    public async Task Pipe_SecondStart_ForwardsPathsToTheFirstInstance()
    {
        var name = UniquePipeName();
        using var first = CreatePipeGuard(name);
        var received = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        first.ActivationRequested += (_, paths) => received.TrySetResult(paths);

        var firstRole = await first.StartAsync([]);
        using var second = CreatePipeGuard(name);
        var secondRole = await second.StartAsync(["/music/a.flac", "/music/b c.mp3"]);

        await Assert.That(firstRole).IsEqualTo(SingleInstanceRole.Primary);
        await Assert.That(secondRole).IsEqualTo(SingleInstanceRole.Secondary);
        var paths = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(string.Join("|", paths)).IsEqualTo("/music/a.flac|/music/b c.mp3");
    }

    [Test]
    public async Task Pipe_WhenTheRunningInstanceDoesNotAnswer_StartsAsPrimary()
    {
        var name = UniquePipeName();
        using var heldByAHungInstance = new Mutex(false, $@"Local\{name}");
        using var guard = new NamedPipeSingleInstanceGuard(
            name, NullLogger<NamedPipeSingleInstanceGuard>.Instance, connectTimeout: TimeSpan.FromMilliseconds(200));

        var role = await guard.StartAsync(["/music/a.flac"]);

        await Assert.That(role).IsEqualTo(SingleInstanceRole.Primary);
    }

    private static DBusSingleInstanceGuard CreateDBusGuard(string busName, string? address = null) =>
        new(busName, address ?? Address.Session, NullLogger<DBusSingleInstanceGuard>.Instance);

    private static string UniqueBusName() => $"io.github.ghorshy.SharpSelecta.Test.T{Guid.NewGuid():N}";

    [Test]
    public async Task DBus_SecondStart_ForwardsPathsToTheFirstInstance()
    {
        Skip.When(!OperatingSystem.IsLinux() || Address.Session is null, "needs a D-Bus session bus");
        var busName = UniqueBusName();
        using var first = CreateDBusGuard(busName);
        var received = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        first.ActivationRequested += (_, paths) => received.TrySetResult(paths);

        var firstRole = await first.StartAsync([]);
        using var second = CreateDBusGuard(busName);
        var secondRole = await second.StartAsync(["/music/a.flac", "/music/b c.mp3"]);

        await Assert.That(firstRole).IsEqualTo(SingleInstanceRole.Primary);
        await Assert.That(secondRole).IsEqualTo(SingleInstanceRole.Secondary);
        var paths = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(string.Join("|", paths)).IsEqualTo("/music/a.flac|/music/b c.mp3");
    }

    [Test]
    public async Task DBus_WhenTheBusIsUnreachable_StartsAsPrimary()
    {
        Skip.When(!OperatingSystem.IsLinux(), "D-Bus guard is Linux-only");
        using var guard = CreateDBusGuard(UniqueBusName(), $"unix:path=/nonexistent/{Guid.NewGuid():N}");

        var role = await guard.StartAsync(["/music/a.flac"]);

        await Assert.That(role).IsEqualTo(SingleInstanceRole.Primary);
    }
}
