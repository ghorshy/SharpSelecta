using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Resources;
using SharpSelecta.App.Services;

namespace SharpSelecta.Tests;

public class FileManagerServiceTests
{
    private const string FilePath = "/music/album/track.mp3";

    private static FileManagerService CreateService(IFileManagerShell shell) =>
        new(NullLogger<FileManagerService>.Instance, shell);

    private static IFileManagerShell CreateLinuxShell(string? name, string? selectCapableExecutable, bool fileManager1Succeeds)
    {
        var shell = Substitute.For<IFileManagerShell>();
        shell.DetectLinuxFileManager().Returns((name, selectCapableExecutable));
        shell.TryShowViaFileManager1Async(FilePath).Returns(fileManager1Succeeds);
        return shell;
    }

    private static List<ProcessStartInfo> CaptureStarts(IFileManagerShell shell)
    {
        var started = new List<ProcessStartInfo>();
        shell.Start(Arg.Do<ProcessStartInfo>(started.Add));
        return started;
    }

    [Test]
    public async Task OnWindows_SelectsTheFileInExplorer()
    {
        Skip.When(!OperatingSystem.IsWindows(), "Windows reveal strategy");
        var shell = Substitute.For<IFileManagerShell>();
        var started = CaptureStarts(shell);
        var service = CreateService(shell);

        await service.RevealInFileManagerAsync(@"C:\Music\track.mp3");

        await Assert.That(service.ActionLabel).IsEqualTo(Strings.ShowInFileManager("Explorer"));
        await Assert.That(started.Single().FileName).IsEqualTo("explorer.exe");
        await Assert.That(started.Single().Arguments).IsEqualTo(@"/select,""C:\Music\track.mp3""");
    }

    [Test]
    public async Task OnMacOS_RevealsTheFileInFinder()
    {
        Skip.When(!OperatingSystem.IsMacOS(), "macOS reveal strategy");
        var shell = Substitute.For<IFileManagerShell>();
        var started = CaptureStarts(shell);
        var service = CreateService(shell);

        await service.RevealInFileManagerAsync(FilePath);

        await Assert.That(service.ActionLabel).IsEqualTo(Strings.ShowInFileManager("Finder"));
        await Assert.That(started.Single().FileName).IsEqualTo("open");
        await Assert.That(string.Join(" ", started.Single().ArgumentList)).IsEqualTo($"-R {FilePath}");
    }

    [Test]
    public async Task ActionLabel_NamesTheDetectedLinuxFileManager()
    {
        Skip.When(!OperatingSystem.IsLinux(), "Linux file manager detection");

        var service = CreateService(CreateLinuxShell("Dolphin", "dolphin", fileManager1Succeeds: false));

        await Assert.That(service.ActionLabel).IsEqualTo(Strings.ShowInFileManager("Dolphin"));
    }

    [Test]
    public async Task ActionLabel_FallsBackToGenericNameWhenNoneDetected()
    {
        Skip.When(!OperatingSystem.IsLinux(), "Linux file manager detection");

        var service = CreateService(CreateLinuxShell(null, null, fileManager1Succeeds: false));

        await Assert.That(service.ActionLabel).IsEqualTo(Strings.ShowInFileManager(Strings.GenericFileManager));
    }

    [Test]
    public async Task Reveal_OnLinux_UsesFileManager1AndLaunchesNothingWhenItSucceeds()
    {
        Skip.When(!OperatingSystem.IsLinux(), "Linux reveal strategy");
        var shell = CreateLinuxShell("Dolphin", "dolphin", fileManager1Succeeds: true);

        await CreateService(shell).RevealInFileManagerAsync(FilePath);

        await shell.Received(1).TryShowViaFileManager1Async(FilePath);
        shell.DidNotReceive().Start(Arg.Any<ProcessStartInfo>());
    }

    [Test]
    public async Task Reveal_OnLinux_FallsBackToTheSelectCapableExecutable()
    {
        Skip.When(!OperatingSystem.IsLinux(), "Linux reveal strategy");
        var shell = CreateLinuxShell("Dolphin", "dolphin", fileManager1Succeeds: false);
        var started = CaptureStarts(shell);

        await CreateService(shell).RevealInFileManagerAsync(FilePath);

        await Assert.That(started.Single().FileName).IsEqualTo("dolphin");
        await Assert.That(string.Join(" ", started.Single().ArgumentList)).IsEqualTo($"--select {FilePath}");
    }

    [Test]
    public async Task Reveal_OnLinux_OpensTheContainingFolderAsALastResort()
    {
        Skip.When(!OperatingSystem.IsLinux(), "Linux reveal strategy");
        var shell = CreateLinuxShell(null, null, fileManager1Succeeds: false);
        var started = CaptureStarts(shell);

        await CreateService(shell).RevealInFileManagerAsync(FilePath);

        await Assert.That(started.Single().FileName).IsEqualTo("xdg-open");
        await Assert.That(string.Join(" ", started.Single().ArgumentList)).IsEqualTo("/music/album");
    }

    [Test]
    public async Task Reveal_SwallowsALaunchFailure()
    {
        Skip.When(!OperatingSystem.IsLinux(), "Linux reveal strategy");
        var shell = CreateLinuxShell(null, null, fileManager1Succeeds: false);
        shell.When(s => s.Start(Arg.Any<ProcessStartInfo>())).Do(_ => throw new Win32Exception());

        await CreateService(shell).RevealInFileManagerAsync(FilePath);
    }
}
