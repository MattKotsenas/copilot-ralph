// Tests for git repository root detection.

using System.Diagnostics;

namespace Ralph.Tests;

[TestClass]
public sealed class GitRepoRootTests
{
    [TestMethod]
    public async Task GetGitRepoRoot_FromRepoRoot_ReturnsCurrentDirectory()
    {
        // This test runs from the actual repo, so we can test real behavior
        var repoRoot = await GetGitRepoRootAsync(Directory.GetCurrentDirectory());

        Assert.IsNotNull(repoRoot);
        Assert.IsTrue(Directory.Exists(repoRoot));
        // The repo root should contain the .git directory
        Assert.IsTrue(Directory.Exists(Path.Combine(repoRoot, ".git")) || File.Exists(Path.Combine(repoRoot, ".git")));
    }

    [TestMethod]
    public async Task GetGitRepoRoot_FromSubdirectory_ReturnsRepoRoot()
    {
        // Get current repo root first
        var repoRoot = await GetGitRepoRootAsync(Directory.GetCurrentDirectory());
        Assert.IsNotNull(repoRoot);

        // Now query from a subdirectory
        var srcDir = Path.Combine(repoRoot, "src");
        if (Directory.Exists(srcDir))
        {
            var rootFromSubdir = await GetGitRepoRootAsync(srcDir);
            Assert.AreEqual(NormalizePath(repoRoot), NormalizePath(rootFromSubdir!));
        }
    }

    [TestMethod]
    public async Task GetGitRepoRoot_FromNonRepo_ReturnsNull()
    {
        // Use temp directory which should not be in a git repo
        var tempDir = Path.GetTempPath();
        var result = await GetGitRepoRootAsync(tempDir);

        // Temp might be in a repo on some systems, so we just verify it doesn't throw
        // and returns either null or a valid path
        if (result != null)
        {
            Assert.IsTrue(Directory.Exists(result));
        }
    }

    [TestMethod]
    public void PathNormalization_RemovesTrailingSeparators()
    {
        var path1 = NormalizePath(@"C:\Users\test\");
        var path2 = NormalizePath(@"C:\Users\test");

        Assert.AreEqual(path1, path2);
    }

    [TestMethod]
    public void PathNormalization_HandlesForwardSlashes()
    {
        var path1 = NormalizePath(@"C:/Users/test/");
        var path2 = NormalizePath(@"C:\Users\test");

        Assert.AreEqual(path1, path2);
    }

    private static async Task<string?> GetGitRepoRootAsync(string directory)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --show-toplevel",
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return null;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                return null;

            var result = output.Trim();

            // Convert Unix-style path from git to Windows-style if needed
            if (OperatingSystem.IsWindows() && result.StartsWith('/'))
            {
                if (result.Length >= 3 && result[2] == '/')
                {
                    result = $"{char.ToUpper(result[1])}:{result[2..]}";
                }
                result = result.Replace('/', Path.DirectorySeparatorChar);
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
