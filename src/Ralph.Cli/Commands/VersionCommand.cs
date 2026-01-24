// Version command implementation.

using System.Runtime.InteropServices;
using VersionInfo = Ralph.Cli.Version;

namespace Ralph.Cli.Commands;

/// <summary>
/// Implements the 'version' command logic.
/// </summary>
public static class VersionCommand
{
    public static void Execute(bool shortVersion)
    {
        var info = VersionInfo.AppVersion.Get();

        if (shortVersion)
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
}
