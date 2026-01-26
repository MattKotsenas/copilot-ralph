// Shell completion script generators for various shells.

namespace Ralph.Cli.Commands;

/// <summary>
/// Generates shell completion scripts that use the built-in [suggest] directive.
/// </summary>
public static class CompletionScripts
{
    /// <summary>
    /// Supported shell types for completion.
    /// </summary>
    public static readonly string[] SupportedShells = ["pwsh", "bash", "zsh", "fish"];

    /// <summary>
    /// Gets the completion script for the specified shell.
    /// </summary>
    public static string GetScript(string shell) => shell.ToLowerInvariant() switch
    {
        "pwsh" or "powershell" => PowerShell,
        "bash" => Bash,
        "zsh" => Zsh,
        "fish" => Fish,
        _ => throw new ArgumentException($"Unsupported shell: {shell}. Supported shells: {string.Join(", ", SupportedShells)}")
    };

    /// <summary>
    /// PowerShell completion script using Register-ArgumentCompleter.
    /// </summary>
    public const string PowerShell = """
        # Ralph CLI completion for PowerShell
        Register-ArgumentCompleter -Native -CommandName ralph -ScriptBlock {
            param($wordToComplete, $commandAst, $cursorPosition)
            $command = $commandAst.ToString()
            ralph "[suggest:$cursorPosition]" $command 2>$null | ForEach-Object {
                [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
            }
        }
        """;

    /// <summary>
    /// Bash completion script.
    /// </summary>
    public const string Bash = """
        # Ralph CLI completion for Bash
        _ralph_bash_complete() {
            local cur="${COMP_WORDS[COMP_CWORD]}"
            local IFS=$'\n'
            local candidates

            read -d '' -ra candidates < <(ralph "[suggest:${COMP_POINT}]" "${COMP_LINE}" 2>/dev/null)
            read -d '' -ra COMPREPLY < <(compgen -W "${candidates[*]:-}" -- "$cur")
        }
        complete -F _ralph_bash_complete ralph
        """;

    /// <summary>
    /// Zsh completion script.
    /// </summary>
    public const string Zsh = """
        # Ralph CLI completion for Zsh
        _ralph_zsh_complete() {
            local pos=$((CURSOR + 1))
            local completions=("$(ralph "[suggest:${pos}]" "$BUFFER" 2>/dev/null)")

            if [ -z "$completions" ]; then
                _arguments '*::arguments: _normal'
                return
            fi

            _values = "${(ps:\n:)completions}"
        }
        compdef _ralph_zsh_complete ralph
        """;

    /// <summary>
    /// Fish completion script.
    /// </summary>
    public const string Fish = """
        # Ralph CLI completion for Fish
        complete -f -c ralph -a "(ralph '[suggest:'(commandline -C)']' (commandline) 2>/dev/null)"
        """;
}