// Version information for Ralph.
// Version information is provided by Nerdbank.GitVersioning via ThisAssembly.

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
/// Provides access to version information using Nerdbank.GitVersioning.
/// </summary>
public static class AppVersion
{
    /// <summary>The semantic version number.</summary>
    public static string Version => ThisAssembly.AssemblyInformationalVersion;

    /// <summary>The git commit hash.</summary>
    public static string Commit => ThisAssembly.GitCommitId;

    /// <summary>The git commit date.</summary>
    public static string BuildDate => ThisAssembly.GitCommitDate.ToString("yyyy-MM-dd");

    /// <summary>The .NET version used at runtime.</summary>
    public static string DotNetVersion => Environment.Version.ToString();

    /// <summary>
    /// Gets the version information.
    /// </summary>
    public static VersionInfo Get() => new(Version, Commit, BuildDate, DotNetVersion);
}
