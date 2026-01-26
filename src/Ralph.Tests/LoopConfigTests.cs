// Tests for loop configuration.

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
        Assert.AreEqual(LoopConfig.DefaultModel, config.Model);
        Assert.AreEqual(".", config.WorkingDir);
        Assert.IsFalse(config.DryRun);
    }
}