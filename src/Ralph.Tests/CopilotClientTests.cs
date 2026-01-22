// Tests for SDK client.

using Ralph.Cli.Sdk;

namespace Ralph.Tests;

[TestClass]
public sealed class CopilotClientTests
{
    [TestMethod]
    public void Constructor_WithDefaultConfig_Succeeds()
    {
        var client = new CopilotClient();
        Assert.AreEqual(CopilotClient.DefaultModel, client.Model);
    }

    [TestMethod]
    public void Constructor_WithCustomConfig_UsesProvidedValues()
    {
        var config = new ClientConfig
        {
            Model = "gpt-test",
            WorkingDir = "/tmp"
        };

        var client = new CopilotClient(config);

        Assert.AreEqual("gpt-test", client.Model);
    }

    [TestMethod]
    public void Constructor_WithEmptyModel_ThrowsException()
    {
        var config = new ClientConfig { Model = "" };
        Assert.ThrowsExactly<ArgumentException>(() => new CopilotClient(config));
    }

    [TestMethod]
    public void Constructor_WithNegativeTimeout_ThrowsException()
    {
        var config = new ClientConfig { Timeout = TimeSpan.FromSeconds(-1) };
        Assert.ThrowsExactly<ArgumentException>(() => new CopilotClient(config));
    }

    [TestMethod]
    public async Task StartAsync_CanBeCalledMultipleTimes()
    {
        var client = new CopilotClient();

        await client.StartAsync();
        await client.StartAsync(); // Should not throw

        await client.StopAsync();
    }

    [TestMethod]
    public async Task StopAsync_WhenNotStarted_DoesNotThrow()
    {
        var client = new CopilotClient();
        await client.StopAsync(); // Should not throw
    }

    [TestMethod]
    public async Task CreateSessionAsync_WithoutStart_ThrowsException()
    {
        var client = new CopilotClient();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.CreateSessionAsync());
    }

    [TestMethod]
    public async Task CreateSessionAsync_AfterStart_Succeeds()
    {
        var client = new CopilotClient();
        await client.StartAsync();

        await client.CreateSessionAsync(); // Should not throw

        await client.StopAsync();
    }

    [TestMethod]
    public void SendPromptAsync_WithoutSession_ThrowsException()
    {
        var client = new CopilotClient();
        client.StartAsync().GetAwaiter().GetResult();

        Assert.ThrowsExactly<InvalidOperationException>(() => client.SendPromptAsync("test"));

        client.StopAsync().GetAwaiter().GetResult();
    }
}
