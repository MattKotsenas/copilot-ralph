// Ralph - Iterative AI Development Loop Tool
// Entry point for the CLI application.

using ConsoleAppFramework;
using Ralph.Cli.Commands;

var app = ConsoleApp.Create();
app.Add<RootCommands>();
await app.RunAsync(args);
