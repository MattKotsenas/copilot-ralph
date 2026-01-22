// Tool types for Copilot SDK integration.

namespace Ralph.Cli.Sdk;

/// <summary>
/// Represents a tool invocation request from the assistant.
/// </summary>
public sealed record ToolCall
{
    public string Id { get; init; } = string.Empty;
    public required string Name { get; init; }
    public Dictionary<string, object?> Parameters { get; init; } = [];
}
