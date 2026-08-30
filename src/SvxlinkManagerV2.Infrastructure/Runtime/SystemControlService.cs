using System.Diagnostics;
using LanguageExt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Features.SystemControl;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Infrastructure.Runtime;

/// <summary>
/// Contrôle de l'alimentation de la machine hôte via une commande système (systemctl par défaut).
/// L'appel système est planifié en arrière-plan avec un court délai, afin que la réponse atteigne
/// le navigateur avant que la connexion ne soit coupée.
/// L'arrêt propre des daemons est orchestré en amont par les handlers de l'Application.
/// </summary>
public class SystemControlService : ISystemControlService
{
    /// <summary>
    /// Répertoires balayés pour retrouver un binaire déclaré sans chemin absolu.
    /// </summary>
    private static readonly string[] BinarySearchPaths =
    [
        "/usr/bin", "/bin", "/usr/sbin", "/sbin", "/usr/local/bin", "/usr/local/sbin"
    ];

    private readonly ILogger<SystemControlService> _logger;
    private readonly SystemControlOptions _options;

    public SystemControlService(
        IConfiguration configuration,
        ILogger<SystemControlService> logger)
    {
        _logger = logger;
        _options = configuration.GetSection(SystemControlOptions.SectionName).Get<SystemControlOptions>()
            ?? new SystemControlOptions();
    }

    public SystemControlAvailabilityDto GetAvailability()
    {
        if (!_options.Enabled)
        {
            return new SystemControlAvailabilityDto(
                IsSupported: false,
                IsSimulated: false,
                UnsupportedReason: "Le contrôle de l'alimentation est désactivé par la configuration (SystemControl:Enabled).");
        }

        if (!OperatingSystem.IsLinux())
        {
            return new SystemControlAvailabilityDto(
                IsSupported: false,
                IsSimulated: false,
                UnsupportedReason: "Le redémarrage et l'arrêt ne sont possibles que sur la cible Linux.");
        }

        if (IsRunningInContainer())
        {
            return new SystemControlAvailabilityDto(
                IsSupported: false,
                IsSimulated: false,
                UnsupportedReason: "L'application s'exécute dans un conteneur : elle ne peut ni redémarrer ni arrêter la machine hôte.");
        }

        var missingBinary = FindMissingBinary();
        if (missingBinary is not null)
        {
            return new SystemControlAvailabilityDto(
                IsSupported: false,
                IsSimulated: false,
                UnsupportedReason: $"La commande « {missingBinary} » est introuvable sur le système.");
        }

        return new SystemControlAvailabilityDto(IsSupported: true, IsSimulated: false, UnsupportedReason: null);
    }

    public Task<Validation<Error, Unit>> RebootAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(SchedulePowerAction(
            _options.RebootCommand,
            "redémarrage",
            "SYSTEM_CONTROL_REBOOT_ERROR"));

    public Task<Validation<Error, Unit>> ShutdownAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(SchedulePowerAction(
            _options.ShutdownCommand,
            "arrêt",
            "SYSTEM_CONTROL_SHUTDOWN_ERROR"));

    private Validation<Error, Unit> SchedulePowerAction(
        string command,
        string actionLabel,
        string errorCode)
    {
        var availability = GetAvailability();
        if (!availability.IsSupported)
        {
            _logger.LogWarning(
                "Action d'alimentation ({Action}) refusée : {Reason}",
                actionLabel, availability.UnsupportedReason);

            return Error.Validation(
                    "SYSTEM_CONTROL_UNSUPPORTED",
                    availability.UnsupportedReason ?? "Le contrôle de l'alimentation n'est pas disponible sur cette plateforme.")
                .ToFailure<Unit>();
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            return Error.Validation(
                    "SYSTEM_CONTROL_COMMAND_NOT_CONFIGURED",
                    $"Aucune commande n'est configurée pour le {actionLabel} de la machine.")
                .ToFailure<Unit>();
        }

        var delaySeconds = Math.Max(0, _options.DelayBeforeCommandSeconds);
        var shellCommand = delaySeconds > 0 ? $"sleep {delaySeconds}; {command}" : command;

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/sh",
                    Arguments = $"-c \"{shellCommand.Replace("\"", "\\\"")}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            _logger.LogWarning(
                "Le {Action} de la machine est planifié dans {Delay}s via « {Command} » (PID {Pid})",
                actionLabel, delaySeconds, command, process.Id);

            return Unit.Default.ToSuccess();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec du déclenchement du {Action} de la machine", actionLabel);

            return Error.Validation(
                    errorCode,
                    $"Le {actionLabel} de la machine n'a pas pu être déclenché : {ex.Message}")
                .ToFailure<Unit>();
        }
    }

    /// <summary>
    /// Détecte une exécution en conteneur : ni le redémarrage ni l'arrêt de l'hôte n'y sont possibles.
    /// </summary>
    private static bool IsRunningInContainer()
    {
        if (File.Exists("/.dockerenv"))
            return true;

        var flag = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
        return string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase) || flag == "1";
    }

    /// <summary>
    /// Retourne le premier binaire requis introuvable sur le système, ou null si tout est présent.
    /// </summary>
    private string? FindMissingBinary()
    {
        foreach (var command in new[] { _options.RebootCommand, _options.ShutdownCommand })
        {
            var binary = ExtractBinary(command);
            if (binary is not null && !BinaryExists(binary))
                return binary;
        }

        return null;
    }

    private static string? ExtractBinary(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return null;

        return command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
    }

    private static bool BinaryExists(string binary)
    {
        if (binary.Contains('/'))
            return File.Exists(binary);

        return BinarySearchPaths.Any(directory => File.Exists(Path.Combine(directory, binary)));
    }
}
