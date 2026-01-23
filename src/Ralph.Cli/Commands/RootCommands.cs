// CLI commands for Ralph using ConsoleAppFramework.

using System.Runtime.InteropServices;
using ConsoleAppFramework;
using Ralph.Cli.Core;
using Ralph.Cli.Sdk;
using Ralph.Cli.Styles;
using Spectre.Console;
using VersionInfo = Ralph.Cli.Version;

namespace Ralph.Cli.Commands;

/// <summary>
/// Root commands for the Ralph CLI.
/// </summary>
public class RootCommands
{
    // Exit codes per spec
    private const int ExitSuccess = 0;
    private const int ExitFailed = 1;
    private const int ExitCancelled = 2;
    private const int ExitTimeout = 3;
    private const int ExitMaxIterations = 4;

    /// <summary>
    /// Run an AI development loop with a prompt.
    /// </summary>
    /// <param name="prompt">The prompt text or path to a Markdown file (.md/.markdown)</param>
    /// <param name="maxIterations">-m, Maximum loop iterations</param>
    /// <param name="timeout">-t, Maximum loop runtime (e.g., 30m, 1h)</param>
    /// <param name="promise">Completion promise phrase</param>
    /// <param name="model">AI model to use</param>
    /// <param name="workingDir">Working directory for loop execution</param>
    /// <param name="dryRun">Show what would be executed without running</param>
    /// <param name="streaming">Enable streaming responses</param>
    /// <param name="systemPrompt">Custom system message, can be a prompt or path to Markdown file</param>
    /// <param name="systemPromptMode">System message mode: append or replace</param>
    /// <param name="logLevel">Log level: debug, info, warn, error</param>
    /// <param name="allowedDirectories">Directories the AI is allowed to access (defaults to working directory)</param>
    /// <param name="availableTools">Tools to allow (comma-separated). Defaults to common .NET dev tools if not specified.</param>
    /// <param name="excludedTools">Tools to exclude (comma-separated)</param>
    /// <param name="allowAllTools">Allow all tools (disables default tool filtering)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [Command("run")]
    public async Task<int> Run(
        [Argument] string prompt,
        int maxIterations = 10,
        string timeout = "30m",
        string promise = "I'm special!",
        string model = "gpt-4",
        string workingDir = ".",
        bool dryRun = false,
        bool streaming = true,
        string? systemPrompt = null,
        string systemPromptMode = "append",
        string logLevel = "info",
        string[]? allowedDirectories = null,
        string? availableTools = null,
        string? excludedTools = null,
        bool allowAllTools = false,
        CancellationToken cancellationToken = default)
    {
        // Resolve prompt from argument (could be text or file path)
        string resolvedPrompt;
        try
        {
            resolvedPrompt = ResolvePrompt(prompt);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(ConsoleStyles.ErrorText($"Error: {ex.Message}"));
            return ExitFailed;
        }

        if (string.IsNullOrWhiteSpace(resolvedPrompt))
        {
            AnsiConsole.MarkupLine(ConsoleStyles.ErrorText("Error: prompt is required"));
            return ExitFailed;
        }

        // Parse timeout
        if (!TryParseTimeout(timeout, out var timeoutDuration))
        {
            AnsiConsole.MarkupLine(ConsoleStyles.ErrorText($"Error: invalid timeout format: {timeout}"));
            return ExitFailed;
        }

        // Parse tool lists
        var parsedAvailableTools = ParseToolList(availableTools, allowAllTools);
        var parsedExcludedTools = ParseToolList(excludedTools, false);

        // Build loop configuration
        var loopConfig = new LoopConfig
        {
            Prompt = resolvedPrompt,
            MaxIterations = maxIterations,
            Timeout = timeoutDuration,
            PromisePhrase = promise,
            Model = model,
            WorkingDir = workingDir,
            DryRun = dryRun
        };

        // Validate configuration
        var validationError = ValidateConfig(loopConfig);
        if (validationError != null)
        {
            AnsiConsole.MarkupLine(ConsoleStyles.ErrorText($"Error: {validationError}"));
            return ExitFailed;
        }

        // Validate settings
        if (systemPromptMode != "append" && systemPromptMode != "replace")
        {
            AnsiConsole.MarkupLine(ConsoleStyles.ErrorText($"Error: invalid system-prompt-mode: {systemPromptMode} (must be append or replace)"));
            return ExitFailed;
        }

        // Handle dry run
        if (dryRun)
        {
            PrintDryRun(loopConfig, allowedDirectories, parsedAvailableTools, parsedExcludedTools);
            return ExitSuccess;
        }

        // Print configuration
        PrintLoopConfig(loopConfig, allowedDirectories, parsedAvailableTools, parsedExcludedTools);

        // Create SDK client
        ICopilotClient sdkClient;
        try
        {
            sdkClient = CreateSdkClient(loopConfig, streaming, logLevel, systemPrompt, systemPromptMode,
                allowedDirectories, parsedAvailableTools, parsedExcludedTools);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(ConsoleStyles.ErrorText($"Failed to create SDK client: {ex.Message}"));
            return ExitFailed;
        }

        // Create loop engine
        var engine = new LoopEngine(loopConfig, sdkClient);

        // Set up signal handling for graceful shutdown
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            AnsiConsole.MarkupLine(ConsoleStyles.WarningText("\n⚠ Received interrupt signal, cancelling loop..."));
            cts.Cancel();
        };

