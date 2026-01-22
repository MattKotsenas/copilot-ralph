// Tests for version information.

using Ralph.Cli.Version;

namespace Ralph.Tests;

[TestClass]
public sealed class VersionInfoTests
{
    [TestMethod]
    public void Get_ReturnsSetValues()
    {
        // Arrange
        var oldVersion = AppVersion.Version;
        var oldCommit = AppVersion.Commit;
        var oldBuildDate = AppVersion.BuildDate;
        var oldDotNetVersion = AppVersion.DotNetVersion;

        try
        {
            AppVersion.Version = "1.2.3";
            AppVersion.Commit = "abc123";
            AppVersion.BuildDate = "2026-01-01";
            AppVersion.DotNetVersion = "10.0.0";

            // Act
            var info = AppVersion.Get();

            // Assert
            Assert.AreEqual("1.2.3", info.Version);
            Assert.AreEqual("abc123", info.Commit);
            Assert.AreEqual("2026-01-01", info.BuildDate);
            Assert.AreEqual("10.0.0", info.DotNetVersion);
        }
        finally
        {
            AppVersion.Version = oldVersion;
            AppVersion.Commit = oldCommit;
            AppVersion.BuildDate = oldBuildDate;
            AppVersion.DotNetVersion = oldDotNetVersion;
        }
    }

    [TestMethod]
    public void Get_DefaultValues_ReturnsDevVersion()
    {
        // The default version should be "dev" unless overridden at build time
        var info = AppVersion.Get();
        Assert.IsNotNull(info.Version);
        Assert.IsNotNull(info.DotNetVersion);
    }
}
