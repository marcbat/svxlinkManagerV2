using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using LanguageExt;
using MediatR;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Features.SystemStatus;
using SvxlinkManagerV2.Application.Features.SystemStatus.GetSystemStatus;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Common;
using SvxlinkManagerV2.Domain.Wifi;

namespace SvxlinkManagerV2.Application.Features.Diagnostics.CreateDiagnosticArchive;

/// <summary>
/// Commande de constitution de l'archive de diagnostic téléchargeable depuis la page Paramètres.
/// </summary>
public record CreateDiagnosticArchiveCommand() : IRequest<Validation<Error, DiagnosticArchiveDto>>;

/// <summary>
/// Handler pour CreateDiagnosticArchiveCommand.
/// Rassemble les logs des deux daemons, la configuration SVXLink générée et les informations
/// de version et d'état système dans une archive ZIP construite en mémoire.
/// Chaque source est facultative : une source indisponible est remplacée par une note dans
/// l'archive plutôt que de faire échouer l'export, dont l'intérêt est justement de documenter
/// une installation en défaut. Tout contenu textuel traverse
/// <see cref="DiagnosticSecretRedactor"/> avant d'être écrit, l'archive étant destinée à être
/// transmise à un tiers.
/// </summary>
public class CreateDiagnosticArchiveCommandHandler
    : IRequestHandler<CreateDiagnosticArchiveCommand, Validation<Error, DiagnosticArchiveDto>>
{
    /// <summary>Nom de l'entrée contenant les logs SVXLink.</summary>
    public const string SvxLinkLogsEntryName = "logs-svxlink.txt";

    /// <summary>Nom de l'entrée contenant les logs du réflecteur.</summary>
    public const string ReflectorLogsEntryName = "logs-reflector.txt";

    /// <summary>Nom de l'entrée contenant la configuration SVXLink générée, expurgée.</summary>
    public const string ConfigurationEntryName = "svxlink.conf";

    /// <summary>Nom de l'entrée contenant les versions et l'état de la machine.</summary>
    public const string SystemInformationEntryName = "informations-systeme.txt";

    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly ISvxLinkLogService _svxLinkLogService;
    private readonly IReflectorLogService _reflectorLogService;
    private readonly ISvxLinkConfigurationReader _configurationReader;
    private readonly ISender _sender;
    private readonly ILogger<CreateDiagnosticArchiveCommandHandler> _logger;

    public CreateDiagnosticArchiveCommandHandler(
        ISvxLinkLogService svxLinkLogService,
        IReflectorLogService reflectorLogService,
        ISvxLinkConfigurationReader configurationReader,
        ISender sender,
        ILogger<CreateDiagnosticArchiveCommandHandler> logger)
    {
        _svxLinkLogService = svxLinkLogService;
        _reflectorLogService = reflectorLogService;
        _configurationReader = configurationReader;
        _sender = sender;
        _logger = logger;
    }

    public async Task<Validation<Error, DiagnosticArchiveDto>> Handle(
        CreateDiagnosticArchiveCommand command,
        CancellationToken cancellationToken)
    {
        var generatedAt = DateTime.Now;

        try
        {
            var svxLinkLogs = DiagnosticLogFormatter.Format(
                "SVXLink", _svxLinkLogService.GetLogs(), searchTerm: null, generatedAt);

            var reflectorLogs = DiagnosticLogFormatter.Format(
                "Reflector", _reflectorLogService.GetLogs(), searchTerm: null, generatedAt);

            var configuration = await BuildConfigurationEntryAsync(cancellationToken);
            var systemInformation = await BuildSystemInformationEntryAsync(generatedAt, cancellationToken);

            using var buffer = new MemoryStream();

            using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                await WriteEntryAsync(archive, SvxLinkLogsEntryName, svxLinkLogs, generatedAt, cancellationToken);
                await WriteEntryAsync(archive, ReflectorLogsEntryName, reflectorLogs, generatedAt, cancellationToken);
                await WriteEntryAsync(archive, ConfigurationEntryName, configuration, generatedAt, cancellationToken);
                await WriteEntryAsync(archive, SystemInformationEntryName, systemInformation, generatedAt, cancellationToken);
            }

            _logger.LogInformation("Archive de diagnostic générée ({Size} octets)", buffer.Length);

            return new DiagnosticArchiveDto(BuildFileName(generatedAt), buffer.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec de la constitution de l'archive de diagnostic");

            return Error
                .Validation("DIAGNOSTIC_ARCHIVE_ERROR", "Impossible de constituer l'archive de diagnostic")
                .ToFailure<DiagnosticArchiveDto>();
        }
    }

    /// <summary>
    /// Nom de l'archive : horodaté, pour distinguer plusieurs envois d'un même utilisateur.
    /// </summary>
    internal static string BuildFileName(DateTime generatedAt)
        => $"diagnostic-svxlinkmanager-{generatedAt:yyyyMMdd-HHmmss}.zip";

    /// <summary>
    /// Lit la configuration SVXLink déployée et l'expurge de ses secrets.
    /// </summary>
    private async Task<string> BuildConfigurationEntryAsync(CancellationToken cancellationToken)
    {
        var header = new StringBuilder()
            .AppendLine($"# Configuration SVXLink générée : {_configurationReader.ConfigurationPath}")
            .AppendLine($"# Les valeurs sensibles ont été remplacées par {DiagnosticSecretRedactor.RedactedValue}")
            .AppendLine()
            .ToString();

        var content = await _configurationReader.ReadAsync(cancellationToken);

        return content.Match(
            Succ: raw => header + DiagnosticSecretRedactor.Redact(raw),
            Fail: errors => header + $"# Configuration indisponible : {FormatErrors(errors)}{Environment.NewLine}");
    }

    /// <summary>
    /// Compose le relevé de versions et d'état système, en dégradant proprement si la
    /// collecte échoue : l'archive doit rester exploitable même sur une plateforme partielle.
    /// </summary>
    private async Task<string> BuildSystemInformationEntryAsync(
        DateTime generatedAt,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Informations système — SvxLink Manager V2");
        builder.AppendLine($"# Archive générée le {generatedAt.ToString("dd/MM/yyyy à HH:mm:ss", French)}");
        builder.AppendLine();

        SystemStatusDto status;
        try
        {
            status = await _sender.Send(new GetSystemStatusQuery(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Collecte de l'état système impossible pour l'archive de diagnostic");
            builder.AppendLine($"État système indisponible : {ex.Message}");
            return builder.ToString();
        }

        builder.AppendLine($"Version de l'application : {status.ApplicationVersion}");
        builder.AppendLine($"Système d'exploitation   : {Environment.OSVersion}");
        builder.AppendLine($"Architecture             : {RuntimeInformation.OSArchitecture}");
        builder.AppendLine();

        builder.AppendLine("## Installations SVXLink");
        foreach (var installation in status.SvxLinkInstallations)
        {
            builder.AppendLine(
                $"- {installation.Name} : version {installation.Version}, protocole {installation.Protocol}, " +
                $"{(installation.IsInstalled ? "installée" : "absente")} ({installation.BinaryPath})");
        }

        builder.AppendLine();
        builder.AppendLine("## État de la machine");
        builder.AppendLine($"Collecte                 : {status.CollectedAt.LocalDateTime.ToString("dd/MM/yyyy à HH:mm:ss", French)}");
        builder.AppendLine($"Température CPU          : {Describe(status.CpuTemperatureCelsius, v => $"{v.ToString("F1", French)} °C")}");
        builder.AppendLine($"Charge CPU               : {Describe(status.CpuLoad, l => $"{l.Load1.ToString("F2", French)} / {l.Load5.ToString("F2", French)} / {l.Load15.ToString("F2", French)} sur {l.CoreCount} cœur(s)")}");
        builder.AppendLine($"Mémoire                  : {Describe(status.Memory, m => $"{FormatBytes(m.UsedBytes)} utilisés sur {FormatBytes(m.TotalBytes)} ({m.UsedPercent.ToString("F1", French)} %)")}");
        builder.AppendLine($"Uptime machine           : {Describe(status.Uptime, u => FormatDuration(u.Machine))}");
        builder.AppendLine($"Uptime application       : {Describe(status.Uptime, u => FormatDuration(u.Process))}");
        builder.AppendLine($"Réseau                   : {Describe(status.Network, FormatNetwork)}");

        foreach (var disk in status.Disks)
        {
            builder.AppendLine(
                $"{disk.Label} ({disk.Path}) : " +
                Describe(disk.Metric, d => $"{FormatBytes(d.UsedBytes)} utilisés sur {FormatBytes(d.TotalBytes)} ({d.UsedPercent.ToString("F1", French)} %)"));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Écrit une entrée texte dans l'archive, en UTF-8.
    /// </summary>
    private static async Task WriteEntryAsync(
        ZipArchive archive,
        string entryName,
        string content,
        DateTime generatedAt,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        entry.LastWriteTime = generatedAt;

        await using var stream = entry.Open();
        await stream.WriteAsync(Encoding.UTF8.GetBytes(content), cancellationToken);
    }

    /// <summary>
    /// Restitue la valeur d'une métrique, ou la raison de son indisponibilité.
    /// </summary>
    private static string Describe<T>(SystemMetric<T> metric, Func<T, string> formatter) where T : class
        => metric.Value is null
            ? $"indisponible ({metric.UnavailableReason})"
            : formatter(metric.Value);

    /// <summary>
    /// Variante de <see cref="Describe{T}"/> pour les métriques numériques.
    /// </summary>
    private static string Describe(SystemValueMetric metric, Func<double, string> formatter)
        => metric.Value is null
            ? $"indisponible ({metric.UnavailableReason})"
            : formatter(metric.Value.Value);

    private static string FormatNetwork(WifiLink link)
    {
        if (!link.IsConnected)
            return "déconnecté";

        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(link.InterfaceName)) details.Add($"interface {link.InterfaceName}");
        if (!string.IsNullOrWhiteSpace(link.Ssid)) details.Add($"SSID {link.Ssid}");
        if (!string.IsNullOrWhiteSpace(link.IpAddress)) details.Add($"IP {link.IpAddress}");
        if (link.SignalPercent.HasValue) details.Add($"signal {link.SignalPercent} %");

        return details.Count == 0 ? "connecté" : $"connecté ({string.Join(", ", details)})";
    }

    private static string FormatBytes(long bytes)
    {
        const double Mega = 1024d * 1024d;
        const double Giga = Mega * 1024d;

        return bytes >= Giga
            ? $"{(bytes / Giga).ToString("F2", French)} Go"
            : $"{(bytes / Mega).ToString("F0", French)} Mo";
    }

    private static string FormatDuration(TimeSpan duration)
        => $"{(int)duration.TotalDays} j {duration.Hours} h {duration.Minutes} min";

    private static string FormatErrors(Seq<Error> errors)
    {
        var message = string.Join(" | ", errors.Select(e => e.Message));
        return string.IsNullOrWhiteSpace(message) ? "raison inconnue" : message;
    }
}
