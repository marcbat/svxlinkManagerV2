using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.SvxLink;

/// <summary>
/// Implémentation de <see cref="IDtmfPtyWriter"/> qui écrit des commandes DTMF
/// dans le pseudo-terminal (PTY) SVXLink (DTMF_CTRL_PTY).
/// </summary>
public class DtmfPtyWriter : IDtmfPtyWriter
{
    private readonly ILogger<DtmfPtyWriter> _logger;
    internal const string DefaultPtyPath = "/tmp/dtmf_uhf";

    private readonly string _ptyPath;

    public DtmfPtyWriter(ILogger<DtmfPtyWriter> logger, string? ptyPath = null)
    {
        _logger = logger;
        _ptyPath = ptyPath ?? DefaultPtyPath;
    }

    /// <inheritdoc/>
    public async Task<Validation<Error, Unit>> SendCommandAsync(
        string command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command))
            return Validation<Error, Unit>.Fail(Seq1(Error.New("La commande DTMF ne peut pas être vide.")));

        _logger.LogInformation("Envoi de la commande DTMF « {Command}# » vers le PTY {PtyPath}", command, _ptyPath);

        try
        {
            if (!File.Exists(_ptyPath))
            {
                var errorMsg = $"Le PTY SVXLink est introuvable : {_ptyPath}";
                _logger.LogWarning(errorMsg);
                return Validation<Error, Unit>.Fail(Seq1(Error.New(errorMsg)));
            }

            var payload = $"{command}#";
            await File.AppendAllTextAsync(_ptyPath, payload, cancellationToken);

            _logger.LogInformation("Commande DTMF « {Payload} » envoyée avec succès", payload);
            return Validation<Error, Unit>.Success(Unit.Default);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception lors de l'envoi de la commande DTMF vers le PTY");
            return Validation<Error, Unit>.Fail(Seq1(Error.New(ex)));
        }
    }
}
