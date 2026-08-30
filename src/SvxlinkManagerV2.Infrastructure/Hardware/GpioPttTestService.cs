using LanguageExt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SvxlinkManagerV2.Domain.Common;
using Unit = LanguageExt.Unit;

namespace SvxlinkManagerV2.Infrastructure.Hardware;

/// <summary>
/// Test d'émission par action directe sur le GPIO du PTT, via sysfs.
///
/// C'est le même GPIO que celui piloté par SVXLink (<c>PTT_PIN</c> de la section Tx1) : le test
/// s'appuie donc sur le daemon en fonctionnement, qui a exporté la broche et l'a configurée en
/// sortie. L'application ne l'exporte jamais elle-même — le faire reviendrait à revendiquer une
/// broche dont elle ignore le câblage, et laisserait un export orphelin derrière elle.
///
/// Le test ne fait que porter la porteuse : aucun audio n'est injecté, SVXLink restant maître
/// de la chaîne de restitution.
/// </summary>
public class GpioPttTestService : PttTestServiceBase
{
    private readonly ILogger<GpioPttTestService> _logger;

    public GpioPttTestService(IOptions<AudioOptions> options, ILogger<GpioPttTestService> logger)
        : base(options, logger)
    {
        _logger = logger;
    }

    protected override bool IsSimulated => false;

    /// <summary>
    /// Chemin sysfs de la valeur du GPIO commandant le PTT.
    /// </summary>
    internal string ValuePath => Path.Combine(Options.PttGpioPath, Options.PttPin, "value");

    protected override async Task<Validation<Error, Unit>> SetPttAsync(bool keyed, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux())
        {
            return Error.Validation(
                    "PTT_TEST_UNSUPPORTED_PLATFORM",
                    "Le test d'émission n'est possible que sur la cible Linux.")
                .ToFailure<Unit>();
        }

        var path = ValuePath;

        if (!File.Exists(path))
        {
            return Error.Validation(
                    "PTT_TEST_GPIO_UNAVAILABLE",
                    $"La broche PTT « {Options.PttPin} » n'est pas exportée : démarrez un salon pour que SVXLink prenne la main sur le GPIO.")
                .ToFailure<Unit>();
        }

        try
        {
            await File.WriteAllTextAsync(path, keyed ? "1" : "0", cancellationToken);

            _logger.LogInformation(
                "PTT {State} via {Path}", keyed ? "activé" : "relâché", path);

            return Unit.Default.ToSuccess();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec de l'écriture du PTT sur {Path}", path);

            return Error.Validation(
                    "PTT_TEST_GPIO_WRITE_FAILED",
                    $"La broche PTT n'a pas pu être commandée : {ex.Message}")
                .ToFailure<Unit>();
        }
    }
}
