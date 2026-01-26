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
}