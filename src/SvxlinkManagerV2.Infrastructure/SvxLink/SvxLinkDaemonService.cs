using System.Diagnostics;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Implémentation réelle du service daemon SVXLink.
/// Gère le daemon SVXLink via des commandes directes (compatible container Docker).
/// </summary>
public class SvxLinkDaemonService : ISvxLinkDaemonService
{
    private readonly ILogger<SvxLinkDaemonService> _logger;
    private const int TimeoutSeconds = 30;
    private const string SvxLinkConfigPath = "/etc/svxlink/svxlink.conf";

    public SvxLinkDaemonService(ILogger<SvxLinkDaemonService> logger)
    {
        _logger = logger;
    }

    public async Task<Validation<Error, Unit>> RestartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Redémarrage du daemon SVXLink");

        try
        {
            // 1. Arrêter le processus s'il tourne
            var isRunning = await IsRunningAsync(cancellationToken);
            var isCurrentlyRunning = isRunning.Match(
                Succ: value => value,
                Fail: _ => false
            );
            
            if (isCurrentlyRunning)
            {
                _logger.LogInformation("Arrêt du daemon SVXLink en cours d'exécution");
                var stopResult = await ExecuteCommandAsync("pkill", "-TERM svxlink", cancellationToken);
                
                // Attendre que le processus se termine (max 5 secondes)
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(500, cancellationToken);
                    var stillRunning = await IsRunningAsync(cancellationToken);
                    var isStillRunning = stillRunning.Match(
                        Succ: value => value,
                        Fail: _ => true // En cas d'erreur, on suppose qu'il tourne encore
                    );
                    
                    if (!isStillRunning)
                    {
                        break;
                    }
                }
            }
            
            // 2. Démarrer le daemon SVXLink
            _logger.LogInformation("Démarrage du daemon SVXLink");
            var startResult = await ExecuteCommandAsync("svxlink", $"--daemon --config={SvxLinkConfigPath}", cancellationToken);
            
            if (startResult.ExitCode == 0)
            {
                // Attendre un peu que le daemon démarre
                await Task.Delay(1000, cancellationToken);
                
                // Vérifier qu'il tourne bien
                var checkRunning = await IsRunningAsync(cancellationToken);
                var isDaemonRunning = checkRunning.Match(
                    Succ: value => value,
                    Fail: _ => false
                );
                
                if (isDaemonRunning)
                {
                    _logger.LogInformation("Daemon SVXLink redémarré avec succès");
                    return Validation<Error, Unit>.Success(Unit.Default);
                }
                
                var errorMessage = "Le daemon SVXLink n'a pas démarré correctement";
                _logger.LogError(errorMessage);
                return Validation<Error, Unit>.Fail(Seq1(Error.New(errorMessage)));
            }
            
            var error = $"Échec du démarrage du daemon SVXLink. Exit code: {startResult.ExitCode}. Error: {startResult.StandardError}";
            _logger.LogError(error);
            return Validation<Error, Unit>.Fail(Seq1(Error.New(error)));
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
            // Utilise pgrep pour vérifier si le processus svxlink est actif
            var result = await ExecuteCommandAsync("pgrep", "-x svxlink", cancellationToken);
            
            // pgrep retourne 0 si le processus existe, 1 sinon
            bool isActive = result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput);
            
            _logger.LogInformation("Daemon SVXLink est {Status}", isActive ? "actif" : "inactif");
            return Validation<Error, bool>.Success(isActive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception lors de la vérification de l'état du daemon SVXLink");
            return Validation<Error, bool>.Fail(Seq1(Error.New(ex)));
        }
    }

    private async Task<ProcessResult> ExecuteCommandAsync(
        string command, 
        string arguments, 
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

        var processStartInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _logger.LogDebug("Exécution de la commande: {Command} {Arguments}", command, arguments);

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
            _logger.LogDebug(
                "La commande {Command} {Arguments} a retourné le code {ExitCode}. Output: {Output}, Error: {Error}",
                command,
                arguments,
                result.ExitCode,
                result.StandardOutput,
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