        var startTime = DateTime.UtcNow;

        // Start event listener in background
        var eventTask = DisplayEventsAsync(engine.Events, loopConfig, cts.Token);

        // Run the loop
        LoopResult? result = null;
        try
        {
            result = await engine.StartAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            result = new LoopResult
            {
                State = LoopState.Cancelled,
                Iterations = engine.Iteration,
                Duration = DateTime.UtcNow - startTime,
                Error = new LoopCancelledException()
            };
        }

        // Wait for events to finish (with timeout)
        try
        {
            await eventTask.WaitAsync(TimeSpan.FromSeconds(1));
        }
        catch (TimeoutException)
        {
            // Ignore timeout waiting for events
        }

        // Print summary
        if (result != null)
        {
            PrintSummary(result, startTime);
        }

        // Return appropriate exit code
        return result?.State switch
        {
            LoopState.Complete => ExitSuccess,
            LoopState.Cancelled => ExitCancelled,
            LoopState.Failed when result.Error is LoopTimeoutException => ExitTimeout,
            LoopState.Failed when result.Error is MaxIterationsException => ExitMaxIterations,
            _ => ExitFailed
        };
    }

    /// <summary>
    /// Show version information.
    /// </summary>
    /// <param name="short">Show only version number</param>
    [Command("version")]
    public void ShowVersion(bool @short = false)
    {
        var info = VersionInfo.AppVersion.Get();

        if (@short)
        {
            Console.WriteLine(info.Version);
            return;
        }

        Console.WriteLine($"Ralph v{info.Version}");
        Console.WriteLine($"Commit: {info.Commit}");
        Console.WriteLine($"Built: {info.BuildDate}");
        Console.WriteLine($".NET: {info.DotNetVersion}");
        Console.WriteLine($"Platform: {RuntimeInformation.OSDescription}");
    }

    private static string ResolvePrompt(string prompt)
    {
        if (string.IsNullOrEmpty(prompt))
            throw new ArgumentException("No prompt provided");

        // Check if it's a file path
        if (!File.Exists(prompt))
            return prompt;

        var ext = Path.GetExtension(prompt).ToLowerInvariant();
        if (ext is not ".md" and not ".markdown")
            throw new ArgumentException($"File {prompt} must be a Markdown file with extension .md or .markdown");

        return File.ReadAllText(prompt);
    }

    private static bool TryParseTimeout(string timeout, out TimeSpan result)
    {
        result = TimeSpan.Zero;

        if (string.IsNullOrEmpty(timeout))
            return false;

        // Handle formats like "30m", "1h", "90s"
        if (timeout.EndsWith('m') && int.TryParse(timeout[..^1], out var minutes))
        {
            result = TimeSpan.FromMinutes(minutes);
            return true;
        }

        if (timeout.EndsWith('h') && int.TryParse(timeout[..^1], out var hours))
        {
            result = TimeSpan.FromHours(hours);
            return true;
        }

        if (timeout.EndsWith('s') && int.TryParse(timeout[..^1], out var seconds))
        {
            result = TimeSpan.FromSeconds(seconds);
            return true;
        }

        // Try parsing as TimeSpan directly
        return TimeSpan.TryParse(timeout, out result);
    }

    private static string? ValidateConfig(LoopConfig config)
    {
        if (string.IsNullOrEmpty(config.Prompt))
            return "prompt cannot be empty";

        if (config.MaxIterations <= 0)
            return $"max-iterations must be positive (got: {config.MaxIterations})";

        if (config.Timeout <= TimeSpan.Zero)
            return $"timeout must be positive (got: {config.Timeout})";

        return null;
    }

    private static void PrintDryRun(LoopConfig config, string[]? allowedDirectories, List<string>? availableTools, List<string>? excludedTools)
    {
        AnsiConsole.MarkupLine(ConsoleStyles.Title("🔍 Dry Run - Configuration Preview"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"{ConsoleStyles.InfoText("  Prompt:            ")}{config.Prompt}");
        AnsiConsole.MarkupLine($"{ConsoleStyles.InfoText("  Model:             ")}{config.Model}");
        AnsiConsole.MarkupLine($"{ConsoleStyles.InfoText("  Max iterations:    ")}{config.MaxIterations}");
        AnsiConsole.MarkupLine($"{ConsoleStyles.InfoText("  Timeout:           ")}{config.Timeout}");
        AnsiConsole.MarkupLine($"{ConsoleStyles.InfoText("  Promise phrase:    ")}{config.PromisePhrase}");
        AnsiConsole.MarkupLine($"{ConsoleStyles.InfoText("  Working directory: ")}{config.WorkingDir}");

        var dirs = allowedDirectories?.Length > 0 ? string.Join(", ", allowedDirectories) : config.WorkingDir;
        AnsiConsole.MarkupLine($"{ConsoleStyles.InfoText("  Allowed dirs:      ")}{dirs}");

        if (availableTools != null)
        {
            AnsiConsole.MarkupLine($"{ConsoleStyles.InfoText("  Available tools:   ")}{string.Join(", ", availableTools)}");
        }
        else
        {
            AnsiConsole.MarkupLine($"{ConsoleStyles.InfoText("  Available tools:   ")}(all)");
        }

        if (excludedTools is { Count: > 0 })
        {
            AnsiConsole.MarkupLine($"{ConsoleStyles.InfoText("  Excluded tools:    ")}{string.Join(", ", excludedTools)}");
        }

        AnsiConsole.WriteLine();
    }

    private static void PrintLoopConfig(LoopConfig config, string[]? allowedDirectories, List<string>? availableTools, List<string>? excludedTools)
    {
        // Print Ralph ASCII art
        AnsiConsole.MarkupLine(ConsoleStyles.InfoText(RalphArt.RalphWiggum));
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine(ConsoleStyles.Title("▶ Starting Ralph Loop"));
        AnsiConsole.MarkupLine($"{ConsoleStyles.WarningText("Prompt:         ")}{Markup.Escape(config.Prompt)}");
        AnsiConsole.MarkupLine($"{ConsoleStyles.WarningText("Model:          ")}{config.Model}");
        AnsiConsole.MarkupLine($"{ConsoleStyles.WarningText("Max iterations: ")}{config.MaxIterations}");
        AnsiConsole.MarkupLine($"{ConsoleStyles.WarningText("Timeout:        ")}{config.Timeout}");
        AnsiConsole.MarkupLine($"{ConsoleStyles.WarningText("Working dir:    ")}{config.WorkingDir}");

        var dirs = allowedDirectories?.Length > 0 ? string.Join(", ", allowedDirectories) : config.WorkingDir;
        AnsiConsole.MarkupLine($"{ConsoleStyles.WarningText("Allowed dirs:   ")}{dirs}");

        if (availableTools != null)
        {
            AnsiConsole.MarkupLine($"{ConsoleStyles.WarningText("Tools:          ")}{string.Join(", ", availableTools)}");
        }
    }

    private static async Task DisplayEventsAsync(System.Threading.Channels.ChannelReader<ILoopEvent> events, LoopConfig config, CancellationToken cancellationToken)
    {
        var needsNewline = false;

        try
        {
            await foreach (var loopEvent in events.ReadAllAsync(cancellationToken))
            {
                switch (loopEvent)
                {
                    case LoopStartEvent:
                        AnsiConsole.WriteLine();
                        AnsiConsole.MarkupLine(ConsoleStyles.Title("▶ Loop started"));
                        break;

                    case IterationStartEvent e:
                        AnsiConsole.WriteLine();
                        AnsiConsole.MarkupLine(ConsoleStyles.SubTitle($"━━━ Iteration {e.Iteration}/{config.MaxIterations} ━━━"));
                        AnsiConsole.WriteLine();
                        break;

                    case AIResponseEvent e:
                        Console.Write(e.Text);
                        needsNewline = true;
                        break;

                    case ToolExecutionStartEvent e:
                        if (needsNewline)
                        {
                            AnsiConsole.WriteLine();
                            needsNewline = false;
                        }
                        AnsiConsole.MarkupLine(ConsoleStyles.InfoText(e.Info("🛠️")));
                        break;

                    case ToolExecutionEvent e:
                        if (e.Error != null)
                        {
                            AnsiConsole.MarkupLine($"{e.Info("❌")} {ConsoleStyles.ErrorText($"({e.Error.Message})")}");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine(ConsoleStyles.SuccessText(e.Info("✔️")));
                        }
                        break;

                    case IterationCompleteEvent e:
                        if (needsNewline)
                        {
                            AnsiConsole.WriteLine();
                            needsNewline = false;
                        }
                        AnsiConsole.MarkupLine(ConsoleStyles.InfoText($"✓ Iteration {e.Iteration} complete"));
                        break;

                    case PromiseDetectedEvent e:
                        if (needsNewline)
                        {
                            AnsiConsole.WriteLine();
                            needsNewline = false;
                        }
                        AnsiConsole.MarkupLine(ConsoleStyles.SuccessText($"🎉 Promise detected: \"{e.Phrase}\""));
                        break;

                    case Core.ErrorEvent e:
                        if (needsNewline)
                        {
                            AnsiConsole.WriteLine();
                            needsNewline = false;
                        }
                        AnsiConsole.MarkupLine(ConsoleStyles.ErrorText($"✗ Error: {e.Error.Message}"));
                        break;

                    case LoopCompleteEvent:
                    case LoopFailedEvent:
                        return;

                    case LoopCancelledEvent:
                        if (needsNewline)
                        {
                            AnsiConsole.WriteLine();
                        }
                        AnsiConsole.MarkupLine(ConsoleStyles.WarningText("⚠ Loop cancelled"));
                        return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
        }
    }

    private static void PrintSummary(LoopResult result, DateTime startTime)
    {
        var duration = DateTime.UtcNow - startTime;

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(ConsoleStyles.Title("📊 Loop Summary"));

        var status = result.State switch
        {
            LoopState.Complete => ConsoleStyles.SuccessText("✓ Complete"),
            LoopState.Failed => ConsoleStyles.ErrorText("✗ Failed"),
            LoopState.Cancelled => ConsoleStyles.WarningText("⚠ Cancelled"),
            _ => result.State.ToString()
        };

        AnsiConsole.MarkupLine($"{ConsoleStyles.InfoText("Status:     ")}{status}");
        AnsiConsole.MarkupLine($"{ConsoleStyles.InfoText("Iterations: ")}{result.Iterations}");
        AnsiConsole.MarkupLine($"{ConsoleStyles.InfoText("Duration:   ")}{duration:hh\\:mm\\:ss}");

        if (result.Error != null)
        {
            AnsiConsole.MarkupLine($"{ConsoleStyles.ErrorText("Error:      ")}{result.Error.Message}");
        }

        AnsiConsole.WriteLine();
    }

    private static List<string>? ParseToolList(string? toolList, bool returnNullIfEmpty)
    {
        if (string.IsNullOrWhiteSpace(toolList))
        {
            // Return null if empty - the SDK treats null as "use defaults" 
            // but an empty list as "none allowed"
            return returnNullIfEmpty ? null : null;
        }

        return toolList
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static ICopilotClient CreateSdkClient(
        LoopConfig loopConfig,
        bool streaming,
        string logLevel,
        string? systemPrompt,
        string systemPromptMode,
        string[]? allowedDirectories,
        List<string>? availableTools,
        List<string>? excludedTools)
    {
        // Build system prompt from template
        var systemMessage = SystemPrompt.Build(loopConfig.PromisePhrase);

        // Override if user specified custom one
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            systemMessage = ResolvePrompt(systemPrompt);
        }

        // availableTools is null when user wants all tools (--allow-all-tools)
        // Pass null to SDK to allow all tools
        var config = new ClientConfig
        {
            Model = loopConfig.Model,
            WorkingDir = loopConfig.WorkingDir,
            Timeout = loopConfig.Timeout,
            Streaming = streaming,
            LogLevel = logLevel,
            SystemMessage = systemMessage,
            SystemMessageMode = systemPromptMode,
            AllowedDirectories = allowedDirectories?.ToList() ?? [],
            AvailableTools = availableTools,
            ExcludedTools = excludedTools
        };

        return new CopilotClient(config);
    }
}
