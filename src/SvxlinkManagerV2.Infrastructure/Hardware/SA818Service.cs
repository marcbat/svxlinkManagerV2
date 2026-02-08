using System.IO.Ports;
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
    private SerialPort? _port;

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
            if (_port?.IsOpen == true)
            {
                return Task.FromResult<Validation<Error, Unit>>(Unit.Default);
            }

            _port = new SerialPort(_serialPort, _baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = _readTimeout,
                WriteTimeout = _writeTimeout
            };

            _port.Open();
            _logger.LogDebug("Port série {SerialPort} ouvert avec succès", _serialPort);
            return Task.FromResult<Validation<Error, Unit>>(Unit.Default);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Accès non autorisé au port série {SerialPort}", _serialPort);
            return Task.FromResult<Validation<Error, Unit>>(Error.New(403, $"Accès non autorisé au port série {_serialPort}", ex));
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Erreur I/O lors de l'ouverture du port série {SerialPort}", _serialPort);
            return Task.FromResult<Validation<Error, Unit>>(Error.New(500, $"Erreur I/O sur le port {_serialPort}", ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur inattendue lors de l'ouverture du port série {SerialPort}", _serialPort);
            return Task.FromResult<Validation<Error, Unit>>(Error.New(500, "Erreur inattendue lors de l'ouverture du port", ex));
        }
    }

    private async Task<Validation<Error, string>> SendCommandAsync(string command, CancellationToken cancellationToken)
    {
        try
        {
            if (_port?.IsOpen != true)
            {
                return Error.New(500, "Port série non ouvert");
            }

            _logger.LogDebug("Envoi de la commande AT: {Command}", command);

            // Envoyer la commande avec terminateur CRLF
            var commandWithTerminator = $"{command}\r\n";
            await _port.BaseStream.WriteAsync(System.Text.Encoding.ASCII.GetBytes(commandWithTerminator), cancellationToken);
            await _port.BaseStream.FlushAsync(cancellationToken);

            // Lire la réponse
            await Task.Delay(100, cancellationToken); // Délai pour laisser le module répondre
            var response = _port.ReadLine().Trim();

            _logger.LogDebug("Réponse reçue: {Response}", response);

            // Vérifier si la réponse indique un succès (généralement "+DMOSETGROUP:0" ou "OK")
            if (response.Contains("+DMOSETGROUP:0") || 
                response.Contains("+DMOSETVOLUME:0") || 
                response.Contains("+SETFILTER:0") ||
                response.Equals("OK", StringComparison.OrdinalIgnoreCase))
            {
                return response;
            }

            _logger.LogWarning("Réponse invalide du module SA818: {Response}", response);
            return Error.New(500, $"Réponse invalide: {response}");
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Timeout lors de l'envoi de la commande {Command}", command);
            return Error.New(408, $"Timeout sur la commande {command}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'envoi de la commande {Command}", command);
            return Error.New(500, "Erreur lors de l'envoi de la commande", ex);
        }
    }

    private void ClosePort()
    {
        try
        {
            if (_port?.IsOpen == true)
            {
                _port.Close();
                _logger.LogDebug("Port série fermé");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erreur lors de la fermeture du port série");
        }
    }

    public void Dispose()
    {
        ClosePort();
        _port?.Dispose();
        GC.SuppressFinalize(this);
    }
}
