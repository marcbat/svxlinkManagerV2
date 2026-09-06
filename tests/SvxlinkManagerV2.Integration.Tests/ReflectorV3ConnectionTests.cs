using FluentAssertions;
using Xunit.Abstractions;

namespace SvxlinkManagerV2.Integration.Tests;

/// <summary>
/// Vérifie qu'un nœud en protocole V3 établit réellement sa liaison au réflecteur :
/// PKI générée, CSR signée, canal chiffré, login en protocole 3.0.
/// </summary>
/// <remarks>
/// Les tests unitaires existants ne couvrent que la <em>génération</em> de svxlink.conf
/// et la résolution de stratégie. C'est ce trou qui a laissé vivre l'absence d'openssl
/// dans l'image du réflecteur (#113) : toute connexion V3 était impossible, et rien ne
/// le disait. Retirer openssl de Dockerfile.reflector, ou casser la signature dans
/// dev-ca-hook.sh, fait tomber ces tests.
/// </remarks>
[Collection(DockerComposeCollection.Name)]
[Trait("Category", "Integration")]
public class ReflectorV3ConnectionTests
{
    /// <summary>Indicatif du nœud V3 nu de la stack (svxlink-config-node3).</summary>
    private const string NodeV3Callsign = "HB9GXP3-H";

    /// <summary>Indicatif du nœud V2 legacy de la stack (svxlink-config-node2).</summary>
    private const string NodeV2Callsign = "HB9GXP2-H";

    /// <summary>
    /// Délai laissé à la négociation. Un nœud SVXLink réessaie toutes les quelques
    /// secondes : la première tentative peut précéder l'écoute du réflecteur.
    /// </summary>
    private static readonly TimeSpan LinkTimeout = TimeSpan.FromMinutes(3);

    private readonly DockerComposeStack _stack;
    private readonly ITestOutputHelper _output;

    public ReflectorV3ConnectionTests(DockerComposeStack stack, ITestOutputHelper output)
    {
        _stack = stack;
        _output = output;
    }

    [DockerComposeFact]
    public async Task NodeV3_ShouldGetItsCertificateSignedByTheReflector()
    {
        var signature = await _stack.WaitForLogAsync(
            "svxreflector", $"[dev-ca-hook] Certificat signé et enregistré", LinkTimeout);

        signature.Should().NotBeNull(
            "le hook CERT_CA_HOOK doit signer la CSR du nœud — sans openssl dans l'image, " +
            "aucun certificat n'est émis et le réflecteur retombe sur l'authentification par challenge");

        _output.WriteLine(signature);

        var reflectorLogs = await _stack.GetLogsAsync("svxreflector");
        reflectorLogs.Should().NotContain(
            "[dev-ca-hook] ERREUR",
            "ni la dépendance openssl ni la signature elle-même ne doivent échouer");
        reflectorLogs.Should().Contain(NodeV3Callsign);
    }

    [DockerComposeFact]
    public async Task NodeV3_ShouldLogInWithProtocolVersion3()
    {
        var login = await _stack.WaitForLogAsync(
            "svxreflector", $"{NodeV3Callsign}: Login OK", LinkTimeout);

        login.Should().NotBeNull($"le nœud {NodeV3Callsign} doit être authentifié par le réflecteur");
        login.Should().Contain("protocol version 3.0", "la liaison doit être négociée en protocole V3");

        _output.WriteLine(login);
    }

    [DockerComposeFact]
    public async Task NodeV3_ShouldEstablishAnEncryptedConnection()
    {
        var encrypted = await _stack.WaitForLogAsync(
            "svxlink-node3", "Encrypted connection established", LinkTimeout);

        encrypted.Should().NotBeNull("le protocole V3 chiffre le canal de contrôle");

        var authenticated = await _stack.WaitForLogAsync(
            "svxlink-node3", "Authentication OK", LinkTimeout);

        authenticated.Should().NotBeNull("le certificat signé doit être accepté par le serveur");

        // Un « Access denied » figure normalement dans ces journaux : à la première
        // connexion le nœud n'a pas encore de certificat, le réflecteur refuse et met
        // la CSR en attente — c'est ce refus qui déclenche la signature. Ce qui
        // distingue #113, c'est que le nœud n'en sortait jamais.
        var nodeLogs = await _stack.GetLogsAsync("svxlink-node3");
        nodeLogs.Should().NotContain(
            "Certificate verification failed",
            "le nœud télécharge la CA du serveur lui-même (CERT_DOWNLOAD_CA_BUNDLE)");
    }

    /// <summary>
    /// Coexistence V2/V3 : c'est le scénario réel de migration du parc, et rien ne
    /// garantit a priori qu'un réflecteur qui sert des talkgroups accepte encore un
    /// nœud legacy.
    /// </summary>
    [DockerComposeFact]
    public async Task V2AndV3Nodes_ShouldCoexistOnTheSameReflector()
    {
        var v3Login = await _stack.WaitForLogAsync(
            "svxreflector", $"{NodeV3Callsign}: Login OK", LinkTimeout);
        var v2Login = await _stack.WaitForLogAsync(
            "svxreflector", $"{NodeV2Callsign}: Login OK", LinkTimeout);

        v3Login.Should().NotBeNull("le nœud V3 doit être connecté");
        v2Login.Should().NotBeNull(
            "le nœud V2 doit rester accepté — il est déclaré dans [USERS]/[PASSWORDS]");

        v3Login.Should().Contain("protocol version 3.0");
        v2Login.Should().NotContain(
            "protocol version 3.0",
            "le nœud legacy négocie une version antérieure du protocole");

        _output.WriteLine(v3Login);
        _output.WriteLine(v2Login);
    }

    /// <summary>
    /// Le coût du job d'intégration doit rester visible : c'est lui qui justifie de le
    /// tenir hors de la boucle de chaque commit.
    /// </summary>
    [DockerComposeFact]
    public void Stack_StartupDuration_IsReported()
    {
        _output.WriteLine(
            $"Démarrage de la stack ({string.Join(", ", DockerComposeStack.Services)}), " +
            $"construction des images comprise : {_stack.StartupDuration.TotalSeconds:F0} s");

        _stack.StartupDuration.Should().BePositive();
    }
}
