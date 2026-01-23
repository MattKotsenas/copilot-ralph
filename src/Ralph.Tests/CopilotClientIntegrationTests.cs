// Integration tests for the Copilot SDK client.
// These tests require the GitHub Copilot CLI to be installed.
// They will be skipped if the CLI is not available.

using System.Diagnostics;
using System.Text;
using Ralph.Cli.Sdk;

namespace Ralph.Tests;

[TestClass]
public sealed class CopilotClientIntegrationTests
{
    private static bool IsCopilotCliAvailable()
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "copilot",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process == null)
                return false;

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    [TestMethod]
    [Timeout(60000)] // 60 second timeout for integration tests
    public async Task CopilotClient_CanStartAndStop()
    {
        if (!IsCopilotCliAvailable())
        {
            Assert.Inconclusive("Copilot CLI not available - skipping integration test");
            return;
        }

        var config = new ClientConfig
        {
            Model = "gpt-4",
            LogLevel = "error" // Reduce noise in tests
        };

        await using var client = new CopilotClient(config);

        // Start the client
        await client.StartAsync();

        // Stop should not throw
        await client.StopAsync();
    }

    [TestMethod]
    [Timeout(60000)]
    public async Task CopilotClient_CanCreateSession()
    {
        if (!IsCopilotCliAvailable())
        {
            Assert.Inconclusive("Copilot CLI not available - skipping integration test");
            return;
        }

        var config = new ClientConfig
        {
            Model = "gpt-4",
            LogLevel = "error"
        };

        await using var client = new CopilotClient(config);
        await client.StartAsync();

        // Create session should not throw
        await client.CreateSessionAsync();

        // Destroy session should not throw
        await client.DestroySessionAsync();
    }

    [TestMethod]
    [Timeout(120000)] // 2 minute timeout for prompt test
    public async Task CopilotClient_CanSendSimplePrompt()
    {
        if (!IsCopilotCliAvailable())
        {
            Assert.Inconclusive("Copilot CLI not available - skipping integration test");
            return;
        }

        var config = new ClientConfig
        {
            Model = "gpt-4",
            LogLevel = "error",
            Streaming = true,
            // Allow all tools by default (null = all tools)
            AvailableTools = null,
            ExcludedTools = null
        };

        await using var client = new CopilotClient(config);
        await client.StartAsync();
        await client.CreateSessionAsync();

        // Send a simple prompt that doesn't require tools
        var events = client.SendPromptAsync("Say 'Hello World' and nothing else.");

        var receivedText = false;
        await foreach (var evt in events.ReadAllAsync())
        {
            if (evt is TextEvent textEvent && !string.IsNullOrEmpty(textEvent.Text))
            {
                receivedText = true;
            }
        }

        Assert.IsTrue(receivedText, "Should have received text response from AI");
    }

    [TestMethod]
    [Timeout(120000)]
    public async Task CopilotClient_WithAvailableToolsNull_AllowsAllTools()
    {
        if (!IsCopilotCliAvailable())
        {
            Assert.Inconclusive("Copilot CLI not available - skipping integration test");
            return;
        }

        var config = new ClientConfig
        {
            Model = "gpt-4",
            LogLevel = "error",
            Streaming = true,
            // null means all tools are available
            AvailableTools = null,
            ExcludedTools = null
        };

        await using var client = new CopilotClient(config);
        await client.StartAsync();

        // CreateSession should not throw when AvailableTools is null
        await client.CreateSessionAsync();

        // Send a prompt that might use tools
        var events = client.SendPromptAsync("What is 2+2? Just respond with the number.");

        var completed = false;
        await foreach (var evt in events.ReadAllAsync())
        {
            if (evt is TextEvent)
            {
                completed = true;
            }
        }

        Assert.IsTrue(completed, "Session should complete without tool availability errors");
    }

    [TestMethod]
    [Timeout(120000)]
    public async Task CopilotClient_DirectorySandboxing_DeniesOutsidePaths()
    {
        if (!IsCopilotCliAvailable())
        {
            Assert.Inconclusive("Copilot CLI not available - skipping integration test");
            return;
        }

        // Create a specific allowed directory
        var allowedDir = Path.Combine(Path.GetTempPath(), "ralph-test-sandbox");
        Directory.CreateDirectory(allowedDir);

        try
        {
            var config = new ClientConfig
            {
                Model = "gpt-4",
                LogLevel = "error",
                Streaming = true,
                WorkingDir = allowedDir,
                AllowedDirectories = [allowedDir],
                AvailableTools = null
            };

            await using var client = new CopilotClient(config);

            // Verify our path checking works
            Assert.IsTrue(client.IsPathAllowed(Path.Combine(allowedDir, "test.txt")));
            Assert.IsFalse(client.IsPathAllowed(Path.Combine(Path.GetTempPath(), "outside.txt")));
        }
        finally
        {
            // Cleanup
            try { Directory.Delete(allowedDir, true); } catch { }
        }
    }

    [TestMethod]
    [Timeout(180000)] // 3 minute timeout for tool test
    public async Task CopilotClient_CanUseViewTool()
    {
        if (!IsCopilotCliAvailable())
        {
            Assert.Inconclusive("Copilot CLI not available - skipping integration test");
            return;
        }

        // Create a test file
        var testDir = Path.Combine(Path.GetTempPath(), "ralph-tool-test");
        Directory.CreateDirectory(testDir);
        var testFile = Path.Combine(testDir, "test-file.txt");
        var testContent = "This is unique test content: ABC123XYZ";
        await File.WriteAllTextAsync(testFile, testContent);

        try
        {
            var config = new ClientConfig
            {
                Model = "gpt-4",
                LogLevel = "debug", // Use debug to see what's happening
                Streaming = true,
                WorkingDir = testDir,
                AllowedDirectories = [testDir],
                // Explicitly null to allow all tools
                AvailableTools = null,
                ExcludedTools = null
            };

            await using var client = new CopilotClient(config);
            await client.StartAsync();
            await client.CreateSessionAsync();

            // Ask to read the file - this requires the view tool
            var events = client.SendPromptAsync($"Read the file at {testFile} and tell me what unique content it contains.");

            var responseText = new StringBuilder();
            var sawToolExecution = false;
            var sawError = false;
            string? errorMessage = null;

            await foreach (var evt in events.ReadAllAsync())
            {
                switch (evt)
                {
                    case TextEvent textEvent:
                        responseText.Append(textEvent.Text);
                        break;
                    case ToolCallEvent:
                        sawToolExecution = true;
                        break;
                    case ToolResultEvent:
                        sawToolExecution = true;
                        break;
                    case ErrorEvent errorEvent:
                        sawError = true;
                        errorMessage = errorEvent.Error?.Message;
                        break;
                }
            }

            // Verify no errors
            Assert.IsFalse(sawError, $"Should not have errors. Error: {errorMessage}");

            // Verify tool was used or response contains our content
            var response = responseText.ToString();
            var containsContent = response.Contains("ABC123XYZ") || response.Contains("unique");

            Assert.IsTrue(sawToolExecution || containsContent, 
                $"Should have used a tool or referenced the file content. Response: {response}");
        }
        finally
        {
            // Cleanup
            try { Directory.Delete(testDir, true); } catch { }
        }
    }

    [TestMethod]
    [Timeout(180000)]
    public async Task CopilotClient_CanUsePowershellTool()
    {
        if (!IsCopilotCliAvailable())
        {
            Assert.Inconclusive("Copilot CLI not available - skipping integration test");
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("PowerShell test only runs on Windows");
            return;
        }

        var testDir = Path.Combine(Path.GetTempPath(), "ralph-ps-test");
        Directory.CreateDirectory(testDir);

        try
        {
            var config = new ClientConfig
            {
                Model = "gpt-4",
                LogLevel = "debug",
                Streaming = true,
                WorkingDir = testDir,
                AllowedDirectories = [testDir],
                AvailableTools = null,
                ExcludedTools = null
            };

            await using var client = new CopilotClient(config);
            await client.StartAsync();
            await client.CreateSessionAsync();

            // Ask to run a simple PowerShell command
            var events = client.SendPromptAsync("Run the PowerShell command 'Get-Date -Format yyyy' and tell me what year it returns.");

            var responseText = new StringBuilder();
            var sawToolExecution = false;
            var sawError = false;
            string? errorMessage = null;

            await foreach (var evt in events.ReadAllAsync())
            {
                switch (evt)
                {
                    case TextEvent textEvent:
                        responseText.Append(textEvent.Text);
                        break;
                    case ToolCallEvent toolCall:
                        sawToolExecution = true;
                        Console.WriteLine($"Tool called: {toolCall.ToolCall.Name}");
                        break;
                    case ToolResultEvent:
                        sawToolExecution = true;
                        break;
                    case ErrorEvent errorEvent:
                        sawError = true;
                        errorMessage = errorEvent.Error?.Message;
                        break;
                }
            }

            // Verify no errors about tool availability
            if (sawError && errorMessage != null)
            {
                Assert.IsFalse(
                    errorMessage.Contains("tool") && errorMessage.Contains("available"),
                    $"Should not have tool availability errors. Error: {errorMessage}");
            }

            // Check that we got a response with a year
            var response = responseText.ToString();
            var hasYear = response.Contains("2026") || response.Contains("202");

            Assert.IsTrue(sawToolExecution || hasYear,
                $"Should have executed PowerShell tool or returned a year. Response: {response}");
        }
        finally
        {
            try { Directory.Delete(testDir, true); } catch { }
        }
    }
}
