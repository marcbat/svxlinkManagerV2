using System.Text;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.Reflector;

/// <summary>
/// Implémentation de <see cref="IReflectorCommandWriter"/> : écrit dans le pseudo-terminal
/// <c>COMMAND_PTY</c> du démon <c>svxreflector</c>.
/// </summary>
/// <remarks>
/// Le chemin du PTY vient de la configuration réellement chargée par le démon, relue à chaque
/// envoi : c'est elle qui décide, et un opérateur peut l'avoir changée depuis le démarrage de
/// l'application.
/// </remarks>
public class ReflectorCommandPtyWriter : IReflectorCommandWriter
{
    private readonly ILogger<ReflectorCommandPtyWriter> _logger;
    private readonly ReflectorConfigurationFile _configuration;

    public ReflectorCommandPtyWriter(ILogger<ReflectorCommandPtyWriter> logger, string? configPath = null)
    {
        _logger = logger;
        _configuration = new ReflectorConfigurationFile(configPath);
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, Unit>> SendCommandAsync(
        string command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command))
            return Fail("La commande du réflecteur ne peut pas être vide.");

        // Un saut de ligne dans la commande en injecterait une seconde : le démon lit le PTY
        // ligne par ligne, et un indicatif mal formé ne doit pas pouvoir en profiter.
        if (command.Contains('\n') || command.Contains('\r'))
            return Fail("La commande du réflecteur ne peut pas contenir de saut de ligne.");

        var ptyPath = _configuration.Read(ReflectorConfigurationFile.CommandPtyKey);
        if (ptyPath is null)
            return Fail(
                $"La configuration du réflecteur ne déclare pas {ReflectorConfigurationFile.CommandPtyKey} " +
                "dans [GLOBAL] : aucune commande ne peut lui être transmise.");

        _logger.LogInformation("Envoi de la commande « {Command} » au réflecteur via {PtyPath}", command, ptyPath);

        try
        {
            // Path.Exists et non File.Exists : le PTY est un dispositif caractère
            // (lien vers /dev/pts/N), pour lequel File.Exists retourne false.
            if (!Path.Exists(ptyPath))
                return Fail($"Le PTY de commande du réflecteur est introuvable : {ptyPath}. Le démon tourne-t-il ?");

            var bytes = Encoding.ASCII.GetBytes(command + "\n");

            // FileMode.Open est requis pour écrire dans un dispositif caractère ;
            // FileMode.Append, qu'utilise File.AppendAllTextAsync, ne fonctionne pas sur un PTY.
            await using var stream = new FileStream(ptyPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            await stream.WriteAsync(bytes, cancellationToken);

            _logger.LogInformation("Commande « {Command} » transmise au réflecteur", command);
            return Validation<Error, Unit>.Success(Unit.Default);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception lors de l'envoi de la commande « {Command} » au réflecteur", command);
            return Validation<Error, Unit>.Fail(Seq1(Error.New(ex)));
        }
    }

    private Validation<Error, Unit> Fail(string message)
    {
        _logger.LogWarning(message);
        return Validation<Error, Unit>.Fail(Seq1(Error.New(message)));
    }
}
