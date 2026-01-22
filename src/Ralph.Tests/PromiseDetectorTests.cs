// Tests for promise detection.

using Ralph.Cli.Core;

namespace Ralph.Tests;

[TestClass]
public sealed class PromiseDetectorTests
{
    [TestMethod]
    [DataRow("<promise>I'm done!</promise>", "I'm done!", true, DisplayName = "exact match")]
    [DataRow("<promise>IM DONE!</promise>", "I'm done!", false, DisplayName = "case sensitive - no match")]
    [DataRow("The task is complete and <promise>I'm done!</promise>", "I'm done!", true, DisplayName = "embedded in text")]
    [DataRow("Still working on it", "I'm done!", false, DisplayName = "not found")]
    [DataRow("I'm don", "I'm done!", false, DisplayName = "partial match should not match")]
    [DataRow("<promise>Task complete</promise>", "Task complete", true, DisplayName = "task complete phrase")]
    [DataRow("All work finished. <promise>task complete</promise>", "task complete", true, DisplayName = "task complete with extra text")]
    [DataRow("", "I'm done!", false, DisplayName = "empty text")]
    [DataRow("I'm done!", "", false, DisplayName = "empty promise")]
    [DataRow("   ", "I'm done!", false, DisplayName = "whitespace only text")]
    [DataRow("<promise>Im   done</promise>", "I'm done!", false, DisplayName = "promise with extra whitespace")]
    [DataRow("The task is <promise>finished</promise>.", "finished", true, DisplayName = "finished phrase")]
    [DataRow("Line 1\nLine 2\n<promise>I'm done!</promise>\nLine 4", "I'm done!", true, DisplayName = "multiline text")]
    public void DetectPromise_VariousCases(string text, string promise, bool expected)
    {
        var result = PromiseDetector.DetectPromise(text, promise);
        Assert.AreEqual(expected, result);
    }
}
