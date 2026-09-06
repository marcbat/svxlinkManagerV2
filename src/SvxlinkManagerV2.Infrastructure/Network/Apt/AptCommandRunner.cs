using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SvxlinkManagerV2.Infrastructure.Network.Apt;

/// <inheritdoc cref="IAptCommandRunner"/>
public class AptCommandRunner : IAptCommandRunner
{
    private readonly ILogger<AptCommandRunner> _logger;
    private readonly AptUpdateOptions _options;

    public AptCommandRunner(ILogger<AptCommandRunner> logger, IOptions<AptUpdateOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task<AptCommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Les libellés d'apt-cache policy (« Installed: », « Candidate: ») sont traduits
        // selon la locale. Sur un système français ils deviennent « Installé : », ce qui
        // casserait l'analyse — d'où la locale neutre forcée ici.
        startInfo.Environment["LC_ALL"] = "C";
        startInfo.Environment["LANG"] = "C";

        // apt refuse toute question interactive dans ce mode et échoue proprement
        // plutôt que de rester bloqué sur une invite sans terminal attaché.
        startInfo.Environment["DEBIAN_FRONTEND"] = "noninteractive";

        _logger.LogDebug("Exécution : {FileName} {Arguments}", fileName, string.Join(' ', arguments));

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Impossible de lancer {FileName}", fileName);
            return new AptCommandResult(-1, string.Empty, $"Impossible de lancer {fileName} : {ex.Message}");
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.CommandTimeoutSeconds)));

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process, fileName);

            var reason = cancellationToken.IsCancellationRequested
                ? "L'opération a été annulée."
                : $"La commande {fileName} a dépassé le délai de {_options.CommandTimeoutSeconds} s.";

            return new AptCommandResult(-1, string.Empty, reason);
        }

        var standardOutput = await stdoutTask;
        var standardError = await stderrTask;

        if (process.ExitCode != 0)
        {
            _logger.LogWarning(
                "{FileName} a retourné le code {ExitCode} : {Error}",
                fileName,
                process.ExitCode,
                string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError);
        }

        return new AptCommandResult(process.ExitCode, standardOutput, standardError);
    }

    private void TryKill(Process process, string fileName)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Impossible d'interrompre le processus {FileName}", fileName);
        }
    }
}
