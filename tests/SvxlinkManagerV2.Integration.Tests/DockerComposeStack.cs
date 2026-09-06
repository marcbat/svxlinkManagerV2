using System.Diagnostics;
using System.Text;

namespace SvxlinkManagerV2.Integration.Tests;

/// <summary>
/// Pilote la stack <c>docker-compose.yml</c> versionnée à la racine du dépôt.
/// </summary>
/// <remarks>
/// La stack elle-même est le sujet du test : c'est elle qui porte le hook de signature,
/// les images et les fichiers de configuration. La rejouer avec Testcontainers en
/// dupliquerait la définition, et le test cesserait alors de couvrir ce qui est livré.
///
/// Un nom de projet dédié isole ces conteneurs et ces volumes de la stack de
/// développement : lancer les tests ne détruit pas ce que le contributeur avait en cours.
/// </remarks>
public sealed class DockerComposeStack : IAsyncLifetime
{
    /// <summary>Nom de projet compose réservé aux tests d'intégration.</summary>
    public const string ProjectName = "svxlink-integration";

    /// <summary>Services montés : le réflecteur et les deux nœuds nus.</summary>
    /// <remarks>
    /// L'application .NET n'en fait pas partie : son image ajoute plusieurs minutes de
    /// construction, et ce qui est éprouvé ici — signature de CSR, canal chiffré, login
    /// en protocole 3.0 — passe intégralement par les nœuds nus.
    /// </remarks>
    public static readonly string[] Services = ["svxreflector", "svxlink-node2", "svxlink-node3"];

    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(5);

    /// <summary>Racine du dépôt, déterminée en remontant jusqu'au docker-compose.yml.</summary>
    public string RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>Durée de démarrage de la stack, construction des images comprise.</summary>
    public TimeSpan StartupDuration { get; private set; }

    public async Task InitializeAsync()
    {
        // xUnit construit les fixtures de collection avant de savoir que les tests
        // sont ignorés : sans cette garde, une machine sans Docker paierait quand
        // même le démarrage de la stack.
        if (Environment.GetEnvironmentVariable(DockerComposeFactAttribute.EnvironmentVariableName) != "1")
            return;

        var stopwatch = Stopwatch.StartNew();

        // Volumes vierges : une CA laissée par une exécution précédente masquerait
        // une régression de la chaîne de signature.
        await ComposeAsync(CommandTimeout, "down", "-v", "--remove-orphans");

        var up = await ComposeAsync(BuildTimeout, ["up", "-d", "--build", .. Services]);
        if (up.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Échec du démarrage de la stack Docker (code {up.ExitCode}) :{Environment.NewLine}{up.Output}");
        }

        StartupDuration = stopwatch.Elapsed;
    }

    public async Task DisposeAsync()
    {
        if (Environment.GetEnvironmentVariable(DockerComposeFactAttribute.EnvironmentVariableName) != "1")
            return;

        await ComposeAsync(CommandTimeout, "down", "-v", "--remove-orphans");
    }

    /// <summary>
    /// Attend qu'une ligne des journaux du service contienne <paramref name="pattern"/>.
    /// </summary>
    /// <returns>La première ligne correspondante, ou <c>null</c> si le délai expire.</returns>
    public async Task<string?> WaitForLogAsync(string service, string pattern, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var logs = await GetLogsAsync(service);
            var match = logs
                .Split('\n')
                .FirstOrDefault(line => line.Contains(pattern, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
                return match.TrimEnd('\r');

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        return null;
    }

    /// <summary>Journaux complets d'un service depuis son démarrage.</summary>
    public async Task<string> GetLogsAsync(string service)
    {
        var result = await ComposeAsync(CommandTimeout, "logs", "--no-color", service);
        return result.Output;
    }

    private Task<(int ExitCode, string Output)> ComposeAsync(TimeSpan timeout, params string[] arguments)
    {
        string[] full = ["compose", "-p", ProjectName, .. arguments];
        return RunAsync("docker", full, RepositoryRoot, timeout);
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(
        string fileName, string[] arguments, string workingDirectory, TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            // Les journaux du réflecteur sont en UTF-8 (les messages de dev-ca-hook.sh
            // sont accentués). Sans cette précision, .NET décode avec la page de code
            // de la console sous Windows et les motifs accentués ne sont jamais trouvés.
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();

        // Lecture asynchrone : docker compose build produit assez de sortie pour
        // remplir le tampon du pipe et bloquer le processus si on ne le draine pas.
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* déjà terminé */ }
            lock (output) output.AppendLine($"[timeout après {timeout}]");
            return (-1, output.ToString());
        }

        lock (output) return (process.ExitCode, output.ToString());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "docker-compose.yml introuvable en remontant depuis " + AppContext.BaseDirectory);
    }
}

/// <summary>
/// Collection xUnit qui partage une seule stack entre tous les tests d'intégration :
/// la monter coûte plusieurs minutes, la rejouer par test serait intenable.
/// </summary>
[CollectionDefinition(Name)]
public class DockerComposeCollection : ICollectionFixture<DockerComposeStack>
{
    public const string Name = "DockerCompose";
}
