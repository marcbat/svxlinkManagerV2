namespace SvxlinkManagerV2.Application.Features.Statistics;

/// <summary>
/// Paramètres de l'historique d'activité, section <c>Statistics</c> des appsettings.
/// </summary>
public class StatisticsOptions
{
    /// <summary>Nom de la section de configuration.</summary>
    public const string SectionName = "Statistics";

    /// <summary>
    /// Durée de conservation des enregistrements, en jours. Au-delà, ils sont supprimés.
    /// La cible est une carte SD : l'historique ne doit pas croître sans fin.
    /// </summary>
    public int RetentionDays { get; set; } = 90;

    /// <summary>Intervalle entre deux passages de purge, en heures.</summary>
    public int PurgeIntervalHours { get; set; } = 12;

    /// <summary>Nombre d'indicatifs retenus dans le palmarès du trafic.</summary>
    public int TopCallsignCount { get; set; } = 10;

    /// <summary>Nombre de codes DTMF retenus dans le palmarès des commandes.</summary>
    public int TopDtmfCodeCount { get; set; } = 10;

    /// <summary>Nombre d'événements affichés dans la chronologie.</summary>
    public int TimelineEntryCount { get; set; } = 200;
}
