// Tests for version information.

using Ralph.Cli.Version;

namespace Ralph.Tests;

[TestClass]
public sealed class VersionInfoTests
{
    [TestMethod]
    public void Get_ReturnsVersionInfo()
    {
        // Act
        var info = AppVersion.Get();

        // Assert - values come from ThisAssembly via nbgv
        Assert.IsNotNull(info.Version);
        Assert.IsNotNull(info.Commit);
        Assert.IsNotNull(info.BuildDate);
        Assert.IsNotNull(info.DotNetVersion);
    }

    [TestMethod]
    public void Version_IsNotEmpty()
    {
        Assert.IsFalse(string.IsNullOrEmpty(AppVersion.Version));
    }

    [TestMethod]
    public void Commit_IsNotEmpty()
    {
        Assert.IsFalse(string.IsNullOrEmpty(AppVersion.Commit));
    }

    [TestMethod]
    public void BuildDate_IsValidDate()
    {
        var buildDate = AppVersion.BuildDate;
        Assert.IsTrue(DateTime.TryParse(buildDate, out _), $"BuildDate '{buildDate}' is not a valid date");
    }

    [TestMethod]
    public void DotNetVersion_MatchesRuntime()
    {
        Assert.AreEqual(Environment.Version.ToString(), AppVersion.DotNetVersion);
    }
}