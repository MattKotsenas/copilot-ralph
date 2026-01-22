// Loop event types for the loop engine.

namespace Ralph.Cli.Core;

/// <summary>
/// Base interface for all loop events.
/// </summary>
public interface ILoopEvent { }

/// <summary>
/// Indicates the loop has started.
/// </summary>
public sealed record LoopStartEvent(LoopConfig Config) : ILoopEvent;

/// <summary>
/// Indicates the loop completed successfully.
/// </summary>
public sealed record LoopCompleteEvent(LoopResult Result) : ILoopEvent;

/// <summary>
/// Indicates the loop failed.
/// </summary>
public sealed record LoopFailedEvent(Exception Error, LoopResult Result) : ILoopEvent;

/// <summary>
/// Indicates the loop was cancelled by the user.
/// </summary>
public sealed record LoopCancelledEvent(LoopResult Result) : ILoopEvent;

/// <summary>
/// Indicates an iteration has started.
/// </summary>
public sealed record IterationStartEvent(int Iteration, int MaxIterations) : ILoopEvent;

/// <summary>
/// Indicates an iteration completed.
/// </summary>
public sealed record IterationCompleteEvent(int Iteration, TimeSpan Duration) : ILoopEvent;

/// <summary>
/// Indicates AI response text was received.
/// </summary>
public sealed record AIResponseEvent(string Text, int Iteration) : ILoopEvent;

/// <summary>
/// Base class for tool-related events.
/// </summary>
public record ToolEventBase(string ToolName, Dictionary<string, object?> Parameters, int Iteration) : ILoopEvent
{
    public string Info(string emoji)
    {
        if (Parameters.Count == 0)
            return $"{emoji} {ToolName}";

        var values = string.Join(", ", Parameters.Values.Select(v => v?.ToString() ?? "null"));
        return $"{emoji} {ToolName}: {values}";
    }
}

/// <summary>
/// Indicates a tool execution has started.
/// </summary>
public sealed record ToolExecutionStartEvent(string ToolName, Dictionary<string, object?> Parameters, int Iteration)
    : ToolEventBase(ToolName, Parameters, Iteration);

/// <summary>
/// Indicates a tool was executed.
/// </summary>
public sealed record ToolExecutionEvent(
    string ToolName,
    Dictionary<string, object?> Parameters,
    string? Result,
    Exception? Error,
    TimeSpan Duration,
    int Iteration
) : ToolEventBase(ToolName, Parameters, Iteration);

/// <summary>
/// Indicates the promise phrase was found.
/// </summary>
public sealed record PromiseDetectedEvent(string Phrase, string Source, int Iteration) : ILoopEvent;

/// <summary>
/// Indicates an error occurred.
/// </summary>
public sealed record ErrorEvent(Exception Error, int Iteration, bool Recoverable) : ILoopEvent;
