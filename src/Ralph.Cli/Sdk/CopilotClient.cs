// Copilot SDK client wrapper.

using System.Threading.Channels;

using GitHub.Copilot.SDK;

using Ralph.Cli.Core;

using SdkClient = GitHub.Copilot.SDK.CopilotClient;

namespace Ralph.Cli.Sdk;

/// <summary>
/// Configuration options for the Copilot client.
/// </summary>
public sealed class ClientConfig
{
    public string Model { get; set; } = LoopConfig.DefaultModel;
    public string LogLevel { get; set; } = CopilotClient.DefaultLogLevel;
    public string WorkingDir { get; set; } = ".";
    public string SystemMessage { get; set; } = string.Empty;
    public string SystemMessageMode { get; set; } = "append";
    public TimeSpan Timeout { get; set; } = CopilotClient.DefaultTimeout;
    public bool Streaming { get; set; } = true;

    /// <summary>
    /// List of directories the AI is allowed to access. Paths outside these directories will be denied.
    /// If empty, defaults to the working directory.
    /// </summary>
    public List<string> AllowedDirectories { get; set; } = [];

    /// <summary>
    /// List of tool names to allow. If null, all tools are allowed (subject to ExcludedTools).
    /// </summary>
    public List<string>? AvailableTools { get; set; }

