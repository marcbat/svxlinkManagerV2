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
public class SvxLinkDaemonService : ISvxLinkDaemonService, IDisposable
{
    private readonly ILogger<SvxLinkDaemonService> _logger;
    private const int TimeoutSeconds = 30;
    private const string SvxLinkConfigPath = "/etc/svxlink/svxlink.conf";
    private Process? _svxlinkProcess;
    private readonly object _processLock = new();
    private bool _disposed;

    public SvxLinkDaemonService(ILogger<SvxLinkDaemonService> logger)
    {
        _logger = logger;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_processLock)
        {
            if (_svxlinkProcess != null && !_svxlinkProcess.HasExited)
            {
                try
                {
                    _logger.LogInformation("Arrêt du processus SVXLink lors du dispose");
                    _svxlinkProcess.Kill(entireProcessTree: true);
                    _svxlinkProcess.WaitForExit(5000);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erreur lors de l'arrêt du processus SVXLink");
                }
                finally
                {
                    _svxlinkProcess.Dispose();
                    _svxlinkProcess = null;
                }
            }
        }

        _disposed = true;
    }

    public async Task<Validation<Error, Unit>> RestartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Redémarrage du daemon SVXLink");

        try
        {
            // 1. Arrêter le processus s'il tourne
            lock (_processLock)
            {
                if (_svxlinkProcess != null && !_svxlinkProcess.HasExited)
                {
                    _logger.LogInformation("Arrêt du processus SVXLink existant (PID: {Pid})", _svxlinkProcess.Id);
                    try
                    {
                        _svxlinkProcess.Kill(entireProcessTree: true);
                        _svxlinkProcess.WaitForExit(5000);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Erreur lors de l'arrêt du processus SVXLink");
                    }
                    finally
                    {
                        _svxlinkProcess.Dispose();
                        _svxlinkProcess = null;
                    }
                }
            }
            
            // 2. Vérifier qu'aucun autre processus svxlink ne tourne
            var isRunning = await IsRunningAsync(cancellationToken);
            var isCurrentlyRunning = isRunning.Match(
                Succ: value => value,
                Fail: _ => false
            );
            
            if (isCurrentlyRunning)
            {
                _logger.LogInformation("Un processus SVXLink est encore actif, tentative d'arrêt via pkill");
                await ExecuteCommandAsync("pkill", "-TERM svxlink", cancellationToken);
                
                // Attendre que le processus se termine (max 5 secondes)
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(500, cancellationToken);
                    var stillRunning = await IsRunningAsync(cancellationToken);
                    if (!stillRunning.Match(Succ: v => v, Fail: _ => true))
                    {
                        break;
                    }
                }
            }
            
            // 3. Démarrer SVXLink en mode non-daemon pour capturer les logs
            _logger.LogInformation("Démarrage de SVXLink avec capture de logs");
            
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "svxlink",
                Arguments = $"--logfile=stdout --config={SvxLinkConfigPath}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = new Process { StartInfo = processStartInfo };
            
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    _logger.LogInformation("[SVXLink] {Output}", e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    _logger.LogWarning("[SVXLink Error] {Error}", e.Data);
                }
            };

            process.Exited += (sender, e) =>
            {
                _logger.LogWarning("Le processus SVXLink s'est terminé de manière inattendue");
            };

            process.EnableRaisingEvents = true;
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            lock (_processLock)
            {
                _svxlinkProcess = process;
            }

            _logger.LogInformation("SVXLink démarré (PID: {Pid})", process.Id);
            
            // 4. Attendre un peu et vérifier que le processus tourne toujours
            await Task.Delay(2000, cancellationToken);
            
            if (process.HasExited)
            {
                var errorMessage = $"SVXLink s'est terminé immédiatement avec le code {process.ExitCode}";
                _logger.LogError(errorMessage);
                return Validation<Error, Unit>.Fail(Seq1(Error.New(errorMessage)));
            }
            
            _logger.LogInformation("Daemon SVXLink redémarré avec succès");
            return Validation<Error, Unit>.Success(Unit.Default);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception lors du redémarrage du daemon SVXLink");
            return Validation<Error, Unit>.Fail(Seq1(Error.New(ex)));
        }
    }

    public async Task<Validation<Error, bool>> IsRunningAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Vérification de l'état du daemon SVXLink");

        try
        {
            // D'abord vérifier si on a un processus actif
            lock (_processLock)
            {
                if (_svxlinkProcess != null && !_svxlinkProcess.HasExited)
                {
                    _logger.LogDebug("Daemon SVXLink est actif (PID: {Pid})", _svxlinkProcess.Id);
                    return Validation<Error, bool>.Success(true);
                }
            }

            // Sinon, vérifier avec pgrep (au cas où le processus aurait été lancé autrement)
            var result = await ExecuteCommandAsync("pgrep", "-x svxlink", cancellationToken);
            
            // pgrep retourne 0 si le processus existe, 1 sinon
            bool isActive = result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput);
            
            _logger.LogDebug("Daemon SVXLink est {Status}", isActive ? "actif (via pgrep)" : "inactif");
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
