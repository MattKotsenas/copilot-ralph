// Loop engine implementation.

using System.Diagnostics;
using System.Threading.Channels;
using Ralph.Cli.Sdk;

namespace Ralph.Cli.Core;

/// <summary>
/// Custom exceptions for loop engine errors.
/// </summary>
public class LoopCancelledException : Exception
{
    public LoopCancelledException() : base("Loop cancelled") { }
}

public class LoopTimeoutException : Exception
{
    public LoopTimeoutException() : base("Loop timeout exceeded") { }
}

public class MaxIterationsException : Exception
{
    public MaxIterationsException() : base("Maximum iterations reached") { }
}

/// <summary>
/// Manages the execution of AI development loops.
/// Coordinates with the Copilot SDK, detects completion promises,
/// and handles state transitions.
/// </summary>
public sealed class LoopEngine
{
    private const int EventChannelBufferSize = 100;

    private readonly LoopConfig _config;
    private readonly ICopilotClient? _sdk;
    private readonly Channel<ILoopEvent> _events;
    private readonly object _lock = new();

    private LoopState _state = LoopState.Idle;
    private int _iteration;
    private DateTime _startTime;
    private bool _eventsClosed;
    private CancellationTokenSource? _cts;

    public LoopEngine(LoopConfig? config, ICopilotClient? sdk = null)
    {
        _config = config ?? LoopConfig.Default;
        _sdk = sdk;
        _events = Channel.CreateBounded<ILoopEvent>(EventChannelBufferSize);
    }

    /// <summary>
    /// Gets the current loop state.
    /// </summary>
    public LoopState State
    {
        get { lock (_lock) return _state; }
    }

    /// <summary>
    /// Gets the current iteration number (1-based).
    /// </summary>
    public int Iteration
    {
        get { lock (_lock) return _iteration; }
    }

    /// <summary>
    /// Gets the loop configuration.
    /// </summary>
    public LoopConfig Config => _config;

    /// <summary>
    /// Gets a channel reader for receiving loop events.
    /// </summary>
    public ChannelReader<ILoopEvent> Events => _events.Reader;

