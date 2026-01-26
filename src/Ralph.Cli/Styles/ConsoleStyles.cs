// Styles and colors for console output using Spectre.Console.

using Spectre.Console;

namespace Ralph.Cli.Styles;

/// <summary>
/// Provides styling constants and helpers for console output.
/// </summary>
public static class ConsoleStyles
{
    // Color palette
    public static readonly Color Primary = new(255, 28, 240);   // Hot Pink
    public static readonly Color Success = new(80, 250, 123);   // Bright Green
    public static readonly Color Warning = new(249, 226, 175);  // Yellow
    public static readonly Color Error = new(243, 139, 168);    // Red
    public static readonly Color Info = new(0, 217, 255);       // Bright Cyan

    // Styles
    public static readonly Style TitleStyle = new(Primary, decoration: Decoration.Bold);
    public static readonly Style SubTitleStyle = new(Primary);
    public static readonly Style InfoStyle = new(Info);
    public static readonly Style SuccessStyle = new(Success);
    public static readonly Style WarningStyle = new(Warning);
    public static readonly Style ErrorStyle = new(Error);

    /// <summary>
    /// Formats text with the title style.
    /// </summary>
    public static string Title(string text) => $"[bold rgb(255,28,240)]{Markup.Escape(text)}[/]";

    /// <summary>
    /// Formats text with the subtitle style.
    /// </summary>
    public static string SubTitle(string text) => $"[rgb(255,28,240)]{Markup.Escape(text)}[/]";

    /// <summary>
    /// Formats text with the info style.
    /// </summary>
    public static string InfoText(string text) => $"[rgb(0,217,255)]{Markup.Escape(text)}[/]";

    /// <summary>
    /// Formats text with the success style.
    /// </summary>
    public static string SuccessText(string text) => $"[rgb(80,250,123)]{Markup.Escape(text)}[/]";

    /// <summary>
    /// Formats text with the warning style.
    /// </summary>
    public static string WarningText(string text) => $"[rgb(249,226,175)]{Markup.Escape(text)}[/]";

    /// <summary>
    /// Formats text with the error style.
    /// </summary>
    public static string ErrorText(string text) => $"[rgb(243,139,168)]{Markup.Escape(text)}[/]";
}