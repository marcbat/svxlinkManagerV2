namespace SvxlinkManagerV2.Domain.Statistics;

/// <summary>
/// Nature d'une session d'activité du nœud.
///
/// Le mode autonome n'est pas un salon au sens de <c>SalonAggregate</c> — aucun enregistrement
/// ne le représente en base — mais il occupe du temps d'antenne au même titre qu'un salon :
/// il est donc traité ici comme une nature de session à part entière, sans quoi le temps passé
/// déconnecté disparaîtrait des statistiques.
/// </summary>
public enum SalonKind
{
    /// <summary>Salon connecté à un réflecteur distant.</summary>
    Reflector = 0,

    /// <summary>Salon perroquet, simplex et autonome.</summary>
    Parrot = 1,

    /// <summary>Mode autonome : simplex sans réflecteur, à l'écoute des commandes DTMF.</summary>
    Standalone = 2
}
