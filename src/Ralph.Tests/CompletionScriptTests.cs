// Snapshot tests for shell completion scripts using Verify.

using Ralph.Cli.Commands;

using VerifyMSTest;

namespace Ralph.Tests;

[TestClass]
public sealed class CompletionScriptTests : VerifyBase
{
    [TestMethod]
    public Task PowerShell_CompletionScript_MatchesSnapshot()
    {
        var script = CompletionScripts.GetScript("pwsh");
        return Verify(script);
    }

    [TestMethod]
    public Task Bash_CompletionScript_MatchesSnapshot()
    {
        var script = CompletionScripts.GetScript("bash");
        return Verify(script);
    }

    [TestMethod]
    public Task Zsh_CompletionScript_MatchesSnapshot()
    {
        var script = CompletionScripts.GetScript("zsh");
        return Verify(script);
    }

    [TestMethod]
    public Task Fish_CompletionScript_MatchesSnapshot()
    {
        var script = CompletionScripts.GetScript("fish");
        return Verify(script);
    }

    [TestMethod]
    public void SupportedShells_ContainsExpectedShells()
    {
        var expected = new[] { "pwsh", "bash", "zsh", "fish" };
        CollectionAssert.AreEquivalent(expected, CompletionScripts.SupportedShells);
    }

    [TestMethod]
    public void GetScript_WithInvalidShell_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CompletionScripts.GetScript("invalid"));
    }

    [TestMethod]
    public void GetScript_PowerShellAlias_Works()
    {
        var pwshScript = CompletionScripts.GetScript("pwsh");
        var powershellScript = CompletionScripts.GetScript("powershell");

        Assert.AreEqual(pwshScript, powershellScript);
    }
}