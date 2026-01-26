// Promise detection for the loop engine.

namespace Ralph.Cli.Core;

/// <summary>
/// Provides promise phrase detection functionality.
/// </summary>
public static class PromiseDetector
{
    /// <summary>
    /// Checks if the given text contains the promise phrase wrapped in promise tags.
    /// </summary>
    /// <param name="text">The text to search in.</param>
    /// <param name="promisePhrase">The promise phrase to look for.</param>
    /// <returns>True if the promise phrase is found in the text wrapped in promise tags.</returns>
    public static bool DetectPromise(string text, string promisePhrase)
    {
        if (string.IsNullOrEmpty(promisePhrase))
            return false;

        var taggedPromise = $"<promise>{promisePhrase}</promise>";
        return text.Contains(taggedPromise, StringComparison.Ordinal);
    }
}