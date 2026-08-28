using Microsoft.Extensions.Logging.Abstractions;
using SharpSelecta.App.Services;

namespace SharpSelecta.Tests;

public class FileManagerServiceTests
{
    // The exact label is environment-dependent (which file manager, if any, is installed/detected),
    // but construction must never throw and must always produce something displayable.
    [Test]
    public async Task ActionLabel_IsNeverNullOrEmpty()
    {
        var service = new FileManagerService(NullLogger<FileManagerService>.Instance);

        await Assert.That(service.ActionLabel).IsNotNullOrEmpty();
    }

    // Exercises the real D-Bus attempt + per-executable + xdg-open fallback chain end to end -
    // must never throw, regardless of what's actually available in the environment running this.
    [Test]
    public async Task RevealInFileManagerAsync_DoesNotThrow()
    {
        var service = new FileManagerService(NullLogger<FileManagerService>.Instance);

        await service.RevealInFileManagerAsync("/tmp/sharpselecta-file-manager-service-test.mp3");
    }
}
