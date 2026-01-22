// SDK event types for Copilot SDK communication.

namespace Ralph.Cli.Sdk;

/// <summary>
/// Represents the type of event from the Copilot SDK.
/// </summary>
public enum EventType
{
    Text,
    ToolCall,
    ToolResult,
    ResponseComplete,
    Error
}

/// <summary>
/// Represents an event from the Copilot SDK.
/// </summary>
public interface IEvent
{
    EventType Type { get; }
    DateTime Timestamp { get; }
}

/// <summary>
/// Represents a text/streaming content event.
/// </summary>
public sealed class TextEvent : IEvent
{
    public EventType Type => EventType.Text;
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public required string Text { get; init; }
    public bool Reasoning { get; init; }
}

/// <summary>
/// Represents a tool invocation request from the assistant.
/// </summary>
public sealed class ToolCallEvent : IEvent
{
    public EventType Type => EventType.ToolCall;
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public required ToolCall ToolCall { get; init; }
}

/// <summary>
/// Represents the result of a tool execution.
/// </summary>
public sealed class ToolResultEvent : IEvent
{
    public EventType Type => EventType.ToolResult;
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public required ToolCall ToolCall { get; init; }
    public string? Result { get; init; }
    public Exception? Error { get; init; }
}

/// <summary>
/// Represents an error that occurred during processing.
/// </summary>
public sealed class ErrorEvent : IEvent
{
    public EventType Type => EventType.Error;
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public required Exception Error { get; init; }
}
