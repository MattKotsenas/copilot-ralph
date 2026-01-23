// Tests for SDK client.

using Ralph.Cli.Sdk;

namespace Ralph.Tests;

/// <summary>
/// Tests for ICopilotClient interface behavior.
/// Note: CopilotClient requires the Copilot CLI to be installed,
/// so these tests use mock implementations to verify interface contracts.
/// </summary>
[TestClass]
public sealed class CopilotClientTests
{
    [TestMethod]
    public void ClientConfig_HasCorrectDefaults()
    {
        var config = new ClientConfig();

        Assert.AreEqual(CopilotClient.DefaultModel, config.Model);
        Assert.AreEqual(CopilotClient.DefaultLogLevel, config.LogLevel);
        Assert.AreEqual(".", config.WorkingDir);
        Assert.AreEqual(string.Empty, config.SystemMessage);
        Assert.AreEqual("append", config.SystemMessageMode);
        Assert.AreEqual(CopilotClient.DefaultTimeout, config.Timeout);
        Assert.IsTrue(config.Streaming);
        Assert.IsNotNull(config.AllowedDirectories);
        Assert.AreEqual(0, config.AllowedDirectories.Count);
        Assert.IsNull(config.AvailableTools);
        Assert.IsNull(config.ExcludedTools);
    }

    [TestMethod]
    public void CopilotClient_Constructor_WithEmptyModel_ThrowsException()
    {
        var config = new ClientConfig { Model = "" };
        Assert.ThrowsExactly<ArgumentException>(() => new CopilotClient(config));
    }

    [TestMethod]
    public void CopilotClient_Constructor_WithNegativeTimeout_ThrowsException()
    {
        var config = new ClientConfig { Timeout = TimeSpan.FromSeconds(-1) };
        Assert.ThrowsExactly<ArgumentException>(() => new CopilotClient(config));
    }

    [TestMethod]
    public void CopilotClient_Model_ReturnsConfiguredModel()
    {
        var config = new ClientConfig { Model = "gpt-test" };
        var client = new CopilotClient(config);
        Assert.AreEqual("gpt-test", client.Model);
    }
}
