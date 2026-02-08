using System.Diagnostics;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Implémentation réelle du service daemon SVXLink.
/// Interagit avec systemctl pour gérer le daemon SVXLink.
/// </summary>
public class SvxLinkDaemonService : ISvxLinkDaemonService
{
    private readonly ILogger<SvxLinkDaemonService> _logger;
    private const int TimeoutSeconds = 30;

    public SvxLinkDaemonService(ILogger<SvxLinkDaemonService> logger)
    {
        _logger = logger;
    }

    public async Task<Validation<Error, Unit>> RestartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Redémarrage du daemon SVXLink");

        try
        {
            var result = await ExecuteSystemctlCommandAsync("restart", "svxlink", cancellationToken);
            
            if (result.ExitCode == 0)
            {
                _logger.LogInformation("Daemon SVXLink redémarré avec succès");
                return Validation<Error, Unit>.Success(Unit.Default);
            }
            
            var errorMessage = $"Échec du redémarrage du daemon SVXLink. Exit code: {result.ExitCode}. Error: {result.StandardError}";
            _logger.LogError(errorMessage);
            return Validation<Error, Unit>.Fail(Seq1(Error.New(errorMessage)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception lors du redémarrage du daemon SVXLink");
            return Validation<Error, Unit>.Fail(Seq1(Error.New(ex)));
        }
    }

    public async Task<Validation<Error, bool>> IsRunningAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Vérification de l'état du daemon SVXLink");

        try
        {
            var result = await ExecuteSystemctlCommandAsync("is-active", "svxlink", cancellationToken);
            
            // systemctl is-active retourne 0 si le service est actif, non-zéro sinon
            bool isActive = result.ExitCode == 0 && result.StandardOutput.Trim() == "active";
            
            _logger.LogInformation("Daemon SVXLink est {Status}", isActive ? "actif" : "inactif");
            return Validation<Error, bool>.Success(isActive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception lors de la vérification de l'état du daemon SVXLink");
            return Validation<Error, bool>.Fail(Seq1(Error.New(ex)));
        }
    }

    private async Task<ProcessResult> ExecuteSystemctlCommandAsync(
        string command, 
        string serviceName, 
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "systemctl",
            Arguments = $"{command} {serviceName}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _logger.LogDebug("Exécution de la commande: systemctl {Command} {ServiceName}", command, serviceName);

        using var process = new Process { StartInfo = processStartInfo };
        
        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cts.Token);

        var result = new ProcessResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = outputBuilder.ToString(),
            StandardError = errorBuilder.ToString()
        };

        if (result.ExitCode != 0)
        {
            _logger.LogWarning(
                "La commande systemctl {Command} {ServiceName} a retourné le code {ExitCode}. Error: {Error}",
                command,
                serviceName,
                result.ExitCode,
                result.StandardError);
        }

        return result;
    }

    private sealed class ProcessResult
    {
        public int ExitCode { get; init; }
        public string StandardOutput { get; init; } = string.Empty;
        public string StandardError { get; init; } = string.Empty;
    }
}
