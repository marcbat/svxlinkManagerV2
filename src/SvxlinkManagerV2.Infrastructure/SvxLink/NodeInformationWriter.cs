using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.GeneralConfiguration.Entities;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Écrit le document <c>node_info.json</c> publié au réflecteur.
/// </summary>
/// <remarks>
/// <para>
/// La structure suit le modèle de référence livré avec SVXLink : <c>nodeLocation</c>,
/// <c>nodeClass</c>, <c>hidden</c>, <c>sysop</c>, <c>toneToTalkgroup</c> et un tableau
/// <c>qth</c> portant position, récepteurs et émetteurs.
/// </para>
/// <para>
/// <b>Seul ce que l'application connaît est publié.</b> Le modèle de référence décrit aussi
/// les antennes — hauteur, azimut — et la puissance d'émission, que rien dans la
/// configuration ne modélise. Le format étant libre, ces clés sont omises plutôt que
/// remplies de valeurs inventées ou réclamées à l'opérateur dans un formulaire qu'il ne
/// remplirait pas.
/// </para>
/// <para>
/// Le fichier est réécrit à chaque activation de salon : c'est une projection de la
/// configuration, pas un état à préserver.
/// </para>
/// </remarks>
public class NodeInformationWriter : INodeInformationWriter
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        // Les noms de lieu comportent des accents : les échapper rendrait le document
        // illisible dans les annuaires qui l'affichent tel quel.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ILogger<NodeInformationWriter> _logger;
    private readonly IGeneralConfigurationRepository _generalConfigurationRepository;

    public NodeInformationWriter(
        ILogger<NodeInformationWriter> logger,
        IGeneralConfigurationRepository generalConfigurationRepository)
    {
        _logger = logger;
        _generalConfigurationRepository = generalConfigurationRepository;
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, Unit>> WriteAsync(
        SalonAggregate salon,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var information = (await _generalConfigurationRepository.GetAsync(cancellationToken))?.NodeInformation
                              ?? NodeInformation.Empty;

            var document = Build(salon, information);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(
                outputPath, document.ToJsonString(WriteOptions), cancellationToken);

            _logger.LogInformation("Informations du nœud publiées dans {Path}", outputPath);
            return Validation<Error, Unit>.Success(Unit.Default);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec de l'écriture des informations du nœud dans {Path}", outputPath);
            return Validation<Error, Unit>.Fail(Seq1(Error.New(ex)));
        }
    }

    /// <summary>
    /// Construit le document. Exposé aux tests : c'est la forme du JSON qui compte, pas
    /// l'écriture du fichier.
    /// </summary>
    internal static JsonObject Build(SalonAggregate salon, NodeInformation information)
    {
        var config = salon.Configuration;

        var document = new JsonObject
        {
            ["nodeClass"] = information.ClassName,
            ["hidden"] = information.Hidden
        };

        AddIfPresent(document, "nodeLocation", information.Location);
        AddIfPresent(document, "sysop", information.Sysop);

        // La tonalité reçue commande le talkgroup activé. L'application n'en connaît qu'une
        // seule, celle du salon : la table est donc dégénérée, là où le modèle de référence
        // en montre plusieurs. Sans CTCSS en réception, l'association n'a pas de sens.
        if (config.RxCtcss is { } rxCtcss)
            document["toneToTalkgroup"] = new JsonObject
            {
                [Format(rxCtcss)] = config.ReflectorProtocol == ReflectorProtocol.V3 ? config.DefaultTg : 0
            };

        document["qth"] = new JsonArray(BuildQth(salon, information));

        return document;
    }

    private static JsonObject BuildQth(SalonAggregate salon, NodeInformation information)
    {
        var config = salon.Configuration;

        var qth = new JsonObject { ["name"] = information.Location ?? config.Callsign };

        if (information.HasPosition)
        {
            var position = new JsonObject();

            if (information.Latitude is { } latitude && information.Longitude is { } longitude)
            {
                position["lat"] = latitude;
                position["long"] = longitude;
            }

            AddIfPresent(position, "loc", information.Locator);
            qth["pos"] = position;
        }

        // Les récepteurs et émetteurs sont indexés par lettre dans le format de référence.
        // Le nœud n'en a qu'un de chaque : « A ».
        qth["rx"] = new JsonObject { ["A"] = BuildReceiver(salon) };
        qth["tx"] = new JsonObject { ["A"] = BuildTransmitter(salon) };

        return qth;
    }

    private static JsonObject BuildReceiver(SalonAggregate salon)
    {
        var config = salon.Configuration;

        var receiver = new JsonObject
        {
            ["name"] = "Rx1",
            ["freq"] = decimal.ToDouble(config.RxFrequency),
            ["sqlType"] = config.RxCtcss.HasValue ? "CTCSS" : "COS"
        };

        // Tableau même pour une seule tonalité : c'est ce qu'attend le format côté réception.
        if (config.RxCtcss is { } rxCtcss)
            receiver["ctcssFreq"] = new JsonArray(JsonValue.Create(decimal.ToDouble(rxCtcss)));

        return receiver;
    }

    private static JsonObject BuildTransmitter(SalonAggregate salon)
    {
        var config = salon.Configuration;

        var transmitter = new JsonObject
        {
            ["name"] = "Tx1",
            ["freq"] = decimal.ToDouble(config.TxFrequency)
        };

        // Valeur unique en émission, contrairement à la réception.
        if (config.TxCtcss is { } txCtcss)
            transmitter["ctcssFreq"] = decimal.ToDouble(txCtcss);

        return transmitter;
    }

    private static void AddIfPresent(JsonObject target, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            target[key] = value.Trim();
    }

    /// <summary>
    /// Clé de <c>toneToTalkgroup</c> : la tonalité en hertz, à une décimale, point décimal.
    /// </summary>
    private static string Format(decimal tone) =>
        tone.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
}
