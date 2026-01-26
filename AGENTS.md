# Ralph Development Guide for AI Agents

This comprehensive guide helps AI agents understand Ralph's architecture, conventions, and development patterns. Use this as your primary reference when working on Ralph.

## Table of Contents

1. [Project Overview](#project-overview)
2. [Architecture](#architecture)
3. [Package Structure](#package-structure)
4. [Development Patterns](#development-patterns)
5. [C# Conventions](#c-conventions)
6. [Testing Guidelines](#testing-guidelines)
7. [Error Handling](#error-handling)
8. [Code Style](#code-style)

## Project Overview

**Ralph** is an iterative AI development loop tool that implements the "Ralph Wiggum" technique for self-referential AI development. It continuously feeds prompts to GitHub Copilot AI, monitoring for a "completion promise" phrase, with each iteration building on previous work.

This is a C# port of the [original Go implementation](https://github.com/JanDeDobbeleer/copilot-ralph).

### Core Concept

```text
User provides task → Ralph starts loop → AI iterates → Makes changes → Tests →
Checks for promise → If found: complete, else: continue → Max iterations reached or timeout
```

### Technology Stack

- **Language:** C# (.NET 10.0)
- **CLI:** System.CommandLine (command framework)
- **Console:** Spectre.Console (terminal rendering)
- **Testing:** MSTest with Verify.MSTest for snapshot testing

## Architecture

### High-Level Architecture

```text
┌─────────────────────────────────────────────────────────────┐
│                     Commands Layer                          │
│  (Ralph.Cli/Commands)                                       │
│  - Command parsing (System.CommandLine)                    │
│  - Flag handling                                            │
│  - Entry point                                              │
└────────────────┬────────────────────────────────────────────┘
                 │
┌────────────────▼────────────────────────────────────────────┐
│                     Styles Layer                            │
│  (Ralph.Cli/Styles)                                         │
│  - Console rendering (Spectre.Console)                      │
│  - Color schemes                                            │
│  - Visual feedback                                          │
└────────────────┬────────────────────────────────────────────┘
                 │
┌────────────────▼────────────────────────────────────────────┐
│                       Core Layer                            │
│  (Ralph.Cli/Core)                                           │
│  - Loop engine                                              │
│  - State management                                         │
│  - Business logic                                           │
└────────────────┬────────────────────────────────────────────┘
                 │
┌────────────────▼────────────────────────────────────────────┐
│                       SDK Layer                             │
│  (Ralph.Cli/Sdk)                                            │
│  - Copilot SDK wrapper                                      │
│  - Session management                                       │
│  - Tool handling                                            │
└─────────────────────────────────────────────────────────────┘
```

### Data Flow

```text
CLI Flags → Loop Config → Loop Engine → SDK Client → Copilot AI
                              ↓
                          Event Stream
                              ↓
                         Console Update
                              ↓
                          User Display
```

## Package Structure

### `Ralph.Cli/Commands`

**Purpose:** System.CommandLine command definitions

**Responsibilities:**

- Define commands (run, version)
- Register flags
- Validate inputs
- Orchestrate execution flow

**Dependencies:** `Core`, `Sdk`, `Styles`, `Version`

### `Ralph.Cli/Styles`

**Purpose:** Spectre.Console styling

**Responsibilities:**

- Define colors and styles
- Provide markup helpers
- ASCII art (Ralph Wiggum)

**Dependencies:** Spectre.Console

### `Ralph.Cli/Core`

**Purpose:** Business logic and loop engine

**Responsibilities:**

- Loop state machine
- Iteration management
- Promise detection
- Event emission
- System prompt building

**Dependencies:** `Sdk`

**Pattern:** No external UI dependencies, pure logic

### `Ralph.Cli/Sdk`

**Purpose:** Copilot SDK integration

**Responsibilities:**

- SDK client wrapper
- Session management
- Event handling
- Tool registration
- Error handling

**Pattern:** Abstract SDK details from core logic

### `Ralph.Cli/Version`

**Purpose:** Version information

**Responsibilities:**

- Store version, commit, build date
- Provide version info

**Pattern:** Set via MSBuild properties

### `Ralph.Tests`

**Purpose:** Unit tests

**Responsibilities:**

- Test all components
- Mock dependencies
- Table-driven test patterns

## Development Patterns

### Interface-Based Design

Define interfaces for testability:

```csharp
// Ralph.Cli/Sdk/CopilotClient.cs
public interface ICopilotClient
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
    Task CreateSessionAsync(CancellationToken cancellationToken = default);
    Task DestroySessionAsync(CancellationToken cancellationToken = default);
    ChannelReader<IEvent> SendPromptAsync(string prompt, CancellationToken cancellationToken = default);
    string Model { get; }
}

// CopilotClient implements ICopilotClient
```

### Dependency Injection

Pass dependencies explicitly:

```csharp
// Good
public LoopEngine(LoopConfig? config, ICopilotClient? sdk = null)
{
    _config = config ?? LoopConfig.Default;
    _sdk = sdk;
}

// Bad - static/global dependency
public static ICopilotClient GlobalSdk;
```

### Event-Driven Communication

Use Channels for async communication:

```csharp
public sealed class LoopEngine
{
    private readonly Channel<ILoopEvent> _events;

    public ChannelReader<ILoopEvent> Events => _events.Reader;
}

// Consumers
await foreach (var loopEvent in engine.Events.ReadAllAsync(cancellationToken))
{
    // Handle event
}
```

## C# Conventions

### Early Returns

Prefer early returns over nested conditions:

```csharp
// Good
public async Task<LoopResult> StartAsync(CancellationToken cancellationToken = default)
{
    if (_state != LoopState.Idle)
        throw new InvalidOperationException("Loop already running");

    // Continue with main logic
}

// Bad
public async Task<LoopResult> StartAsync(CancellationToken cancellationToken = default)
{
    if (_state == LoopState.Idle)
    {
        // Deeply nested logic
    }
    else
    {
        throw new InvalidOperationException("Loop already running");
    }
}
```

### Record Types for Events

Use records for immutable event types:

```csharp
public sealed record LoopStartEvent(LoopConfig Config) : ILoopEvent;
public sealed record IterationStartEvent(int Iteration, int MaxIterations) : ILoopEvent;
public sealed record AIResponseEvent(string Text, int Iteration) : ILoopEvent;
```

### Nullable Reference Types

Always enable nullable reference types and handle nulls explicitly:

```csharp
public sealed class LoopResult
{
    public LoopState State { get; set; }
    public int Iterations { get; set; }
    public TimeSpan Duration { get; set; }
    public Exception? Error { get; set; }  // Nullable
}
```

### Async/Await

Use async/await consistently:

```csharp
public async Task<LoopResult> StartAsync(CancellationToken cancellationToken = default)
{
    await _sdk.StartAsync(cancellationToken);
    await _sdk.CreateSessionAsync(cancellationToken);
    // ...
}
```

## Testing Guidelines

### MSTest v2 Pattern

```csharp
[TestClass]
public sealed class PromiseDetectorTests
{
    [TestMethod]
    [DataRow("<promise>I'm done!</promise>", "I'm done!", true)]
    [DataRow("Still working", "I'm done!", false)]
    public void DetectPromise_VariousCases(string text, string promise, bool expected)
    {
        var result = PromiseDetector.DetectPromise(text, promise);
        Assert.AreEqual(expected, result);
    }
}
```

### Snapshot Testing with Verify

We use [Verify](https://github.com/VerifyTests/Verify) for snapshot testing of generated outputs like shell completion scripts.

```csharp
[TestClass]
public sealed class CompletionScriptTests : VerifyBase
{
    [TestMethod]
    public Task PowerShell_CompletionScript_MatchesSnapshot()
    {
        var script = CompletionScripts.GetScript("pwsh");
        return Verify(script);
    }
}
```

**Updating Snapshots:**

When a snapshot test fails because the output has intentionally changed, use the verify tool to accept the new baseline:

```powershell
# Accept all pending changes
dotnet verify accept

# Review changes interactively
dotnet verify review
```

The `.verified.*` files are the accepted baselines (committed to git). The `.received.*` files are the actual test outputs (ignored by git).

### Mock Dependencies

Create mock implementations for testing:

```csharp
public class MockSdkClient : ICopilotClient
{
    public string Model { get; set; } = "mock-model";
    public string ResponseText { get; set; } = "Mock response";
    public bool SimulatePromise { get; set; }
    public Exception? StartError { get; set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (StartError != null)
            throw StartError;
        return Task.CompletedTask;
    }
    // ...
}
```

### Test Timeouts

Add timeouts to async tests that could hang:

```csharp
[TestMethod]
[Timeout(5000)]  // 5 second timeout
public async Task Start_Cancellation_TransitionsToCancelled()
{
    // ...
}
```

## Error Handling

### Custom Exceptions

Define custom exceptions for domain errors:

```csharp
public class LoopCancelledException : Exception
{
    public LoopCancelledException() : base("Loop cancelled") { }
}

public class LoopTimeoutException : Exception
{
    public LoopTimeoutException() : base("Loop timeout exceeded") { }
}
```

### Exception Wrapping

Wrap exceptions with context:

```csharp
try
{
    await _sdk.StartAsync(cancellationToken);
}
catch (Exception ex)
{
    return await FailAsync(new Exception($"Failed to start SDK: {ex.Message}", ex));
}
```

## Code Style

### Naming Conventions

- **Namespaces:** PascalCase (e.g., `Ralph.Cli.Core`)
- **Types:** PascalCase (e.g., `LoopEngine`, `LoopConfig`)
- **Methods:** PascalCase (e.g., `StartAsync`, `DetectPromise`)
- **Properties:** PascalCase (e.g., `MaxIterations`, `PromisePhrase`)
- **Private fields:** _camelCase (e.g., `_config`, `_events`)
- **Parameters:** camelCase (e.g., `cancellationToken`, `prompt`)
- **Constants:** PascalCase (e.g., `DefaultModel`, `EventChannelBufferSize`)

### File Organization

```csharp
// 1. Using directives (sorted)
using System.Threading.Channels;
using Ralph.Cli.Sdk;

// 2. Namespace
namespace Ralph.Cli.Core;

// 3. Type declaration with XML docs
/// <summary>
/// Manages the execution of AI development loops.
/// </summary>
public sealed class LoopEngine
{
    // 4. Constants
    private const int EventChannelBufferSize = 100;

    // 5. Fields
    private readonly LoopConfig _config;
    private readonly ICopilotClient? _sdk;

    // 6. Properties
    public LoopState State { get; }
    public LoopConfig Config => _config;

    // 7. Constructors
    public LoopEngine(LoopConfig? config, ICopilotClient? sdk = null)
    {
    }

    // 8. Public methods
    public async Task<LoopResult> StartAsync(CancellationToken cancellationToken = default)
    {
    }

    // 9. Private methods
    private async Task<LoopResult> RunLoopAsync()
    {
    }
}
```

## Quick Reference

### MANDATORY: Testing Requirements

**Every code change MUST include corresponding tests:**

- New methods → Unit tests covering happy path + edge cases
- New classes → Create test class in `Ralph.Tests`
- Bug fixes → Add regression test
- Refactoring → Ensure tests pass, add new coverage

**Before marking work complete:**

1. Run `dotnet build` - Build must succeed
2. Run `dotnet test` - All tests must pass
3. Verify test coverage for new code

### Common Tasks

**Add a new command:**

1. Add method to `RootCommands` class
2. Add `[Command("name")]` attribute
3. Define parameters with attributes
4. Implement logic
5. **Write tests**

**Add a new event type:**

1. Create record in `Core/Events.cs`
2. Implement `ILoopEvent`
3. Handle in `LoopEngine`
4. Handle in `RootCommands.DisplayEventsAsync`
5. **Write tests**

### Useful Commands

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run specific tests
dotnet test --filter "FullyQualifiedName~LoopEngineTests"

# Run the CLI
dotnet run --project Ralph.Cli -- version
dotnet run --project Ralph.Cli -- run "Test" --dry-run
```

## Remember

- ✅ Use early returns
- ✅ Use records for immutable data
- ✅ Use async/await consistently
- ✅ Enable nullable reference types
- ✅ Use Channels for async streams
- ✅ Write tests for new code
- ✅ Document public APIs with XML comments
