// Ralph - Iterative AI Development Loop Tool
// Entry point for the CLI application.

using System.CommandLine;
using Ralph.Cli.Commands;

return CommandBuilder.Build().Parse(args).Invoke();
