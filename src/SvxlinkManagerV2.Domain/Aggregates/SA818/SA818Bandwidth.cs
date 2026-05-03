namespace SvxlinkManagerV2.Domain.Aggregates.SA818;

/// <summary>
/// Enum représentant les largeurs de bande (bandwidth) supportées par le module SA818.
/// Ces valeurs correspondent aux codes attendus dans la commande AT+DMOSETGROUP.
/// </summary>
public enum SA818Bandwidth
{
    /// <summary>
    /// Bande étroite 12.5 kHz (NFM - Narrow FM)
    /// Commande AT: 0
    /// </summary>
    Narrow12_5kHz = 0,

    /// <summary>
    /// Bande large 25 kHz (Wide FM)
    /// Commande AT: 1
    /// Utilisé par défaut pour la bande radioamateur VHF/UHF
    /// </summary>
    Wide25kHz = 1
}
