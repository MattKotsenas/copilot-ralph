// Tests for directory sandboxing functionality.

using Ralph.Cli.Sdk;

namespace Ralph.Tests;

[TestClass]
public sealed class DirectorySandboxTests
{
    [TestMethod]
    public void AllowedDirectories_DefaultsToWorkingDir_WhenEmpty()
    {
        var config = new ClientConfig
        {
            WorkingDir = Path.GetTempPath(),
            AllowedDirectories = []
        };

        var client = new CopilotClient(config);

        Assert.HasCount(1, client.AllowedDirectories);
        // Paths are normalized (trailing slash removed)
        var expected = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Assert.AreEqual(expected, client.AllowedDirectories[0]);
    }

    [TestMethod]
    public void AllowedDirectories_NormalizesRelativePaths()
    {
        var config = new ClientConfig
        {
            WorkingDir = ".",
            AllowedDirectories = [".", ".."]
        };

        var client = new CopilotClient(config);

        Assert.HasCount(2, client.AllowedDirectories);
        Assert.AreEqual(Path.GetFullPath("."), client.AllowedDirectories[0]);
        Assert.AreEqual(Path.GetFullPath(".."), client.AllowedDirectories[1]);
    }

    [TestMethod]
    public void IsPathAllowed_ReturnsTrueForPathsWithinAllowedDirectory()
    {
        var tempDir = Path.GetTempPath();
        var config = new ClientConfig
        {
            AllowedDirectories = [tempDir]
        };

        var client = new CopilotClient(config);

        // Path within allowed directory
        var allowedPath = Path.Combine(tempDir, "subdir", "file.txt");
        Assert.IsTrue(client.IsPathAllowed(allowedPath));
    }

    [TestMethod]
    public void IsPathAllowed_ReturnsFalseForPathsOutsideAllowedDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "sandbox-test");
        var config = new ClientConfig
        {
            AllowedDirectories = [tempDir]
        };

        var client = new CopilotClient(config);

        // Path outside allowed directory
        var outsidePath = Path.Combine(Path.GetTempPath(), "other-dir", "file.txt");
        Assert.IsFalse(client.IsPathAllowed(outsidePath));
    }

    [TestMethod]
    public void IsPathAllowed_ReturnsTrueForExactAllowedDirectory()
    {
        var tempDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var config = new ClientConfig
        {
            AllowedDirectories = [tempDir]
        };

        var client = new CopilotClient(config);

        // Exact match
        Assert.IsTrue(client.IsPathAllowed(tempDir));
    }

    [TestMethod]
    public void IsPathAllowed_ReturnsFalseForEmptyPath()
    {
        var config = new ClientConfig
        {
            AllowedDirectories = [Path.GetTempPath()]
        };

        var client = new CopilotClient(config);

        Assert.IsFalse(client.IsPathAllowed(""));
        Assert.IsFalse(client.IsPathAllowed(null!));
    }

    [TestMethod]
    public void IsPathAllowed_HandlesMultipleAllowedDirectories()
    {
        var dir1 = Path.Combine(Path.GetTempPath(), "dir1");
        var dir2 = Path.Combine(Path.GetTempPath(), "dir2");
        var config = new ClientConfig
        {
            AllowedDirectories = [dir1, dir2]
        };

        var client = new CopilotClient(config);

        // Paths in both directories should be allowed
        Assert.IsTrue(client.IsPathAllowed(Path.Combine(dir1, "file.txt")));
        Assert.IsTrue(client.IsPathAllowed(Path.Combine(dir2, "file.txt")));

        // Path in neither should be denied
        var dir3 = Path.Combine(Path.GetTempPath(), "dir3");
        Assert.IsFalse(client.IsPathAllowed(Path.Combine(dir3, "file.txt")));
    }

    [TestMethod]
    public void IsPathAllowed_PreventsDirectoryTraversal()
    {
        var allowedDir = Path.Combine(Path.GetTempPath(), "allowed");
        var config = new ClientConfig
        {
            AllowedDirectories = [allowedDir]
        };

        var client = new CopilotClient(config);

        // Attempt to traverse up and back down should be blocked
        // Path.GetFullPath resolves ".." so this might still be blocked
        var traversalPath = Path.Combine(allowedDir, "..", "other", "file.txt");
        var resolvedPath = Path.GetFullPath(traversalPath);

        // The resolved path is outside the allowed directory
        Assert.IsFalse(resolvedPath.StartsWith(Path.GetFullPath(allowedDir), StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(client.IsPathAllowed(traversalPath));
    }

    [TestMethod]
    public void IsPathAllowed_IsCaseInsensitiveOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            // This test is Windows-specific
            return;
        }

        var tempDir = Path.GetTempPath();
        var config = new ClientConfig
        {
            AllowedDirectories = [tempDir.ToLowerInvariant()]
        };

        var client = new CopilotClient(config);

        // Should match regardless of case on Windows
        var upperCasePath = Path.Combine(tempDir.ToUpperInvariant(), "file.txt");
        Assert.IsTrue(client.IsPathAllowed(upperCasePath));
    }

    [TestMethod]
    public void IsPathAllowed_PreventsPrefixAttack()
    {
        // Ensure "C:\allowed" doesn't accidentally allow "C:\allowedextra"
        var allowedDir = Path.Combine(Path.GetTempPath(), "allowed");
        var config = new ClientConfig
        {
            AllowedDirectories = [allowedDir]
        };

        var client = new CopilotClient(config);

        // This path starts with the allowed directory string but is not actually inside it
        var prefixAttackPath = allowedDir + "extra" + Path.DirectorySeparatorChar + "file.txt";
        Assert.IsFalse(client.IsPathAllowed(prefixAttackPath));
    }

    [TestMethod]
    public void IsPathAllowed_DeniesAccessToDriveRoot()
    {
        // Simulate the user's scenario: working in D:\Projects\copilot-ralph
        // but trying to access D:\hello.txt
        var workingDir = Path.Combine(Path.GetTempPath(), "projects", "test-project");
        var config = new ClientConfig
        {
            WorkingDir = workingDir,
            AllowedDirectories = [workingDir]
        };

        var client = new CopilotClient(config);

        // Files in project directory should be allowed
        Assert.IsTrue(client.IsPathAllowed(Path.Combine(workingDir, "src", "file.cs")));

        // Drive root should be denied
        var driveRoot = Path.GetPathRoot(workingDir) ?? "C:\\";
        var rootFilePath = Path.Combine(driveRoot, "hello.txt");
        Assert.IsFalse(client.IsPathAllowed(rootFilePath), $"Path at drive root should be denied: {rootFilePath}");

        // Parent directories should be denied
        var parentDir = Directory.GetParent(workingDir)?.FullName;
        if (parentDir != null)
        {
            Assert.IsFalse(client.IsPathAllowed(Path.Combine(parentDir, "other-file.txt")),
                $"Path in parent directory should be denied: {parentDir}");
        }
    }

    [TestMethod]
    public void IsPathAllowed_DeniesAbsolutePathOutsideAllowedDir()
    {
        // When allowed dir is a subdirectory, absolute paths outside should be denied
        var allowedDir = Path.Combine(Path.GetTempPath(), "sandbox");
        var config = new ClientConfig
        {
            AllowedDirectories = [allowedDir]
        };

        var client = new CopilotClient(config);

        // These absolute paths should all be denied
        Assert.IsFalse(client.IsPathAllowed("C:\\Windows\\System32\\cmd.exe"));
        Assert.IsFalse(client.IsPathAllowed("D:\\"));
        Assert.IsFalse(client.IsPathAllowed("D:\\hello.txt"));
        Assert.IsFalse(client.IsPathAllowed("/etc/passwd")); // Unix-style path
    }
}

