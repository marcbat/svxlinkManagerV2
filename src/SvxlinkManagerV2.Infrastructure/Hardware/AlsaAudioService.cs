using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Infrastructure.Hardware;

/// <summary>
/// Pilotage des niveaux de la carte son via <c>amixer</c>.
///
/// Seuls les deux contrôles désignés par <see cref="AudioOptions"/> sont lus et écrits ; le reste
/// du routage de la carte (commutateurs de capture, sélection de source) n'est jamais touché.
/// </summary>
public partial class AlsaAudioService : IAudioService
{
    private readonly ILogger<AlsaAudioService> _logger;
    private readonly AudioOptions _options;

    public AlsaAudioService(IOptions<AudioOptions> options, ILogger<AlsaAudioService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsSimulated => false;

    public async Task<Validation<Error, AudioMixerState>> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var capture = await ReadControlAsync(_options.CaptureControl, cancellationToken);
        var playback = await ReadControlAsync(_options.PlaybackControl, cancellationToken);

        return (capture, playback).Apply((c, p) =>
            new AudioMixerState(_options.CardIndex, c, p, IsSimulated));
    }

    public Task<Validation<Error, AudioControlState>> SetCaptureLevelAsync(int value, CancellationToken cancellationToken = default)
        => WriteControlAsync(_options.CaptureControl, value, cancellationToken);

    public Task<Validation<Error, AudioControlState>> SetPlaybackLevelAsync(int value, CancellationToken cancellationToken = default)
        => WriteControlAsync(_options.PlaybackControl, value, cancellationToken);

    /// <summary>
    /// Lit l'état d'un contrôle via <c>amixer sget</c>.
    /// </summary>
    private async Task<Validation<Error, AudioControlState>> ReadControlAsync(
        string control,
        CancellationToken cancellationToken)
    {
        var result = await RunAmixerAsync(new[] { "sget", control }, cancellationToken);

        return result.Bind(output => ParseControl(control, output));
    }

    /// <summary>
    /// Applique une valeur à un contrôle via <c>amixer sset</c>, après l'avoir bornée à la plage
    /// réelle du contrôle : celle-ci dépend de la carte son et n'est connue qu'après lecture.
    /// </summary>
    private async Task<Validation<Error, AudioControlState>> WriteControlAsync(
        string control,
        int value,
        CancellationToken cancellationToken)
    {
        var currentResult = await ReadControlAsync(control, cancellationToken);
        if (currentResult.IsFail)
            return currentResult;

        var current = currentResult.Match(
            Succ: s => s,
            Fail: _ => throw new InvalidOperationException("Lecture du contrôle déjà validée."));

        var clamped = Math.Clamp(value, current.MinValue, current.MaxValue);

        if (clamped != value)
        {
            _logger.LogWarning(
                "Niveau {Value} hors plage pour le contrôle ALSA « {Control} » ({Min}-{Max}) : borné à {Clamped}",
                value, control, current.MinValue, current.MaxValue, clamped);
        }

        var writeResult = await RunAmixerAsync(
            new[] { "sset", control, clamped.ToString(CultureInfo.InvariantCulture) },
            cancellationToken);

        if (writeResult.IsFail)
            return writeResult.Map(_ => current);

        _logger.LogInformation(
            "Niveau ALSA « {Control} » réglé sur {Value} (carte {Card})",
            control, clamped, _options.CardIndex);

        // amixer réémet l'état du contrôle après écriture : on le relit dans sa sortie plutôt que
        // d'en supposer la valeur, ce qui révèle un réglage refusé par le pilote.
        return writeResult.Bind(output => ParseControl(control, output));
    }

    /// <summary>
    /// Extrait la plage et la valeur courante de la sortie d'<c>amixer</c> pour un contrôle simple.
    /// </summary>
    internal static Validation<Error, AudioControlState> ParseControl(string control, string amixerOutput)
    {
        var limits = LimitsRegex().Match(amixerOutput);
        if (!limits.Success)
        {
            return Error.Validation(
                    "AUDIO_CONTROL_NOT_ADJUSTABLE",
                    $"Le contrôle ALSA « {control} » ne déclare aucune plage de niveau : il n'est pas réglable.")
                .ToFailure<AudioControlState>();
        }

        var value = ValueRegex().Match(amixerOutput);
        if (!value.Success)
        {
            return Error.Validation(
                    "AUDIO_CONTROL_UNREADABLE",
                    $"Impossible de lire le niveau courant du contrôle ALSA « {control} ».")
                .ToFailure<AudioControlState>();
        }

        var min = int.Parse(limits.Groups[1].Value, CultureInfo.InvariantCulture);
        var max = int.Parse(limits.Groups[2].Value, CultureInfo.InvariantCulture);
        var current = int.Parse(value.Groups[1].Value, CultureInfo.InvariantCulture);

        return new AudioControlState(control, current, min, max).ToSuccess();
    }

    /// <summary>
    /// Exécute amixer sur la carte configurée et retourne sa sortie standard.
    /// </summary>
    private async Task<Validation<Error, string>> RunAmixerAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux())
        {
            return Error.Validation(
                    "AUDIO_UNSUPPORTED_PLATFORM",
                    "Le réglage des niveaux ALSA n'est possible que sur la cible Linux.")
                .ToFailure<string>();
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _options.AmixerPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(_options.CardIndex.ToString(CultureInfo.InvariantCulture));
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.CommandTimeoutSeconds)));

            await process.WaitForExitAsync(timeoutCts.Token);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                _logger.LogError(
                    "amixer {Arguments} a échoué (code {ExitCode}) : {StdErr}",
                    string.Join(' ', arguments), process.ExitCode, stderr.Trim());

                return Error.Validation(
                        "AUDIO_AMIXER_FAILED",
                        $"La commande amixer a échoué : {stderr.Trim()}")
                    .ToFailure<string>();
            }

            return stdout.ToSuccess();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Error.Validation(
                    "AUDIO_AMIXER_TIMEOUT",
                    "La carte son n'a pas répondu dans le délai imparti.")
                .ToFailure<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'appel à amixer {Arguments}", string.Join(' ', arguments));

            return Error.Validation(
                    "AUDIO_AMIXER_ERROR",
                    $"La carte son n'a pas pu être interrogée : {ex.Message}")
                .ToFailure<string>();
        }
    }

    /// <summary>
    /// Ligne « Limits: Playback 0 - 31 », « Limits: Capture 0 - 7 » ou « Limits: 0 - 7 ».
    /// </summary>
    [GeneratedRegex(@"^[ \t]*Limits:[ \t]*(?:Playback|Capture)?[ \t]*(-?\d+)[ \t]*-[ \t]*(-?\d+)[ \t]*$",
        RegexOptions.Multiline)]
    private static partial Regex LimitsRegex();

    /// <summary>
    /// Première ligne de canal portant une valeur, par exemple « Front Left: Playback 22 [71%] »
    /// ou « Mono: Capture 3 [43%] ». Le crochet ouvrant exigé après le nombre écarte la ligne
    /// « Limits: », dont les bornes ne sont pas suivies d'un pourcentage.
    /// </summary>
    [GeneratedRegex(@"^[ \t]*[^:\r\n]+:[ \t]*(?:Playback|Capture)?[ \t]*(-?\d+)[ \t]*\[",
        RegexOptions.Multiline)]
    private static partial Regex ValueRegex();
}
