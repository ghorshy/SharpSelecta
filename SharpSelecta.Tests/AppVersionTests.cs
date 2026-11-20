using SharpSelecta.App;

namespace SharpSelecta.Tests;

public class AppVersionTests
{
    [Test]
    [Arguments("1.0.0-rc.1+5712d41a0c9e3f7b2d4e6a8c1b3d5f7e9a0b2c4d", "1.0.0-rc.1 (5712d41)")]
    [Arguments("1.0.0+abc", "1.0.0 (abc)")]
    [Arguments("1.0.0+", "1.0.0")]
    [Arguments("1.0.0", "1.0.0")]
    public async Task Format_ShortensTheCommitAfterTheVersion(string informationalVersion, string expected)
    {
        await Assert.That(AppVersion.Format(informationalVersion)).IsEqualTo(expected);
    }
}
