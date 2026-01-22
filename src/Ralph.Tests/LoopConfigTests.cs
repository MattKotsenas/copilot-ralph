// Tests for loop configuration and state.

using Ralph.Cli.Core;

namespace Ralph.Tests;

[TestClass]
public sealed class LoopConfigTests
{
    [TestMethod]
    public void Default_HasExpectedValues()
    {
        var config = LoopConfig.Default;

        Assert.AreEqual(10, config.MaxIterations);
        Assert.AreEqual(TimeSpan.FromMinutes(30), config.Timeout);
        Assert.AreEqual("I'm special!", config.PromisePhrase);
        Assert.AreEqual("gpt-4", config.Model);
        Assert.AreEqual(".", config.WorkingDir);
        Assert.IsFalse(config.DryRun);
    }
}

[TestClass]
public sealed class LoopStateTests
{
    [TestMethod]
    [DataRow(LoopState.Idle, "Idle")]
    [DataRow(LoopState.Running, "Running")]
    [DataRow(LoopState.Complete, "Complete")]
    [DataRow(LoopState.Failed, "Failed")]
    [DataRow(LoopState.Cancelled, "Cancelled")]
    public void LoopState_ToString_ReturnsExpected(LoopState state, string expected)
    {
        Assert.AreEqual(expected, state.ToString());
    }
}
