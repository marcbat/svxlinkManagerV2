namespace SvxlinkManagerV2.Application.Features.Statistics;

/// <summary>
/// Fenêtre d'observation demandée par l'opérateur.
/// </summary>
public enum StatisticsPeriod
{
    /// <summary>Les vingt-quatre dernières heures.</summary>
    Last24Hours = 0,

    /// <summary>Les sept derniers jours.</summary>
    Last7Days = 1,

    /// <summary>Les trente derniers jours.</summary>
    Last30Days = 2,

    /// <summary>Tout l'historique conservé.</summary>
    All = 3
}

/// <summary>
/// Conversion d'une période en borne de début.
/// </summary>
public static class StatisticsPeriodExtensions
{
    /// <summary>
    /// Début de la fenêtre, en UTC. <see cref="StatisticsPeriod.All"/> remonte à
    /// <see cref="DateTimeOffset.MinValue"/> : la rétention borne déjà l'historique.
    /// </summary>
    /// <param name="period">Période demandée.</param>
    /// <param name="now">Instant de référence.</param>
    public static DateTimeOffset ToStartUtc(this StatisticsPeriod period, DateTimeOffset now) => period switch
    {
        StatisticsPeriod.Last24Hours => now.ToUniversalTime().AddDays(-1),
        StatisticsPeriod.Last7Days => now.ToUniversalTime().AddDays(-7),
        StatisticsPeriod.Last30Days => now.ToUniversalTime().AddDays(-30),
        _ => DateTimeOffset.MinValue
    };

    /// <summary>Libellé français de la période, pour l'interface et les exports.</summary>
    /// <param name="period">Période demandée.</param>
    public static string ToLabel(this StatisticsPeriod period) => period switch
    {
        StatisticsPeriod.Last24Hours => "24 heures",
        StatisticsPeriod.Last7Days => "7 jours",
        StatisticsPeriod.Last30Days => "30 jours",
        _ => "Tout l'historique"
    };
}
