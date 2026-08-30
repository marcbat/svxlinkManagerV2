using System.Diagnostics;
using System.Text;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.Reflector;

/// <summary>
/// Implémentation du service de contrôle du daemon svxreflector.
/// Gère le processus svxreflector via System.Diagnostics.Process (compatible container Docker).
/// Singleton — le processus doit survivre entre les requêtes.
/// </summary>
public class ReflectorDaemonService : IReflectorDaemonService, IDisposable
{
    private readonly ILogger<ReflectorDaemonService> _logger;
    private readonly IReflectorLogService _logService;
    private const int TimeoutSeconds = 30;
    private const string ReflectorConfigPath = "/etc/svxlink/svxreflector.conf";
    private const string ReflectorBinaryPath = "/opt/svxlink-modern/bin/svxreflector";
    private Process? _reflectorProcess;
    private readonly object _processLock = new();
    private bool _disposed;

    public ReflectorDaemonService(
        ILogger<ReflectorDaemonService> logger,
        IReflectorLogService logService)
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
            if (_reflectorProcess != null && !_reflectorProcess.HasExited)
            {
                try
                {
                    _logger.LogInformation("Arrêt du processus svxreflector lors du dispose");
                    _reflectorProcess.Kill(entireProcessTree: true);
                    _reflectorProcess.WaitForExit(5000);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erreur lors de l'arrêt du processus svxreflector");
                }
                finally
                {
                    _reflectorProcess.Dispose();
                    _reflectorProcess = null;
                }
            }
        }

        _disposed = true;
    }

    public async Task<Validation<Error, Unit>> StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Arrêt du daemon svxreflector");

        try
        {
            lock (_processLock)
            {
                if (_reflectorProcess != null && !_reflectorProcess.HasExited)
                {
                    _logger.LogInformation("Arrêt du processus svxreflector (PID: {Pid})", _reflectorProcess.Id);
                    try
                    {
                        _reflectorProcess.Kill(entireProcessTree: true);
                        _reflectorProcess.WaitForExit(5000);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Erreur lors de l'arrêt du processus svxreflector");
                    }
                    finally
                    {
                        _reflectorProcess.Dispose();
                        _reflectorProcess = null;
                    }
                }
            }

            // Tuer tout processus svxreflector résiduel
            var isRunning = await IsRunningAsync(cancellationToken);
            if (isRunning.Match(Succ: v => v, Fail: _ => false))
            {
                _logger.LogInformation("Processus svxreflector résiduel détecté, arrêt via pkill -TERM");
                await ExecuteCommandAsync("pkill", "-TERM svxreflector", cancellationToken);
                await Task.Delay(2000, cancellationToken);
            }

            _logger.LogInformation("Daemon svxreflector arrêté avec succès");
            _logService.AddLog("--- Daemon svxreflector arrêté ---");
            return unit;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception lors de l'arrêt du daemon svxreflector");
            return Validation<Error, Unit>.Fail(Seq1(Error.New(ex)));
        }
    }

    public async Task<Validation<Error, Unit>> RestartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Démarrage/redémarrage du daemon svxreflector");

        try
        {
            // Arrêter le processus existant s'il tourne
            lock (_processLock)
            {
                if (_reflectorProcess != null && !_reflectorProcess.HasExited)
                {
                    _logger.LogInformation("Arrêt du processus svxreflector existant (PID: {Pid})", _reflectorProcess.Id);
                    try
                    {
                        _reflectorProcess.Kill(entireProcessTree: true);
                        _reflectorProcess.WaitForExit(5000);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Erreur lors de l'arrêt du processus svxreflector");
                    }
                    finally
                    {
                        _reflectorProcess.Dispose();
                        _reflectorProcess = null;
                    }
                }
            }

            // Vérifier qu'aucun processus résiduel ne tourne
            var isRunning = await IsRunningAsync(cancellationToken);
            if (isRunning.Match(Succ: v => v, Fail: _ => false))
            {
                _logger.LogInformation("Processus svxreflector résiduel détecté, arrêt via pkill -TERM");
                await ExecuteCommandAsync("pkill", "-TERM svxreflector", cancellationToken);

                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(500, cancellationToken);
                    var stillRunning = await IsRunningAsync(cancellationToken);
                    if (!stillRunning.Match(Succ: v => v, Fail: _ => true))
                        break;
                }
            }

            // Lancer svxreflector via /bin/bash avec chemin absolu
            _logger.LogInformation("Démarrage de svxreflector avec config {ConfigPath}", ReflectorConfigPath);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{ReflectorBinaryPath} --config={ReflectorConfigPath}\"",
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    RedirectStandardError = true,
                    StandardErrorEncoding = Encoding.UTF8,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            // svxreflector écrit sur stderr (comme svxlink)
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    _logger.LogInformation("[svxreflector] {Output}", e.Data);
                    _logService.AddLog(e.Data);
                }
            };

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    _logger.LogInformation("[svxreflector] {Output}", e.Data);
                    _logService.AddLog(e.Data);
                }
            };

            process.Exited += (sender, e) =>
            {
                _logger.LogWarning("Le processus svxreflector s'est terminé (code: {ExitCode})", process.ExitCode);
            };

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            lock (_processLock)
            {
                _reflectorProcess = process;
            }

            _logger.LogInformation("svxreflector démarré (PID: {Pid})", process.Id);

            // Attendre un peu et vérifier que le processus tourne toujours
            await Task.Delay(2000, cancellationToken);

            if (process.HasExited)
            {
                var errorMessage = $"svxreflector s'est terminé immédiatement avec le code {process.ExitCode}";
                _logger.LogError(errorMessage);
                return Validation<Error, Unit>.Fail(Seq1(Error.New(errorMessage)));
            }

            _logger.LogInformation("Daemon svxreflector démarré avec succès");
            _logService.AddLog("--- Daemon svxreflector démarré ---");
            return unit;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception lors du démarrage du daemon svxreflector");
            return Validation<Error, Unit>.Fail(Seq1(Error.New(ex)));
        }
    }

    public async Task<Validation<Error, bool>> IsRunningAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Vérification de l'état du daemon svxreflector");

        try
        {
            lock (_processLock)
            {
                if (_reflectorProcess != null && !_reflectorProcess.HasExited)
                {
                    _logger.LogDebug("Daemon svxreflector est actif (PID: {Pid})", _reflectorProcess.Id);
                    return Validation<Error, bool>.Success(true);
                }
            }

            // Vérifier via pgrep (processus lancé hors de notre contrôle)
            var result = await ExecuteCommandAsync("pgrep", "-x svxreflector", cancellationToken);
            bool isActive = result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput);

            _logger.LogDebug("Daemon svxreflector est {Status}", isActive ? "actif (via pgrep)" : "inactif");
            return Validation<Error, bool>.Success(isActive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception lors de la vérification de l'état du daemon svxreflector");
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

        using var process = new Process { StartInfo = processStartInfo };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cts.Token);

        return new ProcessResult(
            process.ExitCode,
            outputBuilder.ToString(),
            errorBuilder.ToString());
    }

    private record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
