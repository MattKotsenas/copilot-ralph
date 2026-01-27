// Terminal title management for showing activity indicator.

namespace Ralph.Cli.Styles;

/// <summary>
/// Interface for managing terminal title state.
/// </summary>
public interface ITerminalTitle : IDisposable
{
    void SetActive();
    void SetIdle();
}

/// <summary>
/// Manages the terminal tab title to show a robot emoji when Ralph is active.
/// Uses ANSI escape sequences compatible with most modern terminals.
/// </summary>
public sealed class TerminalTitle : ITerminalTitle
{
    private const string RobotEmoji = "🤖";
    private const string TitlePrefix = $"{RobotEmoji} Ralph";

    // ANSI escape sequence to set terminal title: ESC ] 0 ; <title> BEL
    private const string SetTitleEscape = "\x1b]0;";
    private const string EndTitleEscape = "\x07";

    private readonly string? _originalTitle;
    private bool _disposed;

    public TerminalTitle()
    {
        _originalTitle = GetConsoleTitleSafe();
    }

    public void SetActive()
    {
        WriteTitle(TitlePrefix);
    }

    public void SetIdle()
    {
        if (_originalTitle != null)
        {
            WriteTitle(_originalTitle);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            SetIdle();
            _disposed = true;
        }
    }

    private static string? GetConsoleTitleSafe()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            return Console.Title;
        }
        catch
        {
            return null;
        }
    }

    private static void WriteTitle(string title)
    {
        try
        {
            Console.Write($"{SetTitleEscape}{title}{EndTitleEscape}");
        }
        catch
        {
            // Ignore failures (redirected output, unsupported terminal)
        }
    }
}

/// <summary>
/// Mock implementation for testing that tracks state changes.
/// </summary>
public sealed class MockTerminalTitle : ITerminalTitle
{
    public bool IsActive { get; private set; }
    public int SetActiveCalls { get; private set; }
    public int SetIdleCalls { get; private set; }
    public bool IsDisposed { get; private set; }

    public void SetActive()
    {
        IsActive = true;
        SetActiveCalls++;
    }

    public void SetIdle()
    {
        IsActive = false;
        SetIdleCalls++;
    }

    public void Dispose()
    {
        if (!IsDisposed)
        {
            SetIdle();
            IsDisposed = true;
        }
    }
}
