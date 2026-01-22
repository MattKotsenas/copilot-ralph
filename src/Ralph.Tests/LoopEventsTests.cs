// Tests for loop events.

using Ralph.Cli.Core;

namespace Ralph.Tests;

[TestClass]
public sealed class LoopEventsTests
{
    [TestMethod]
    public void ToolEventBase_Info_NoParameters_ReturnsToolName()
    {
        var toolEvent = new ToolExecutionStartEvent("echo", [], 1);
        var info = toolEvent.Info("!");

        Assert.AreEqual("! echo", info);
    }

    [TestMethod]
    public void ToolEventBase_Info_WithParameters_IncludesValues()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["path"] = "file.txt",
            ["line"] = 42
        };
        var toolEvent = new ToolExecutionStartEvent("edit", parameters, 2);
        var info = toolEvent.Info("🔧");

        StringAssert.Contains(info, "edit");
        StringAssert.Contains(info, "file.txt");
        StringAssert.Contains(info, "42");
    }

    [TestMethod]
    public void LoopStartEvent_HasConfig()
    {
        var config = new LoopConfig { Prompt = "test" };
        var loopEvent = new LoopStartEvent(config);

        Assert.AreEqual(config, loopEvent.Config);
    }

    [TestMethod]
    public void LoopCompleteEvent_HasResult()
    {
        var result = new LoopResult { State = LoopState.Complete };
        var loopEvent = new LoopCompleteEvent(result);

        Assert.AreEqual(result, loopEvent.Result);
    }

    [TestMethod]
    public void LoopFailedEvent_HasErrorAndResult()
    {
        var error = new Exception("test error");
        var result = new LoopResult { State = LoopState.Failed };
        var loopEvent = new LoopFailedEvent(error, result);

        Assert.AreEqual(error, loopEvent.Error);
        Assert.AreEqual(result, loopEvent.Result);
    }

    [TestMethod]
    public void LoopCancelledEvent_HasResult()
    {
        var result = new LoopResult { State = LoopState.Cancelled };
        var loopEvent = new LoopCancelledEvent(result);

        Assert.AreEqual(result, loopEvent.Result);
    }

    [TestMethod]
    public void IterationStartEvent_HasIterationAndMax()
    {
        var loopEvent = new IterationStartEvent(3, 10);

        Assert.AreEqual(3, loopEvent.Iteration);
        Assert.AreEqual(10, loopEvent.MaxIterations);
    }

    [TestMethod]
    public void IterationCompleteEvent_HasIterationAndDuration()
    {
        var loopEvent = new IterationCompleteEvent(2, TimeSpan.FromSeconds(5));

        Assert.AreEqual(2, loopEvent.Iteration);
        Assert.AreEqual(TimeSpan.FromSeconds(5), loopEvent.Duration);
    }

    [TestMethod]
    public void AIResponseEvent_HasTextAndIteration()
    {
        var loopEvent = new AIResponseEvent("Hello", 1);

        Assert.AreEqual("Hello", loopEvent.Text);
        Assert.AreEqual(1, loopEvent.Iteration);
    }

    [TestMethod]
    public void ToolExecutionEvent_HasAllProperties()
    {
        var parameters = new Dictionary<string, object?> { ["key"] = "value" };
        var loopEvent = new ToolExecutionEvent(
            "read_file",
            parameters,
            "content",
            null,
            TimeSpan.FromMilliseconds(100),
            2);

        Assert.AreEqual("read_file", loopEvent.ToolName);
        Assert.AreEqual(parameters, loopEvent.Parameters);
        Assert.AreEqual("content", loopEvent.Result);
        Assert.IsNull(loopEvent.Error);
        Assert.AreEqual(TimeSpan.FromMilliseconds(100), loopEvent.Duration);
        Assert.AreEqual(2, loopEvent.Iteration);
    }

    [TestMethod]
    public void PromiseDetectedEvent_HasAllProperties()
    {
        var loopEvent = new PromiseDetectedEvent("I'm done!", "ai_response", 5);

        Assert.AreEqual("I'm done!", loopEvent.Phrase);
        Assert.AreEqual("ai_response", loopEvent.Source);
        Assert.AreEqual(5, loopEvent.Iteration);
    }

    [TestMethod]
    public void ErrorEvent_HasAllProperties()
    {
        var error = new Exception("test error");
        var loopEvent = new ErrorEvent(error, 2, true);

        Assert.AreEqual(error, loopEvent.Error);
        Assert.AreEqual(2, loopEvent.Iteration);
        Assert.IsTrue(loopEvent.Recoverable);
    }
}
