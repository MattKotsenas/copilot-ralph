// Ralph - Iterative AI Development Loop Tool
// Entry point for the CLI application.

using System.CommandLine;
using System.Text;

using Ralph.Cli.Commands;

// Ensure console can display Unicode characters (including emojis and Braille art)
Console.OutputEncoding = Encoding.UTF8;

return CommandBuilder.Build().Parse(args).Invoke();