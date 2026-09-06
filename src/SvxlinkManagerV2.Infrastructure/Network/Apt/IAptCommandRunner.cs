namespace SvxlinkManagerV2.Infrastructure.Network.Apt;

/// <summary>
/// Résultat brut d'une commande APT.
/// </summary>
/// <param name="ExitCode">Code de sortie du processus.</param>
/// <param name="StandardOutput">Sortie standard complète.</param>
/// <param name="StandardError">Sortie d'erreur complète.</param>
public record AptCommandResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;

    /// <summary>
    /// Message d'erreur exploitable : apt écrit ses diagnostics sur stderr, mais
    /// retombe sur stdout pour certaines erreurs de résolution.
    /// </summary>
    public string ErrorMessage =>
        !string.IsNullOrWhiteSpace(StandardError) ? StandardError.Trim()
        : !string.IsNullOrWhiteSpace(StandardOutput) ? StandardOutput.Trim()
        : $"La commande a échoué avec le code {ExitCode}.";
}

/// <summary>
/// Exécution des commandes APT et dpkg de la machine.
/// Abstraite pour que la logique de mise à jour reste testable sans système Debian.
/// </summary>
public interface IAptCommandRunner
{
    /// <summary>
    /// Exécute un binaire avec les arguments donnés.
    /// L'implémentation force une locale neutre : les libellés d'apt-cache sont
    /// traduits, et un système en français casserait toute analyse de la sortie.
    /// </summary>
    Task<AptCommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}
