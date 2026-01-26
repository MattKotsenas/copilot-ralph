namespace Ralph.Cli.Core;

/// <summary>
/// Represents the current state of the loop.
/// </summary>
public enum LoopState
{
    Idle,
    Running,
    Complete,
    Failed,
    Cancelled
}

/// <summary>
/// Configuration for loop execution.
/// </summary>
public sealed class LoopConfig
{
    /// <summary>
    /// The default AI model to use.
    /// </summary>
    public const string DefaultModel = "claude-opus-4-5";

    public string Prompt { get; set; } = string.Empty;
    public string PromisePhrase { get; set; } = "I'm special!";
    public string Model { get; set; } = DefaultModel;
    public string WorkingDir { get; set; } = ".";
    public int MaxIterations { get; set; } = 10;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);
    public bool DryRun { get; set; }

    /// <summary>
    /// Creates a default configuration.
    /// </summary>
    public static LoopConfig Default => new();
}

/// <summary>
/// Contains the outcome of loop execution.
/// </summary>
public sealed class LoopResult
{
    public LoopState State { get; set; }
    public int Iterations { get; set; }
    public TimeSpan Duration { get; set; }
    public Exception? Error { get; set; }
}