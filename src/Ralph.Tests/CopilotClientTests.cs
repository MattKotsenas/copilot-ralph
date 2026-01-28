// Tests for SDK client.

using Ralph.Cli.Core;
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

        Assert.AreEqual(LoopConfig.DefaultModel, config.Model);
        Assert.AreEqual(CopilotClient.DefaultLogLevel, config.LogLevel);
        Assert.AreEqual(".", config.WorkingDir);
        Assert.AreEqual(string.Empty, config.SystemMessage);
        Assert.AreEqual("append", config.SystemMessageMode);
        Assert.AreEqual(CopilotClient.DefaultTimeout, config.Timeout);
        Assert.IsTrue(config.Streaming);
        Assert.IsNotNull(config.AllowedDirectories);
        Assert.IsEmpty(config.AllowedDirectories);
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

    [TestMethod]
    public void PermissionRequestResult_WithRulesEmpty_DoesNotThrow()
    {
        // This test verifies the pattern we use in HandlePermissionRequest.
        // The SDK expects Rules to be an empty list, not null.
        // If Rules is null, the JavaScript SDK throws:
        // "Cannot read properties of undefined (reading 'map')"
        var result = new GitHub.Copilot.SDK.PermissionRequestResult
        {
            Kind = "approved",
            Rules = []
        };

        Assert.AreEqual("approved", result.Kind);
        Assert.IsNotNull(result.Rules);
        Assert.IsEmpty(result.Rules);
    }

    [TestMethod]
    public void PermissionRequestResult_ApprovedPattern_HasRequiredProperties()
    {
        // Verify the "approved" pattern we use sets both Kind and Rules
        var result = new GitHub.Copilot.SDK.PermissionRequestResult
        {
            Kind = "approved",
            Rules = []
        };

        Assert.AreEqual("approved", result.Kind);
        Assert.IsNotNull(result.Rules, "Rules must not be null - SDK requires empty list");
    }

    [TestMethod]
    public void PermissionRequestResult_DeniedPattern_HasRequiredProperties()
    {
        // Verify the "denied-by-rules" pattern we use sets both Kind and Rules
        var result = new GitHub.Copilot.SDK.PermissionRequestResult
        {
            Kind = "denied-by-rules",
            Rules = []
        };

        Assert.AreEqual("denied-by-rules", result.Kind);
        Assert.IsNotNull(result.Rules, "Rules must not be null - SDK requires empty list");
    }
}