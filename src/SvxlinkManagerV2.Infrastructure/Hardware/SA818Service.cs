using System.Diagnostics;
using System.Runtime.InteropServices;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Infrastructure.Hardware;

/// <summary>
/// Implémentation réelle du service de communication avec le module SA818.
/// Utilise le port série pour envoyer des commandes AT.
/// </summary>
public class SA818Service : ISA818Service, IDisposable
{
    private readonly ILogger<SA818Service> _logger;
    private readonly string _serialPort;
    private readonly int _baudRate;
    private readonly int _readTimeout;
    private readonly int _writeTimeout;
    private readonly int _commandDelay;
    private bool _portConfigured;

    public SA818Service(IConfiguration configuration, ILogger<SA818Service> logger)
    {
        _logger = logger;
        _serialPort = configuration["SA818:SerialPort"] ?? "/dev/ttyS2";
        _baudRate = configuration.GetValue<int>("SA818:BaudRate", 9600);
        _readTimeout = configuration.GetValue<int>("SA818:ReadTimeout", 2000);
        _writeTimeout = configuration.GetValue<int>("SA818:WriteTimeout", 2000);
        _commandDelay = configuration.GetValue<int>("SA818:CommandDelay", 1000);
    }

    public async Task<Validation<Error, Unit>> ConfigureAsync(SA818CommandSet commands, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Démarrage de la configuration du module SA818");

            // Ouvrir le port série
            var openResult = await OpenPortAsync(cancellationToken);
            if (openResult.IsFail)
            {
                return openResult;
            }

            // Envoyer les 3 commandes AT dans l'ordre
            var commands_list = new[] { commands.DmoSetGroup, commands.DmoSetVolume, commands.SetFilter };
            
            foreach (var command in commands_list)
            {
                if (string.IsNullOrWhiteSpace(command))
                {
                    _logger.LogWarning("Commande vide ignorée");
                    continue;
                }

                var result = await SendCommandAsync(command, cancellationToken);
                if (result.IsFail)
                {
                    ClosePort();
                    return Error.New(500, "Échec de l'envoi d'une commande");
                }

                // Délai inter-commandes
                await Task.Delay(_commandDelay, cancellationToken);
            }

            ClosePort();
            _logger.LogInformation("Configuration du module SA818 terminée avec succès");
            return Unit.Default;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Configuration du SA818 annulée");
            ClosePort();
            return Error.New(499, "Configuration annulée");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la configuration du SA818");
            ClosePort();
            return Error.New(500, "Erreur lors de la configuration", ex);
        }
    }

    public async Task<Validation<Error, bool>> IsConnectedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var openResult = await OpenPortAsync(cancellationToken);
            if (openResult.IsFail)
            {
                return false;
            }

            // Envoyer une commande simple pour tester la connexion (ex: AT)
            var result = await SendCommandAsync("AT", cancellationToken);
            ClosePort();

            return result.Match(
                Succ: _ => true,
                Fail: _ => false
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la vérification de connexion du SA818");
            ClosePort();
            return Error.New(500, "Erreur lors de la vérification de connexion", ex);
        }
    }

    private Task<Validation<Error, Unit>> OpenPortAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return Task.FromResult<Validation<Error, Unit>>(
                    Error.New(500, "SA818Service réel supporté uniquement sous Linux"));
            }

            if (_portConfigured)
            {
                return Task.FromResult<Validation<Error, Unit>>(Unit.Default);
            }

            var escapedPort = EscapeSingleQuoted(_serialPort);
            var sttyCommand =
                $"stty -F '{escapedPort}' {_baudRate} cs8 -cstopb -parenb -ixon -ixoff -icanon -echo min 0 time 5";

            var (exitCode, _, stderr) = RunShellCommand(sttyCommand, _writeTimeout, cancellationToken);
            if (exitCode != 0)
            {
                _logger.LogError("Impossible de configurer le port série {SerialPort}. stderr={StdErr}", _serialPort, stderr);
                return Task.FromResult<Validation<Error, Unit>>(
                    Error.New(500, $"Impossible de configurer le port série {_serialPort}: {stderr}"));
            }

            _portConfigured = true;
            _logger.LogDebug("Port série {SerialPort} configuré avec succès via stty", _serialPort);
            return Task.FromResult<Validation<Error, Unit>>(Unit.Default);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur inattendue lors de l'ouverture du port série {SerialPort}", _serialPort);
            return Task.FromResult<Validation<Error, Unit>>(Error.New(500, "Erreur inattendue lors de l'ouverture du port", ex));
        }
    }

    private Task<Validation<Error, string>> SendCommandAsync(string command, CancellationToken cancellationToken)
    {
        try
        {
            if (!_portConfigured)
            {
                return Task.FromResult<Validation<Error, string>>(Error.New(500, "Port série non configuré"));
            }

            _logger.LogDebug("Envoi de la commande AT: {Command}", command);

            var escapedPort = EscapeSingleQuoted(_serialPort);
            var escapedCommand = EscapeSingleQuoted(command);
            var readTimeoutSeconds = Math.Max(1, _readTimeout / 1000);

            var shellCommand =
                $"printf '%s\\r\\n' '{escapedCommand}' > '{escapedPort}' && timeout {readTimeoutSeconds}s head -n 1 < '{escapedPort}'";

            var (exitCode, stdout, stderr) = RunShellCommand(shellCommand, _readTimeout + _writeTimeout + 1000, cancellationToken);
            if (exitCode != 0)
            {
                _logger.LogWarning("Commande SA818 en échec (code {ExitCode}). stderr={StdErr}", exitCode, stderr);
                return Task.FromResult<Validation<Error, string>>(Error.New(500, $"Échec commande SA818: {stderr}"));
            }

            var response = (stdout ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(response))
            {
                return Task.FromResult<Validation<Error, string>>(Error.New(408, $"Timeout sur la commande {command}"));
            }

            _logger.LogDebug("Réponse reçue: {Response}", response);

            // Vérifier si la réponse indique un succès (les firmwares peuvent répondre SETFILTER ou DMOSETFILTER)
            if (response.Contains("+DMOSETGROUP:0") || 
                response.Contains("+DMOSETVOLUME:0") || 
                response.Contains("+SETFILTER:0") ||
                response.Contains("+DMOSETFILTER:0") ||
                response.Equals("OK", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<Validation<Error, string>>(response);
            }

            _logger.LogWarning("Réponse invalide du module SA818: {Response}", response);
            return Task.FromResult<Validation<Error, string>>(Error.New(500, $"Réponse invalide: {response}"));
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Timeout lors de l'envoi de la commande {Command}", command);
            return Task.FromResult<Validation<Error, string>>(Error.New(408, $"Timeout sur la commande {command}", ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'envoi de la commande {Command}", command);
            return Task.FromResult<Validation<Error, string>>(Error.New(500, "Erreur lors de l'envoi de la commande", ex));
        }
    }

    private static string EscapeSingleQuoted(string input) => input.Replace("'", "'\"'\"'");

    private static (int ExitCode, string StdOut, string StdErr) RunShellCommand(
        string command,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-lc \"{command.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(Math.Max(500, timeoutMs));

        process.WaitForExitAsync(linkedCts.Token).GetAwaiter().GetResult();

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        return (process.ExitCode, stdout, stderr);
    }

    private void ClosePort()
    {
        _portConfigured = false;
        _logger.LogDebug("Port série SA818 libéré");
    }

    public void Dispose()
    {
        ClosePort();
        GC.SuppressFinalize(this);
    }
}