/// <summary>
/// Tests for shell command path extraction used in directory sandboxing.
/// </summary>
[TestClass]
public sealed class ShellCommandPathExtractionTests
{
    [TestMethod]
    public void ExtractPathsFromShellCommand_FindsWindowsAbsolutePath()
    {
        // Create a client with a specific allowed directory
        var allowedDir = Path.Combine(Path.GetTempPath(), "test-allowed");
        var config = new ClientConfig
        {
            AllowedDirectories = [allowedDir]
        };

        var client = new CopilotClient(config);

        // Test the IsPathAllowed with paths that would be extracted from a command
        // like: Set-Content -Path "D:\hello.txt" -Value "test"
        Assert.IsFalse(client.IsPathAllowed("D:\\hello.txt"));
        Assert.IsFalse(client.IsPathAllowed("C:\\Windows\\file.txt"));

        // Paths within allowed directory should pass
        Assert.IsTrue(client.IsPathAllowed(Path.Combine(allowedDir, "file.txt")));
    }

    [TestMethod]
    public void AllowedDirectory_PermitsOperationsInWorkingDir()
    {
        // The default behavior should allow operations in the working directory
        var workingDir = Directory.GetCurrentDirectory();
        var config = new ClientConfig
        {
            WorkingDir = workingDir,
            AllowedDirectories = [] // Empty means default to working dir
        };

        var client = new CopilotClient(config);

        // Operations in working dir should be allowed
        Assert.IsTrue(client.IsPathAllowed(Path.Combine(workingDir, "test.txt")));
        Assert.IsTrue(client.IsPathAllowed(Path.Combine(workingDir, "subdir", "file.txt")));

        // Operations outside working dir should be denied
        var driveRoot = Path.GetPathRoot(workingDir) ?? "C:\\";
        Assert.IsFalse(client.IsPathAllowed(Path.Combine(driveRoot, "outside.txt")));
    }

    [TestMethod]
    public void ShellSandboxing_BlocksAccessToDriveRoot()
    {
        // Simulates the user's scenario: running from D:\Projects\copilot-ralph
        // should block access to D:\hello.txt
        var projectDir = Path.Combine(Path.GetTempPath(), "Projects", "test-project");
        var config = new ClientConfig
        {
            WorkingDir = projectDir,
            AllowedDirectories = [projectDir]
        };

        var client = new CopilotClient(config);

        // Access to project files should be allowed
        Assert.IsTrue(client.IsPathAllowed(Path.Combine(projectDir, "src", "file.cs")));
        Assert.IsTrue(client.IsPathAllowed(Path.Combine(projectDir, "README.md")));

        // Access to drive root should be blocked
        var driveRoot = Path.GetPathRoot(projectDir) ?? "C:\\";
        Assert.IsFalse(client.IsPathAllowed(Path.Combine(driveRoot, "hello.txt")));
        Assert.IsFalse(client.IsPathAllowed(driveRoot));
    }
}