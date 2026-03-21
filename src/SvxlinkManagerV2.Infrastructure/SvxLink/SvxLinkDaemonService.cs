using System.Diagnostics;
using System.Text;
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
    private readonly ISvxLinkLogService _logService;
    private const int TimeoutSeconds = 30;
    private const string SvxLinkConfigPath = "/etc/svxlink/svxlink.conf";
    private Process? _svxlinkProcess;
    private readonly object _processLock = new();
    private bool _disposed;

    public SvxLinkDaemonService(ILogger<SvxLinkDaemonService> logger, ISvxLinkLogService logService)
    {
        _logger = logger;
        _logService = logService;
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

    public async Task<Validation<Error, Unit>> StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Arrêt du daemon SVXLink");

        try
        {
            lock (_processLock)
            {
                if (_svxlinkProcess != null && !_svxlinkProcess.HasExited)
                {
                    _logger.LogInformation("Arrêt du processus SVXLink (PID: {Pid})", _svxlinkProcess.Id);
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

            // Tuer également tout processus svxlink résiduel
            var isRunning = await IsRunningAsync(cancellationToken);
            if (isRunning.Match(Succ: v => v, Fail: _ => false))
            {
                _logger.LogInformation("Processus SVXLink résiduel détecté, arrêt via pkill -TERM");
                await ExecuteCommandAsync("pkill", "-TERM svxlink", cancellationToken);
                await Task.Delay(2000, cancellationToken);
            }

            _logger.LogInformation("Daemon SVXLink arrêté avec succès");
            return unit;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception lors de l'arrêt du daemon SVXLink");
            return Validation<Error, Unit>.Fail(Seq1(Error.New(ex)));
        }
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
                        // Arrêt gracieux via SIGTERM (comme le legacy), puis force si nécessaire
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
            
            // 2. Vérifier qu'aucun autre processus svxlink ne tourne (lancé hors de notre contrôle)
            var isRunning = await IsRunningAsync(cancellationToken);
            var isCurrentlyRunning = isRunning.Match(
                Succ: value => value,
                Fail: _ => false
            );
            
            if (isCurrentlyRunning)
            {
                _logger.LogInformation("Un processus SVXLink résiduel détecté, arrêt via pkill -TERM");
                await ExecuteCommandAsync("pkill", "-TERM svxlink", cancellationToken);
                
                // Attendre que le processus se termine (max 5 secondes)
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(500, cancellationToken);
                    var stillRunning = await IsRunningAsync(cancellationToken);
                    if (!stillRunning.Match(Succ: v => v, Fail: _ => true))
                        break;
                }
            }
            
            // 3. Démarrer SVXLink via /bin/bash (comme le legacy) pour garantir le PATH
            //    et l'encodage UTF8. SVXLink écrit TOUT sur stderr (logs normaux inclus).
            _logger.LogInformation("Démarrage de SVXLink");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"svxlink --config={SvxLinkConfigPath}\"",
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    RedirectStandardError = true,
                    StandardErrorEncoding = Encoding.UTF8,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            // SVXLink écrit sa sortie sur stderr — les deux canaux vont en LogInformation
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    _logger.LogInformation("[SVXLink] {Output}", e.Data);
                    _logService.AddLog(e.Data);
                }
            };

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    _logger.LogInformation("[SVXLink] {Output}", e.Data);
                    _logService.AddLog(e.Data);
                }
            };

            process.Exited += (sender, e) =>
            {
                _logger.LogWarning("Le processus SVXLink s'est terminé (code: {ExitCode})", process.ExitCode);
            };

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

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
