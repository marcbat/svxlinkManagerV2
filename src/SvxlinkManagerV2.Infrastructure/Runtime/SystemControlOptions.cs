namespace SvxlinkManagerV2.Infrastructure.Runtime;

/// <summary>
/// Options de contrôle de l'alimentation de la machine hôte (section "SystemControl").
/// </summary>
public class SystemControlOptions
{
    public const string SectionName = "SystemControl";

    /// <summary>
    /// Permet de désactiver complètement les actions d'alimentation depuis la configuration.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Active l'implémentation simulée (développement sans matériel : aucun appel système réel).
    /// </summary>
    public bool UseMock { get; set; }

    /// <summary>
    /// Commande shell exécutée pour redémarrer la machine.
    /// </summary>
    public string RebootCommand { get; set; } = "systemctl reboot";

    /// <summary>
    /// Commande shell exécutée pour arrêter la machine.
    /// </summary>
    public string ShutdownCommand { get; set; } = "systemctl poweroff";

    /// <summary>
    /// Délai laissé avant l'appel système, pour que la réponse atteigne le navigateur.
    /// </summary>
    public int DelayBeforeCommandSeconds { get; set; } = 3;
}
