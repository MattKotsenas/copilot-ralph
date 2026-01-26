// Command builder for Ralph CLI using System.CommandLine.

using System.CommandLine;
using System.CommandLine.Parsing;

using Ralph.Cli.Core;

namespace Ralph.Cli.Commands;

/// <summary>
/// Builds the command-line interface using System.CommandLine.
/// </summary>
public static class CommandBuilder
{
    /// <summary>
    /// Builds the root command with all subcommands.
    /// </summary>
    public static RootCommand Build()
    {
        var rootCommand = new RootCommand("Ralph - An iterative AI development loop tool using GitHub Copilot");

        rootCommand.Subcommands.Add(BuildRunCommand());
        rootCommand.Subcommands.Add(BuildVersionCommand());
        rootCommand.Subcommands.Add(BuildCompletionCommand());

        return rootCommand;
    }

    private static Command BuildRunCommand()
    {
        var promptArg = new Argument<string>("prompt")
        {
            Description = "The prompt text or path to a Markdown file (.md/.markdown)"
        };

        var maxIterationsOpt = new Option<int>("--max-iterations", "-m")
        {
            Description = "Maximum loop iterations",
            DefaultValueFactory = _ => 10
        };

        var timeoutOpt = new Option<string>("--timeout", "-t")
        {
            Description = "Maximum loop runtime (e.g., 30m, 1h)",
            DefaultValueFactory = _ => "30m"
        };

        var promiseOpt = new Option<string>("--promise")
        {
            Description = "Completion promise phrase",
            DefaultValueFactory = _ => "I'm special!"
        };

        var modelOpt = new Option<string?>("--model")
        {
            Description = $"AI model to use (default: {LoopConfig.DefaultModel})"
        };

        var workingDirOpt = new Option<string>("--working-dir")
        {
            Description = "Working directory for loop execution",
            DefaultValueFactory = _ => "."
        };

        var dryRunOpt = new Option<bool>("--dry-run")
        {
            Description = "Show what would be executed without running"
        };

        var streamingOpt = new Option<bool>("--streaming")
        {
            Description = "Enable streaming responses",
            DefaultValueFactory = _ => true
        };

        var systemPromptOpt = new Option<string?>("--system-prompt")
        {
            Description = "Custom system message, can be a prompt or path to Markdown file"
        };

        var systemPromptModeOpt = new Option<string>("--system-prompt-mode")
        {
            Description = "System message mode: append or replace",
            DefaultValueFactory = _ => "append"
        };
        systemPromptModeOpt.AcceptOnlyFromAmong("append", "replace");

        var logLevelOpt = new Option<string>("--log-level")
        {
            Description = "Log level: debug, info, warn, error",
            DefaultValueFactory = _ => "info"
        };
        logLevelOpt.AcceptOnlyFromAmong("debug", "info", "warn", "error");

        var allowedDirsOpt = new Option<string[]?>("--allowed-directories")
        {
            Description = "Directories the AI is allowed to access (defaults to working directory)"
        };

        var availableToolsOpt = new Option<string?>("--available-tools")
        {
            Description = "Tools to allow (comma-separated). Defaults to common .NET dev tools if not specified."
        };

        var excludedToolsOpt = new Option<string?>("--excluded-tools")
        {
            Description = "Tools to exclude (comma-separated)"
        };

        var allowAllToolsOpt = new Option<bool>("--allow-all-tools")
        {
            Description = "Allow all tools (disables default tool filtering)"
        };

        var command = new Command("run", "Run an AI development loop with a prompt");
        command.Arguments.Add(promptArg);
        command.Options.Add(maxIterationsOpt);
        command.Options.Add(timeoutOpt);
        command.Options.Add(promiseOpt);
        command.Options.Add(modelOpt);
        command.Options.Add(workingDirOpt);
        command.Options.Add(dryRunOpt);
        command.Options.Add(streamingOpt);
        command.Options.Add(systemPromptOpt);
        command.Options.Add(systemPromptModeOpt);
        command.Options.Add(logLevelOpt);
        command.Options.Add(allowedDirsOpt);
        command.Options.Add(availableToolsOpt);
        command.Options.Add(excludedToolsOpt);
        command.Options.Add(allowAllToolsOpt);

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            return await RunCommand.ExecuteAsync(
                prompt: parseResult.GetValue(promptArg)!,
                maxIterations: parseResult.GetValue(maxIterationsOpt),
                timeout: parseResult.GetValue(timeoutOpt)!,
                promise: parseResult.GetValue(promiseOpt)!,
                model: parseResult.GetValue(modelOpt),
                workingDir: parseResult.GetValue(workingDirOpt)!,
                dryRun: parseResult.GetValue(dryRunOpt),
                streaming: parseResult.GetValue(streamingOpt),
                systemPrompt: parseResult.GetValue(systemPromptOpt),
                systemPromptMode: parseResult.GetValue(systemPromptModeOpt)!,
                logLevel: parseResult.GetValue(logLevelOpt)!,
                allowedDirectories: parseResult.GetValue(allowedDirsOpt),
                availableTools: parseResult.GetValue(availableToolsOpt),
                excludedTools: parseResult.GetValue(excludedToolsOpt),
                allowAllTools: parseResult.GetValue(allowAllToolsOpt),
                cancellationToken: ct);
        });

        return command;
    }

    private static Command BuildVersionCommand()
    {
        var shortOpt = new Option<bool>("--short")
        {
            Description = "Show only version number"
        };

        var command = new Command("version", "Show version information");
        command.Options.Add(shortOpt);

        command.SetAction((ParseResult parseResult) =>
        {
            VersionCommand.Execute(parseResult.GetValue(shortOpt));
        });

        return command;
    }

    private static Command BuildCompletionCommand()
    {
        var shellArg = new Argument<string>("shell")
        {
            Description = "Shell type to generate completion script for",
            DefaultValueFactory = _ => "pwsh"
        };
        shellArg.AcceptOnlyFromAmong(CompletionScripts.SupportedShells);

        var command = new Command("completion", "Generate shell completion script");
        command.Arguments.Add(shellArg);

        command.SetAction((ParseResult parseResult) =>
        {
            var shell = parseResult.GetValue(shellArg)!;
            Console.WriteLine(CompletionScripts.GetScript(shell));
        });

        return command;
    }
}