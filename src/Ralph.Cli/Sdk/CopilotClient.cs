// Copilot SDK client wrapper.

using System.Threading.Channels;

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
public sealed class CopilotClient : ICopilotClient
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
    private bool _started;
    private bool _hasSession;

    public string Model => _config.Model;

    public CopilotClient(ClientConfig? config = null)
    {
        _config = config ?? new ClientConfig();

        if (string.IsNullOrEmpty(_config.Model))
            throw new ArgumentException("Model cannot be empty");

        if (_config.Timeout <= TimeSpan.Zero)
            throw new ArgumentException("Timeout must be positive");
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
            return Task.CompletedTask;

        // In a real implementation, this would start the SDK client
        _started = true;
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (!_started)
            return Task.CompletedTask;

        _hasSession = false;
        _started = false;
        return Task.CompletedTask;
    }

    public Task CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        if (!_started)
            throw new InvalidOperationException("SDK client not started");

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
        if (!_hasSession)
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
        // In a real implementation, this would send to the SDK and stream events
        // For now, just complete the channel as a stub
        await Task.CompletedTask;
        writer.Complete();
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
}