    /// <summary>
    /// List of tool names to exclude.
    /// </summary>
    public List<string>? ExcludedTools { get; set; }
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
/// Provides session management, event handling, directory sandboxing, and tool filtering.
/// </summary>
public sealed class CopilotClient : ICopilotClient, IAsyncDisposable
{
    public const string DefaultLogLevel = "info";
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan[] RetryBackoffs =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5)
    ];

    private readonly ClientConfig _config;
    private readonly List<string> _allowedDirectories;
    private SdkClient? _client;
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

        // Normalize and store allowed directories
        _allowedDirectories = NormalizeAllowedDirectories(_config.AllowedDirectories, _config.WorkingDir);
    }

    /// <summary>
    /// Gets the normalized list of allowed directories.
    /// </summary>
    public IReadOnlyList<string> AllowedDirectories => _allowedDirectories;

    private static List<string> NormalizeAllowedDirectories(List<string> directories, string workingDir)
    {
        var normalized = new List<string>();

        if (directories.Count == 0)
        {
            // Default to working directory
            normalized.Add(NormalizePath(workingDir));
        }
        else
        {
            foreach (var dir in directories)
            {
                normalized.Add(NormalizePath(dir));
            }
        }

        return normalized;
    }

    private static string NormalizePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        // Remove trailing separator for consistent comparison
        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// Checks if a path is within any of the allowed directories.
    /// </summary>
    public bool IsPathAllowed(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        var fullPath = NormalizePath(path);

        foreach (var allowedDir in _allowedDirectories)
        {
            // Exact match
            if (fullPath.Equals(allowedDir, StringComparison.OrdinalIgnoreCase))
                return true;

            // Check if it's a subdirectory - must start with allowed dir + separator
            var allowedDirWithSep = allowedDir + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(allowedDirWithSep, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private Task<PermissionRequestResult> HandlePermissionRequest(PermissionRequest request, PermissionInvocation invocation)
    {
        // Check if this is a shell command - extract paths from command text and possiblePaths
        if (request.Kind == "shell")
        {
            // Get paths from possiblePaths array (may be empty)
            var possiblePaths = ExtractPossiblePaths(request.ExtensionData);

            // Also extract paths from the command text itself since possiblePaths may be empty
            var commandPaths = ExtractPathsFromCommandText(request.ExtensionData);

            // Combine all paths
            var allPaths = possiblePaths.Concat(commandPaths).Distinct().ToList();

            if (!allPaths.All(IsPathAllowed))
            {
                // Deny shell commands that access paths outside allowed directories
                return Task.FromResult(new PermissionRequestResult
                {
                    Kind = "denied-by-rules"
                });
            }

            return Task.FromResult(new PermissionRequestResult
            {
                Kind = "approved"
            });
        }

        // Check if this is a read or write operation - these have path info in ExtensionData
        if (request.Kind is "read" or "write")
        {
            // Try to extract path from request's extension data
            var requestedPath = ExtractPath(request.ExtensionData);

            if (string.IsNullOrEmpty(requestedPath))
            {
                // If we can't find a path for a read/write operation, deny it
                return Task.FromResult(new PermissionRequestResult
                {
                    Kind = "denied-by-rules"
                });
            }

            if (!IsPathAllowed(requestedPath))
            {
                // Deny access to paths outside the allowed directories
                return Task.FromResult(new PermissionRequestResult
                {
                    Kind = "denied-by-rules"
                });
            }

            return Task.FromResult(new PermissionRequestResult
            {
                Kind = "approved"
            });
        }

        // For other permission request kinds, check if there's a path in ExtensionData
        var pathInRequest = ExtractPath(request.ExtensionData);
        if (!string.IsNullOrEmpty(pathInRequest) && !IsPathAllowed(pathInRequest))
        {
            return Task.FromResult(new PermissionRequestResult
            {
                Kind = "denied-by-rules"
            });
        }

        // Also check possiblePaths for other kinds
        var otherPaths = ExtractPossiblePaths(request.ExtensionData);
        foreach (var path in otherPaths)
        {
            if (!IsPathAllowed(path))
            {
                return Task.FromResult(new PermissionRequestResult
                {
                    Kind = "denied-by-rules"
                });
            }
        }

        // Approve operations that don't involve file paths or are within allowed dirs
        return Task.FromResult(new PermissionRequestResult
        {
            Kind = "approved"
        });
    }

    private static List<string> ExtractPossiblePaths(IDictionary<string, object>? data)
    {
        var paths = new List<string>();

        if (data == null)
            return paths;

        // possiblePaths is likely an array/list of paths
        if (data.TryGetValue("possiblePaths", out var possiblePathsObj) && possiblePathsObj != null)
        {
            // Handle different possible types
            if (possiblePathsObj is System.Text.Json.JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in jsonElement.EnumerateArray())
                    {
                        var path = item.GetString();
                        if (!string.IsNullOrEmpty(path))
                        {
                            paths.Add(path);
                        }
                    }
                }
                else if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var path = jsonElement.GetString();
                    if (!string.IsNullOrEmpty(path))
                    {
                        paths.Add(path);
                    }
                }
            }
            else if (possiblePathsObj is IEnumerable<object> enumerable)
            {
                foreach (var item in enumerable)
                {
                    var path = item?.ToString();
                    if (!string.IsNullOrEmpty(path))
                    {
                        paths.Add(path);
                    }
                }
            }
            else if (possiblePathsObj is string singlePath && !string.IsNullOrEmpty(singlePath))
            {
                paths.Add(singlePath);
            }
        }

        return paths;
    }

    private static string? ExtractPath(IDictionary<string, object>? data)
    {
        if (data == null)
            return null;

        // Try different path keys the SDK might use
        string[] pathKeys = ["path", "filePath", "file", "directory", "dir", "cwd"];

        foreach (var key in pathKeys)
        {
            if (data.TryGetValue(key, out var value) && value != null)
            {
                var path = value.ToString();
                if (!string.IsNullOrEmpty(path))
                    return path;
            }
        }

        return null;
    }

    private static List<string> ExtractPathsFromCommandText(IDictionary<string, object>? data)
    {
        var paths = new List<string>();

        if (data == null)
            return paths;

        // Get the full command text
        string? commandText = null;
        if (data.TryGetValue("fullCommandText", out var cmdObj) && cmdObj != null)
        {
            if (cmdObj is System.Text.Json.JsonElement jsonElement &&
                jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                commandText = jsonElement.GetString();
            }
            else
            {
                commandText = cmdObj.ToString();
            }
        }

        if (string.IsNullOrEmpty(commandText))
            return paths;

        // Use regex to find Windows-style absolute paths (e.g., D:\hello.txt, C:\Users\file.txt)
        // Match patterns like X:\ or X:/ followed by path characters
        var windowsPathRegex = new System.Text.RegularExpressions.Regex(
            @"[A-Za-z]:[\\\/][^\s""'`]*",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (System.Text.RegularExpressions.Match match in windowsPathRegex.Matches(commandText))
        {
            var path = match.Value.TrimEnd('"', '\'', '`', ',', ';');
            if (!string.IsNullOrEmpty(path))
            {
                paths.Add(path);
            }
        }

        // Also match Unix-style absolute paths starting with /
        var unixPathRegex = new System.Text.RegularExpressions.Regex(
            @"(?<=[""'\s]|^)/[^\s""'`]+",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (System.Text.RegularExpressions.Match match in unixPathRegex.Matches(commandText))
        {
            var path = match.Value.TrimEnd('"', '\'', '`', ',', ';');
            if (!string.IsNullOrEmpty(path) && path != "/")
            {
                paths.Add(path);
            }
        }

        return paths;
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

        _client = new SdkClient(options);
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
            SystemMessage = systemMessageConfig,
            AvailableTools = _config.AvailableTools,
            ExcludedTools = _config.ExcludedTools,
            OnPermissionRequest = HandlePermissionRequest
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

        // Track tool calls by ID to correlate start/complete events
        // (ToolExecutionCompleteEvent doesn't include ToolName)
        var pendingToolCalls = new Dictionary<string, string>();

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

                    case GitHub.Copilot.SDK.ToolExecutionStartEvent toolStart:
                        // Track tool name by ID for correlation with complete event
                        pendingToolCalls[toolStart.Data.ToolCallId] = toolStart.Data.ToolName;
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
                        // Look up tool name from tracked start event
                        var toolName = pendingToolCalls.TryGetValue(toolComplete.Data.ToolCallId, out var name)
                            ? name
                            : "tool";
                        pendingToolCalls.Remove(toolComplete.Data.ToolCallId);
                        writer.TryWrite(new ToolResultEvent
                        {
                            ToolCall = new ToolCall
                            {
                                Id = toolComplete.Data.ToolCallId,
                                Name = toolName,
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