    /// <summary>
    /// Starts the loop execution.
    /// </summary>
    public async Task<LoopResult> StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_state != LoopState.Idle)
                throw new InvalidOperationException("Loop already running");

            _state = LoopState.Running;
            _startTime = DateTime.UtcNow;
            _iteration = 0;
        }

        // Set up cancellation with timeout
        _cts = _config.Timeout > TimeSpan.Zero
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (_config.Timeout > TimeSpan.Zero)
            _cts.CancelAfter(_config.Timeout);

        try
        {
            await EmitAsync(new LoopStartEvent(_config));

            // Initialize SDK if provided
            if (_sdk != null)
            {
                try
                {
                    await _sdk.StartAsync(_cts.Token);
                    await _sdk.CreateSessionAsync(_cts.Token);
                }
                catch (Exception ex)
                {
                    return await FailAsync(new Exception($"Failed to start SDK: {ex.Message}", ex));
                }
            }

            // Run the main loop
            var result = await RunLoopAsync();

            // Clean up SDK
            if (_sdk != null)
            {
                if (result.State == LoopState.Cancelled)
                {
                    // Background cleanup on cancellation
                    _ = Task.Run(async () =>
                    {
                        using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                        try
                        {
                            await _sdk.DestroySessionAsync(cleanupCts.Token);
                            await _sdk.StopAsync();
                        }
                        catch { /* Ignore cleanup errors */ }
                    });
                }
                else
                {
                    using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    try
                    {
                        await _sdk.DestroySessionAsync(cleanupCts.Token);
                        await _sdk.StopAsync();
                    }
                    catch { /* Ignore cleanup errors */ }
                }
            }

            return result;
        }
        finally
        {
            lock (_lock)
            {
                _eventsClosed = true;
            }
            _events.Writer.Complete();
        }
    }

    private async Task<LoopResult> RunLoopAsync()
    {
        while (true)
        {
            var (result, shouldStop) = await PreIterationCheckAsync();
            if (shouldStop)
                return result!;

            try
            {
                await ExecuteIterationAsync();
            }
            catch (OperationCanceledException)
            {
                if (_cts?.Token.IsCancellationRequested == true && _config.Timeout > TimeSpan.Zero && DateTime.UtcNow - _startTime >= _config.Timeout)
                    return await FailAsync(new LoopTimeoutException());

                return await CancelledAsync();
            }
            catch (Exception ex)
            {
                return await FailAsync(new Exception($"Iteration {_iteration} failed: {ex.Message}", ex));
            }
        }
    }

    private async Task<(LoopResult?, bool)> PreIterationCheckAsync()
    {
        if (_cts?.Token.IsCancellationRequested == true)
        {
            if (_config.Timeout > TimeSpan.Zero && DateTime.UtcNow - _startTime >= _config.Timeout)
                return (await FailAsync(new LoopTimeoutException()), true);

            return (await CancelledAsync(), true);
        }

        lock (_lock)
        {
            if (_state == LoopState.Cancelled)
                return (null, false);
        }

        if (_config.Timeout > TimeSpan.Zero && DateTime.UtcNow - _startTime > _config.Timeout)
            return (await FailAsync(new LoopTimeoutException()), true);

        // Check if max iterations have been reached BEFORE starting a new one
        if (_config.MaxIterations > 0 && _iteration >= _config.MaxIterations)
            return (await CompleteAsync(), true);

        return (null, false);
    }

    private async Task ExecuteIterationAsync()
    {
        int iteration;
        lock (_lock)
        {
            _iteration++;
            iteration = _iteration;
        }

        var sw = Stopwatch.StartNew();

        await EmitAsync(new IterationStartEvent(iteration, _config.MaxIterations));

        var prompt = BuildIterationPrompt(iteration);

        if (_sdk != null)
        {
            var events = _sdk.SendPromptAsync(prompt, _cts!.Token);

            await foreach (var sdkEvent in events.ReadAllAsync(_cts.Token))
            {
                switch (sdkEvent)
                {
                    case TextEvent textEvent:
                        await EmitAsync(new AIResponseEvent(textEvent.Text, iteration));

                        if (!textEvent.Reasoning && PromiseDetector.DetectPromise(textEvent.Text, _config.PromisePhrase))
                        {
                            await EmitAsync(new PromiseDetectedEvent(_config.PromisePhrase, "ai_response", iteration));
                        }
                        break;

                    case ToolCallEvent toolCallEvent:
                        await EmitAsync(new ToolExecutionStartEvent(
                            toolCallEvent.ToolCall.Name,
                            toolCallEvent.ToolCall.Parameters,
                            iteration));
                        break;

                    case ToolResultEvent toolResultEvent:
                        await EmitAsync(new ToolExecutionEvent(
                            toolResultEvent.ToolCall.Name,
                            toolResultEvent.ToolCall.Parameters,
                            toolResultEvent.Result,
                            toolResultEvent.Error,
                            TimeSpan.Zero,
                            iteration));
                        break;

                    case Sdk.ErrorEvent errorEvent:
                        await EmitAsync(new Core.ErrorEvent(errorEvent.Error, iteration, true));
                        break;
                }
            }
        }

        sw.Stop();
        await EmitAsync(new IterationCompleteEvent(iteration, sw.Elapsed));
    }

    private string BuildIterationPrompt(int iteration)
    {
        return $"[Iteration {iteration}/{_config.MaxIterations}]\n\n{_config.Prompt}";
    }

    private async Task<LoopResult> CompleteAsync()
    {
        LoopResult result;
        lock (_lock)
        {
            _state = LoopState.Complete;
            result = BuildResult();
            result.State = LoopState.Complete;
        }

        await EmitAsync(new LoopCompleteEvent(result));
        return result;
    }

    private async Task<LoopResult> FailAsync(Exception error)
    {
        LoopResult result;
        lock (_lock)
        {
            _state = LoopState.Failed;
            result = BuildResult();
            result.State = LoopState.Failed;
            result.Error = error;
        }

        await EmitAsync(new LoopFailedEvent(error, result));
        return result;
    }

    private async Task<LoopResult> CancelledAsync()
    {
        LoopResult result;
        lock (_lock)
        {
            _state = LoopState.Cancelled;
            result = BuildResult();
            result.State = LoopState.Cancelled;
            result.Error = new LoopCancelledException();
        }

        await EmitAsync(new LoopCancelledEvent(result));
        return result;
    }

    private LoopResult BuildResult()
    {
        return new LoopResult
        {
            State = _state,
            Iterations = _iteration,
            Duration = DateTime.UtcNow - _startTime
        };
    }

    private async Task EmitAsync(ILoopEvent loopEvent)
    {
        lock (_lock)
        {
            if (_eventsClosed)
                return;
        }

        try
        {
            await _events.Writer.WriteAsync(loopEvent);
        }
        catch (ChannelClosedException)
        {
            // Channel was closed, ignore
        }
    }
}
