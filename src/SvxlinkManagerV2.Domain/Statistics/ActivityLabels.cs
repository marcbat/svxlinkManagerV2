namespace SvxlinkManagerV2.Domain.Statistics;

/// <summary>
/// Libellés partagés par l'écriture et la lecture de l'historique d'activité.
/// </summary>
public static class ActivityLabels
{
    /// <summary>
    /// Nom porté par les sessions passées hors salon. Écrit tel quel dans la session
    /// et réutilisé à l'affichage : les deux côtés doivent nommer la même chose pareil.
    /// </summary>
    public const string StandaloneSalonName = "Mode autonome";
}
