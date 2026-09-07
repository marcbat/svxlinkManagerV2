namespace SvxlinkManagerV2.Domain.Aggregates.Reflector;

/// <summary>
/// Réglage que la configuration du réflecteur local devrait déclarer.
/// </summary>
/// <param name="Section">Section INI qui l'accueille, sans crochets.</param>
/// <param name="Key">Nom de la variable SVXLink.</param>
/// <param name="Value">Valeur recommandée.</param>
/// <param name="Rationale">Ce qui ne fonctionne pas sans elle, en une phrase.</param>
public record ReflectorRecommendedSetting(string Section, string Key, string Value, string Rationale);

/// <summary>
/// Réglages sans lesquels le réflecteur local est incomplet, et que l'application sait
/// proposer à une configuration existante.
/// </summary>
/// <remarks>
/// <para>
/// Le seeder ne crée le réflecteur que s'il n'en existe aucun : une installation déjà en
/// service ne reçoit jamais les clés ajoutées au modèle par la suite. C'est arrivé trois
/// fois de suite — <c>HTTP_SRV_PORT</c>, puis <c>COMMAND_PTY</c>, puis les réglages de
/// talkgroup ci-dessous — et chaque fois la fonctionnalité correspondante se dégradait
/// silencieusement sur les nœuds existants.
/// </para>
/// <para>
/// Cette liste est la référence commune au modèle par défaut et au diagnostic proposé sur la
/// page Réflecteur. Y ajouter une entrée suffit à ce qu'une installation existante se la
/// voie proposer.
/// </para>
/// </remarks>
public static class ReflectorRecommendedSettings
{
    /// <summary>Section principale de <c>svxreflector.conf</c>.</summary>
    public const string GlobalSection = "GLOBAL";

    /// <summary>
    /// Talkgroup auquel le réflecteur rattache les nœuds en protocole V1/V2.
    /// </summary>
    /// <remarks>
    /// Sur un réflecteur local le numéro est libre : celui-ci est repris de la stack de test,
    /// pour que documentation, tests et configuration livrée parlent du même talkgroup.
    /// </remarks>
    public const int LegacyClientsTalkGroup = 240;

    /// <summary>
    /// Plage de tirage des QSY aléatoires, convention <c>&lt;MCC&gt;9900:100</c>.
    /// </summary>
    /// <remarks>
    /// 228 est le MCC suisse ; la France utilise 208, soit <c>2089900:100</c>. Le choix n'a
    /// d'importance que sur un réflecteur ouvert à d'autres nœuds, où il évite qu'un QSY
    /// tombe sur un talkgroup déjà utilisé ailleurs.
    /// </remarks>
    public const string RandomQsyRange = "2289900:100";

    /// <summary>
    /// Réglages proposés à une configuration qui ne les déclare pas.
    /// </summary>
    public static IReadOnlyList<ReflectorRecommendedSetting> All { get; } =
    [
        new(GlobalSection, "TG_FOR_V1_CLIENTS", LegacyClientsTalkGroup.ToString(),
            "Sans elle, un nœud en protocole V2 ne peut participer à aucun talkgroup et reste "
            + "muet dès qu'un talkgroup est utilisé sur le réflecteur."),

        new(GlobalSection, "RANDOM_QSY_RANGE", RandomQsyRange,
            "Sans elle, le QSY aléatoire — et donc AUTO_QSY_AFTER — ne peut pas fonctionner."),

        new(GlobalSection, "SQL_TIMEOUT", "300",
            "Coupe l'audio d'un nœud qui émet plus de cinq minutes : la parade à un émetteur "
            + "resté bloqué."),

        new(GlobalSection, "SQL_TIMEOUT_BLOCKTIME", "60",
            "Maintient muet le nœud dont l'émission a été coupée, le temps que son opérateur "
            + "s'en aperçoive."),

        new(GlobalSection, "HTTP_SRV_PORT", "8888",
            "Serveur de statut lu par la page Réflecteur pour lister les nœuds connectés et "
            + "leur talkgroup."),

        new(GlobalSection, "COMMAND_PTY", "/tmp/reflector_ctrl",
            "Sans lui, l'application ne peut pas signer les demandes de certificat : elles "
            + "restent en attente et les nœuds V3 ne se connectent jamais.")
    ];

    /// <summary>
    /// Réglages recommandés que la configuration donnée ne déclare pas.
    /// </summary>
    /// <param name="config">Contenu INI brut de <c>svxreflector.conf</c>.</param>
    public static IReadOnlyList<ReflectorRecommendedSetting> MissingFrom(string? config)
    {
        if (string.IsNullOrWhiteSpace(config))
            return All;

        return All.Where(setting => !Declares(config, setting)).ToList();
    }

    /// <summary>
    /// La clé est-elle déclarée dans sa section ?
    /// </summary>
    /// <remarks>
    /// Une clé commentée ne compte pas comme déclarée : c'est le comportement du démon, et
    /// c'est aussi ce qu'attend un opérateur qui a délibérément mis un dièse devant.
    /// </remarks>
    private static bool Declares(string config, ReflectorRecommendedSetting setting)
    {
        var currentSection = string.Empty;

        foreach (var rawLine in config.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1].Trim();
                continue;
            }

            if (!currentSection.Equals(setting.Section, StringComparison.OrdinalIgnoreCase))
                continue;

            var separator = line.IndexOf('=');
            if (separator > 0 &&
                line[..separator].Trim().Equals(setting.Key, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
