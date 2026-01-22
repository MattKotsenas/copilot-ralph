// Version information for Ralph.
// Version information is set at build time using MSBuild properties.

namespace Ralph.Cli.Version;

/// <summary>
/// Contains version information for the application.
/// </summary>
public sealed record VersionInfo(
    string Version,
    string Commit,
    string BuildDate,
    string DotNetVersion
);

/// <summary>
/// Provides access to version information.
/// </summary>
public static class AppVersion
{
    /// <summary>The semantic version number.</summary>
    public static string Version { get; set; } = "dev";

    /// <summary>The git commit hash.</summary>
    public static string Commit { get; set; } = "unknown";

    /// <summary>The build timestamp.</summary>
    public static string BuildDate { get; set; } = "unknown";

    /// <summary>The .NET version used to build.</summary>
    public static string DotNetVersion { get; set; } = Environment.Version.ToString();

    /// <summary>
    /// Gets the version information.
    /// </summary>
    public static VersionInfo Get() => new(Version, Commit, BuildDate, DotNetVersion);
}
