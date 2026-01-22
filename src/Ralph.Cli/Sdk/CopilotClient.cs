// Copilot SDK client wrapper.

using System.Threading.Channels;
using GitHub.Copilot.SDK;

namespace Ralph.Cli.Sdk;

/// <summary>
/// Configuration options for the Copilot client.
/// </summary>
public sealed class ClientConfig
{
    public string Model { get; set; } = CopilotClient.DefaultModel;
    public string LogLevel { get; set; } = CopilotClient.DefaultLogLevel;
    public string WorkingDir { get; set; } = ".";
    public string SystemMessage { get; set; } = string.Empty;
    public string SystemMessageMode { get; set; } = "append";
    public TimeSpan Timeout { get; set; } = CopilotClient.DefaultTimeout;
    public bool Streaming { get; set; } = true;
}

/// <summary>
/// Interface for the Copilot SDK client (for testability).
/// </summary>
public interface ICopilotClient
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
    Task CreateSessionAsync(CancellationToken cancellationToken = default);
    Task DestroySessionAsync(CancellationToken cancellationToken = default);
    ChannelReader<IEvent> SendPromptAsync(string prompt, CancellationToken cancellationToken = default);
    string Model { get; }
}

/// <summary>
/// Wraps the GitHub Copilot SDK.
/// Provides session management, event handling, and tool registration.
/// </summary>
public sealed class CopilotClient : ICopilotClient, IAsyncDisposable
{
    public const string DefaultModel = "gpt-4";
    public const string DefaultLogLevel = "info";
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan[] RetryBackoffs =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5)
    ];

    private readonly ClientConfig _config;
    private GitHub.Copilot.SDK.CopilotClient? _client;
    private CopilotSession? _session;
    private bool _started;

    public string Model => _config.Model;

    public CopilotClient(ClientConfig? config = null)
    {
        _config = config ?? new ClientConfig();

        if (string.IsNullOrEmpty(_config.Model))
            throw new ArgumentException("Model cannot be empty");

        if (_config.Timeout <= TimeSpan.Zero)
            throw new ArgumentException("Timeout must be positive");
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
            return;

        var options = new CopilotClientOptions
        {
            LogLevel = _config.LogLevel,
            Cwd = _config.WorkingDir
        };

        _client = new GitHub.Copilot.SDK.CopilotClient(options);
        await _client.StartAsync();
        _started = true;
    }

    public async Task StopAsync()
    {
        if (!_started || _client == null)
            return;

        try
        {
            await _client.StopAsync();
        }
        catch
        {
            // Ignore errors during cleanup
        }
        finally
        {
            _client = null;
            _session = null;
            _started = false;
        }
    }

    public async Task CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        if (!_started || _client == null)
            throw new InvalidOperationException("SDK client not started");

        var systemMessageConfig = string.IsNullOrEmpty(_config.SystemMessage)
            ? null
            : new SystemMessageConfig
            {
                Mode = _config.SystemMessageMode.Equals("replace", StringComparison.OrdinalIgnoreCase)
                    ? SystemMessageMode.Replace
                    : SystemMessageMode.Append,
                Content = _config.SystemMessage
            };

        _session = await _client.CreateSessionAsync(new SessionConfig
        {
            Model = _config.Model,
            Streaming = _config.Streaming,
            SystemMessage = systemMessageConfig
        });
    }

    public async Task DestroySessionAsync(CancellationToken cancellationToken = default)
    {
        if (_session != null)
        {
            try
            {
                await _session.DisposeAsync();
            }
            catch
            {
                // Ignore errors during cleanup
            }
            finally
            {
                _session = null;
            }
        }
    }

    public ChannelReader<IEvent> SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (_session == null)
            throw new InvalidOperationException("No active session");

        var channel = Channel.CreateBounded<IEvent>(100);

        _ = SendPromptWithRetryAsync(prompt, channel.Writer, cancellationToken);

        return channel.Reader;
    }

    private async Task SendPromptWithRetryAsync(string prompt, ChannelWriter<IEvent> writer, CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        for (var attempt = 0; attempt <= RetryBackoffs.Length; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                writer.Complete();
                return;
            }

            if (attempt > 0)
            {
                try
                {
                    await Task.Delay(RetryBackoffs[attempt - 1], cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    writer.Complete();
                    return;
                }
            }

            try
            {
                await SendPromptOnceAsync(prompt, writer, cancellationToken);
                return;
            }
            catch (Exception ex) when (IsRetryableError(ex))
            {
                lastError = ex;
            }
            catch (Exception ex)
            {
                await writer.WriteAsync(new ErrorEvent { Error = ex }, cancellationToken);
                writer.Complete();
                return;
            }
        }

        if (lastError != null)
        {
            await writer.WriteAsync(new ErrorEvent { Error = new Exception($"Max retries exceeded: {lastError.Message}", lastError) }, cancellationToken);
        }

        writer.Complete();
    }

    private async Task SendPromptOnceAsync(string prompt, ChannelWriter<IEvent> writer, CancellationToken cancellationToken)
    {
        if (_session == null)
        {
            writer.Complete();
            return;
        }

        var done = new TaskCompletionSource();

        // Register cancellation
        await using var registration = cancellationToken.Register(() => done.TrySetCanceled());

        using var subscription = _session.On(evt =>
        {
            try
            {
                switch (evt)
                {
                    case AssistantMessageDeltaEvent delta:
                        // Streaming text chunk
                        writer.TryWrite(new TextEvent { Text = delta.Data.DeltaContent ?? "", Reasoning = false });
                        break;

                    case AssistantReasoningDeltaEvent reasoningDelta:
                        // Streaming reasoning chunk
                        writer.TryWrite(new TextEvent { Text = reasoningDelta.Data.DeltaContent ?? "", Reasoning = true });
                        break;

                    case AssistantMessageEvent msg:
                        // Final complete message (when not streaming, or as final event)
                        if (!_config.Streaming)
                        {
                            writer.TryWrite(new TextEvent { Text = msg.Data.Content ?? "", Reasoning = false });
                        }
                        break;

                    case ToolExecutionStartEvent toolStart:
                        writer.TryWrite(new ToolCallEvent
                        {
                            ToolCall = new ToolCall
                            {
                                Id = toolStart.Data.ToolCallId,
                                Name = toolStart.Data.ToolName,
                                Parameters = toolStart.Data.Arguments != null
                                    ? new Dictionary<string, object?> { ["arguments"] = toolStart.Data.Arguments }
                                    : []
                            }
                        });
                        break;

                    case ToolExecutionCompleteEvent toolComplete:
                        writer.TryWrite(new ToolResultEvent
                        {
                            ToolCall = new ToolCall
                            {
                                Id = toolComplete.Data.ToolCallId,
                                Name = "tool", // Not available in complete event
                            },
                            Result = toolComplete.Data.Result?.Content ?? "",
                            Error = toolComplete.Data.Error != null
                                ? new Exception(toolComplete.Data.Error.Message)
                                : null
                        });
                        break;

                    case SessionErrorEvent errorEvent:
                        writer.TryWrite(new ErrorEvent
                        {
                            Error = new Exception(errorEvent.Data.Message ?? "Unknown error")
                        });
                        break;

                    case SessionIdleEvent:
                        // Session finished processing
                        done.TrySetResult();
                        break;
                }
            }
            catch (Exception ex)
            {
                writer.TryWrite(new ErrorEvent { Error = ex });
            }
        });

        try
        {
            await _session.SendAsync(new MessageOptions { Prompt = prompt });
            await done.Task;
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected
        }
        finally
        {
            writer.Complete();
        }
    }

    private static bool IsRetryableError(Exception ex)
    {
        var message = ex.Message;
        return message.Contains("GOAWAY", StringComparison.OrdinalIgnoreCase)
            || message.Contains("connection reset", StringComparison.OrdinalIgnoreCase)
            || message.Contains("connection refused", StringComparison.OrdinalIgnoreCase)
            || message.Contains("connection terminated", StringComparison.OrdinalIgnoreCase)
            || message.Contains("EOF", StringComparison.OrdinalIgnoreCase)
            || message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
    }

    public async ValueTask DisposeAsync()
    {
        await DestroySessionAsync();
        await StopAsync();
    }
}
