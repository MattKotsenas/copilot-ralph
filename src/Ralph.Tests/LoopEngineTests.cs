// Tests for loop engine.

using System.Threading.Channels;
using Ralph.Cli.Core;
using Ralph.Cli.Sdk;

namespace Ralph.Tests;

/// <summary>
/// Mock SDK client for testing.
/// </summary>
public class MockSdkClient : ICopilotClient
{
    public string Model { get; set; } = "mock-model";
    public string ResponseText { get; set; } = "Mock response";
    public bool SimulatePromise { get; set; }
    public string PromisePhrase { get; set; } = string.Empty;
    public List<ToolCall> ToolCalls { get; set; } = [];
    public Exception? StartError { get; set; }
    public Exception? CreateSessionError { get; set; }
    public Exception? SendPromptError { get; set; }
    public TimeSpan ResponseDelay { get; set; } = TimeSpan.Zero;

    private bool _started;
    private bool _hasSession;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (StartError != null)
            throw StartError;
        _started = true;
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _started = false;
        return Task.CompletedTask;
    }

    public Task CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        if (CreateSessionError != null)
            throw CreateSessionError;
        _hasSession = true;
        return Task.CompletedTask;
    }

    public Task DestroySessionAsync(CancellationToken cancellationToken = default)
    {
        _hasSession = false;
        return Task.CompletedTask;
    }

    public ChannelReader<IEvent> SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (SendPromptError != null)
            throw SendPromptError;

        var channel = Channel.CreateBounded<IEvent>(10);
        var delay = ResponseDelay;

        _ = Task.Run(async () =>
        {
            try
            {
                // Add delay if configured (for testing cancellation)
                if (delay > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }

                // Check for cancellation
                if (cancellationToken.IsCancellationRequested)
                    return;

                // Send text response
                var text = ResponseText;
                if (SimulatePromise && !string.IsNullOrEmpty(PromisePhrase))
                {
                    text = $"{text} <promise>{PromisePhrase}</promise>";
                }

                await channel.Writer.WriteAsync(new TextEvent { Text = text, Reasoning = false }, cancellationToken);

                // Send tool calls
                foreach (var tc in ToolCalls)
                {
                    await channel.Writer.WriteAsync(new ToolCallEvent { ToolCall = tc }, cancellationToken);
                    await channel.Writer.WriteAsync(new ToolResultEvent { ToolCall = tc, Result = "Mock tool result" }, cancellationToken);
                }
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);

        return channel.Reader;
    }
}

[TestClass]
public sealed class LoopEngineTests
{
    [TestMethod]
    public void NewLoopEngine_WithNullConfig_UsesDefaults()
    {
        var engine = new LoopEngine(null);

        Assert.AreEqual(LoopState.Idle, engine.State);
        Assert.IsNotNull(engine.Config);
        Assert.AreEqual("I'm special!", engine.Config.PromisePhrase);
    }

    [TestMethod]
    public void NewLoopEngine_WithCustomConfig_UsesProvidedConfig()
    {
        var config = new LoopConfig
        {
            Prompt = "Test prompt",
            MaxIterations = 5,
            PromisePhrase = "Task complete"
        };
        var mockSdk = new MockSdkClient();
        var engine = new LoopEngine(config, mockSdk);

        Assert.AreEqual(LoopState.Idle, engine.State);
        Assert.AreEqual("Test prompt", engine.Config.Prompt);
        Assert.AreEqual(5, engine.Config.MaxIterations);
        Assert.AreEqual("Task complete", engine.Config.PromisePhrase);
    }

    [TestMethod]
    public void Events_ChannelIsAvailable()
    {
        var engine = new LoopEngine(null);
        Assert.IsNotNull(engine.Events);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task Start_WithPromise_TransitionsToComplete()
    {
        var mockSdk = new MockSdkClient
        {
            SimulatePromise = true,
            PromisePhrase = "I'm done!"
        };

        var config = new LoopConfig
        {
            Prompt = "Test task",
            MaxIterations = 5,
            PromisePhrase = "I'm done!"
        };
        var engine = new LoopEngine(config, mockSdk);

        var result = await engine.StartAsync();

        Assert.AreEqual(LoopState.Complete, engine.State);
        Assert.AreEqual(LoopState.Complete, result.State);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task Start_MaxIterations_Completes()
    {
        var mockSdk = new MockSdkClient
        {
            ResponseText = "Working on it..."
        };

        var config = new LoopConfig
        {
            Prompt = "Test task",
            MaxIterations = 3,
            PromisePhrase = "never found"
        };
        var engine = new LoopEngine(config, mockSdk);

        var result = await engine.StartAsync();

        Assert.AreEqual(LoopState.Complete, engine.State);
        Assert.AreEqual(LoopState.Complete, result.State);
        Assert.AreEqual(3, result.Iterations);
    }

    [TestMethod]
    [Timeout(5000)]  // 5 second timeout
    public async Task Start_Cancellation_TransitionsToCancelled()
    {
        var mockSdk = new MockSdkClient
        {
            ResponseDelay = TimeSpan.FromMilliseconds(500)  // Slow response to allow cancellation
        };
        var config = new LoopConfig
        {
            Prompt = "Test task",
            MaxIterations = 100,
            Timeout = TimeSpan.FromSeconds(30),
            PromisePhrase = "never found"
        };
        var engine = new LoopEngine(config, mockSdk);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        var result = await engine.StartAsync(cts.Token);

        Assert.AreEqual(LoopState.Cancelled, engine.State);
        Assert.AreEqual(LoopState.Cancelled, result.State);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task Start_SdkStartError_Fails()
    {
        var mockSdk = new MockSdkClient
        {
            StartError = new Exception("SDK start failed")
        };

        var engine = new LoopEngine(null, mockSdk);
        var result = await engine.StartAsync();

        Assert.AreEqual(LoopState.Failed, result.State);
        Assert.IsNotNull(result.Error);
        StringAssert.Contains(result.Error.Message, "Failed to start SDK");
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task Start_CreateSessionError_Fails()
    {
        var mockSdk = new MockSdkClient
        {
            CreateSessionError = new Exception("Session creation failed")
        };

        var engine = new LoopEngine(null, mockSdk);
        var result = await engine.StartAsync();

        Assert.AreEqual(LoopState.Failed, result.State);
        Assert.IsNotNull(result.Error);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task Start_DryRunWithoutSdk_Completes()
    {
        var config = new LoopConfig
        {
            Prompt = "Test task",
            MaxIterations = 2,
            PromisePhrase = "never found",
            DryRun = true
        };
        var engine = new LoopEngine(config); // No SDK for dry run

        var result = await engine.StartAsync();

        Assert.AreEqual(LoopState.Complete, result.State);
        Assert.AreEqual(2, result.Iterations);
    }
}
