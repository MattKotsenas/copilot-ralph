// Tests for system prompt.

using Ralph.Cli.Core;

namespace Ralph.Tests;

[TestClass]
public sealed class SystemPromptTests
{
    [TestMethod]
    public void Build_ReplacesPromisePlaceholder()
    {
        var result = SystemPrompt.Build("I'm done!");

        StringAssert.Contains(result, "<promise>I'm done!</promise>");
    }

    [TestMethod]
    public void Build_ContainsInstructions()
    {
        var result = SystemPrompt.Build("Task complete");

        StringAssert.Contains(result, "Ralph Loop System Instructions");
        StringAssert.Contains(result, "Completion Signal");
    }

    [TestMethod]
    public void Build_WithDifferentPhrase_ReplacesCorrectly()
    {
        var result = SystemPrompt.Build("All work finished!");

        StringAssert.Contains(result, "<promise>All work finished!</promise>");
        Assert.IsFalse(result.Contains("{{Promise}}"));
    }
}
