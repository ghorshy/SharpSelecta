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
}
