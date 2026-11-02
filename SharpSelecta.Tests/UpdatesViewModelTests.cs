using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SharpSelecta.App.Resources;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;
using SharpSelecta.Core.Updates;

namespace SharpSelecta.Tests;

public class UpdatesViewModelTests
{
    private static string CreateTempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"sharpselecta-updates-tests-{Guid.NewGuid():N}.json");

    private static IUpdateService Service(bool supported = true, string? newVersion = null)
    {
        var service = Substitute.For<IUpdateService>();
        service.IsSupported.Returns(supported);
        service.CurrentVersion.Returns("1.0.0");
        service.DownloadLatestAsync(Arg.Any<CancellationToken>()).Returns(newVersion);
        return service;
    }

    private static UpdatesViewModel Create(IUpdateService? service, string? settingsPath = null) =>
        new(new SettingsStore(settingsPath ?? CreateTempSettingsPath()), service, NullLogger.Instance);

    [Test]
    public async Task CheckAutomatically_DefaultsToTrue()
    {
        await Assert.That(Create(Service()).CheckAutomatically).IsTrue();
    }

    [Test]
    public async Task WithoutAService_IsUnsupportedAndDoesNothingOnStartup()
    {
        var viewModel = Create(null);

        await viewModel.CheckOnStartupAsync();

        await Assert.That(viewModel.IsSupported).IsFalse();
        await Assert.That(viewModel.CheckNowCommand.CanExecute(null)).IsFalse();
        await Assert.That(viewModel.StatusMessage).IsNull();
    }

    [Test]
    public async Task StartupCheck_WithANewVersion_ShowsTheUpdateBar()
    {
        var viewModel = Create(Service(newVersion: "1.1.0"));

        await viewModel.CheckOnStartupAsync();

        await Assert.That(viewModel.ReadyVersion).IsEqualTo("1.1.0");
        await Assert.That(viewModel.ShowUpdateBar).IsTrue();
        await Assert.That(viewModel.UpdateReadyMessage).IsEqualTo(Strings.UpdateReady("1.1.0"));
        await Assert.That(viewModel.CheckNowCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task StartupCheck_WhenUpToDate_SaysSo()
    {
        var viewModel = Create(Service());

        await viewModel.CheckOnStartupAsync();

        await Assert.That(viewModel.ShowUpdateBar).IsFalse();
        await Assert.That(viewModel.StatusMessage).IsEqualTo(Strings.UpToDate);
    }

    [Test]
    public async Task StartupCheck_WhenTurnedOff_DoesNotCheck()
    {
        var settingsPath = CreateTempSettingsPath();
        var service = Service(newVersion: "1.1.0");
        var first = Create(service, settingsPath);
        first.CheckAutomatically = false;
        first.ApplyUpdatesSettingsCommand.Execute(null);

        await Create(service, settingsPath).CheckOnStartupAsync();

        await service.DidNotReceive().DownloadLatestAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CheckNow_WhenTheCheckFails_ReportsIt()
    {
        var service = Service();
        service.DownloadLatestAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        var viewModel = Create(service);

        await viewModel.CheckNowCommand.ExecuteAsync(null);

        await Assert.That(viewModel.StatusMessage).IsEqualTo(Strings.UpdateCheckFailed);
        await Assert.That(viewModel.IsChecking).IsFalse();
    }

    [Test]
    public async Task Later_HidesTheBarButStillInstallsOnExit()
    {
        var service = Service(newVersion: "1.1.0");
        var viewModel = Create(service);
        await viewModel.CheckOnStartupAsync();

        viewModel.DismissUpdateBarCommand.Execute(null);
        viewModel.InstallPendingUpdateOnExit();

        await Assert.That(viewModel.ShowUpdateBar).IsFalse();
        service.Received(1).UpdateOnExit();
    }

    [Test]
    public async Task InstallOnExit_WithNothingDownloaded_DoesNothing()
    {
        var service = Service();

        Create(service).InstallPendingUpdateOnExit();

        service.DidNotReceive().UpdateOnExit();
        await Task.CompletedTask;
    }

    [Test]
    public async Task RestartNow_LetsTheAppSaveStateBeforeTheUpdaterTakesOver()
    {
        var service = Service(newVersion: "1.1.0");
        var viewModel = Create(service);
        await viewModel.CheckOnStartupAsync();
        var order = new List<string>();
        viewModel.RestartingToUpdate += (_, _) => order.Add("save");
        service.When(s => s.RestartToUpdate()).Do(_ => order.Add("restart"));

        viewModel.RestartToUpdateCommand.Execute(null);

        await Assert.That(order).IsEquivalentTo(["save", "restart"]);
    }
}
