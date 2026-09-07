using System.Net;
using FluentAssertions;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SvxlinkManagerV2.Application.Features.Reflectors;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;
using SvxlinkManagerV2.Infrastructure.Reflector;
using Xunit;
using static LanguageExt.Prelude;
using LangExtError = LanguageExt.Common.Error;

namespace SvxlinkManagerV2.Infrastructure.Tests.Reflector;

/// <summary>
/// Tests de la dégradation du service d'interrogation du statut du réflecteur.
/// </summary>
/// <remarks>
/// Ce qui compte ici n'est pas le chemin nominal — le parsing a ses propres tests — mais
/// que chaque façon dont l'API peut manquer produise un état <em>nommé</em>. Une page qui
/// affiche « aucun nœud » alors que le réflecteur est arrêté ment à l'opérateur.
/// </remarks>
public class ReflectorStatusPollerTests : IDisposable
{
    private readonly ILogger<ReflectorStatusPoller> _logger = Substitute.For<ILogger<ReflectorStatusPoller>>();
    private readonly IReflectorDaemonService _daemonService = Substitute.For<IReflectorDaemonService>();
    private readonly string _configPath;

    public ReflectorStatusPollerTests()
    {
        _configPath = Path.Combine(Path.GetTempPath(), $"svxreflector-{Guid.NewGuid():N}.conf");
        _daemonService.IsRunningAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, bool>>(true));
    }

    private void WriteConfig(string content) => File.WriteAllText(_configPath, content);

    private ReflectorStatusPoller CreatePoller(HttpMessageHandler? handler = null, string host = "127.0.0.1") =>
        new(_logger,
            _daemonService,
            Options.Create(new LocalReflectorOptions { Host = host }),
            handler is null ? null : new HttpClient(handler),
            _configPath);

    [Fact]
    public async Task ReadStatusAsync_WhenTheDaemonIsStopped_ShouldSaySo()
    {
        _daemonService.IsRunningAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, bool>>(false));
        WriteConfig("[GLOBAL]\nHTTP_SRV_PORT=8888\n");

        var snapshot = await CreatePoller().ReadStatusAsync(CancellationToken.None);

        snapshot.Availability.Should().Be(ReflectorStatusAvailability.DaemonStopped);
        snapshot.Nodes.Should().BeEmpty();
        snapshot.Detail.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ReadStatusAsync_WhenTheDaemonStateIsUnknown_ShouldNotClaimAvailability()
    {
        // Un échec de lecture de l'état du démon ne dit pas qu'il tourne : interroger
        // quand même produirait une erreur réseau moins parlante.
        _daemonService.IsRunningAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Validation<LangExtError, bool>.Fail(Seq1(LangExtError.New("pgrep absent")))));
        WriteConfig("[GLOBAL]\nHTTP_SRV_PORT=8888\n");

        var snapshot = await CreatePoller().ReadStatusAsync(CancellationToken.None);

        snapshot.Availability.Should().Be(ReflectorStatusAvailability.DaemonStopped);
    }

    [Fact]
    public async Task ReadStatusAsync_WithoutTheHttpPort_ShouldSaySoRatherThanTryToConnect()
    {
        // Cas d'un réflecteur configuré avant que l'application n'expose l'API : la clé
        // manque, et c'est à l'utilisateur de l'ajouter.
        WriteConfig("[GLOBAL]\nLISTEN_PORT=5300\n");

        var snapshot = await CreatePoller().ReadStatusAsync(CancellationToken.None);

        snapshot.Availability.Should().Be(ReflectorStatusAvailability.PortNotConfigured);
        snapshot.Detail.Should().Contain("HTTP_SRV_PORT");
    }

    [Fact]
    public async Task ReadStatusAsync_WithoutAnyConfigFile_ShouldSayThePortIsNotConfigured()
    {
        var snapshot = await CreatePoller().ReadStatusAsync(CancellationToken.None);

        snapshot.Availability.Should().Be(ReflectorStatusAvailability.PortNotConfigured);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("70000")]
    [InlineData("-1")]
    public async Task ReadStatusAsync_WithAnUnusablePort_ShouldTreatItAsNotConfigured(string port)
    {
        WriteConfig($"[GLOBAL]\nHTTP_SRV_PORT={port}\n");

        var snapshot = await CreatePoller().ReadStatusAsync(CancellationToken.None);

        snapshot.Availability.Should().Be(ReflectorStatusAvailability.PortNotConfigured);
    }

    [Fact]
    public async Task ReadStatusAsync_WhenTheServerIsUnreachable_ShouldSaySo()
    {
        WriteConfig("[GLOBAL]\nHTTP_SRV_PORT=8888\n");
        var handler = new StubHandler(_ => throw new HttpRequestException("connexion refusée"));

        var snapshot = await CreatePoller(handler).ReadStatusAsync(CancellationToken.None);

        snapshot.Availability.Should().Be(ReflectorStatusAvailability.Unreachable);
        snapshot.Detail.Should().Contain("8888");
    }

    [Fact]
    public async Task ReadStatusAsync_WhenTheResponseIsNotTheExpectedDocument_ShouldSaySo()
    {
        WriteConfig("[GLOBAL]\nHTTP_SRV_PORT=8888\n");
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>Not Found</html>")
        });

        var snapshot = await CreatePoller(handler).ReadStatusAsync(CancellationToken.None);

        snapshot.Availability.Should().Be(ReflectorStatusAvailability.Invalid);
        snapshot.Nodes.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadStatusAsync_WithAValidDocument_ShouldExposeTheNodes()
    {
        WriteConfig("[GLOBAL]\nHTTP_SRV_PORT=8888\n");
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"nodes":{"HB9GXP3-H":{"tg":240,"isTalker":true,"monitoredTGs":[2403]}}}""")
        });

        var snapshot = await CreatePoller(handler).ReadStatusAsync(CancellationToken.None);

        snapshot.IsAvailable.Should().BeTrue();
        snapshot.Nodes.Should().ContainSingle().Which.TalkGroup.Should().Be(240);
        snapshot.TalkerCallsign.Should().Be("HB9GXP3-H");
        snapshot.RetrievedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ReadStatusAsync_ShouldQueryTheLoopbackAddressOnly()
    {
        // Le démon tourne sur la même machine. Interroger une autre adresse ferait sortir
        // du nœud une requête vers un port que la documentation SVXLink demande de garder
        // fermé au monde extérieur.
        WriteConfig("[GLOBAL]\nHTTP_SRV_PORT=8888\n");
        Uri? requested = null;
        var handler = new StubHandler(request =>
        {
            requested = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"nodes":{}}""") };
        });

        await CreatePoller(handler).ReadStatusAsync(CancellationToken.None);

        requested.Should().NotBeNull();
        requested!.Host.Should().Be("127.0.0.1");
        requested.Port.Should().Be(8888);
    }

    [Fact]
    public async Task ReadStatusAsync_ShouldRereadThePortOnEveryCall()
    {
        // La configuration peut changer sans que l'application redémarre : relire le
        // fichier évite d'obliger l'utilisateur à relancer le service.
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"nodes":{}}""")
        });
        var poller = CreatePoller(handler);

        WriteConfig("[GLOBAL]\nLISTEN_PORT=5300\n");
        var before = await poller.ReadStatusAsync(CancellationToken.None);

        WriteConfig("[GLOBAL]\nHTTP_SRV_PORT=8888\n");
        var after = await poller.ReadStatusAsync(CancellationToken.None);

        before.Availability.Should().Be(ReflectorStatusAvailability.PortNotConfigured);
        after.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task ReadStatusAsync_WithARemoteReflector_ShouldNotCheckTheLocalDaemon()
    {
        // Stack Docker : le réflecteur vit dans un autre conteneur. Interroger pgrep ici
        // conclurait à un démon arrêté et masquerait un réflecteur parfaitement joignable.
        _daemonService.IsRunningAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Validation<LangExtError, bool>>(false));
        WriteConfig("[GLOBAL]\nHTTP_SRV_PORT=8888\n");
        Uri? requested = null;
        var handler = new StubHandler(request =>
        {
            requested = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"nodes":{}}""") };
        });

        var snapshot = await CreatePoller(handler, "svxreflector").ReadStatusAsync(CancellationToken.None);

        snapshot.IsAvailable.Should().BeTrue();
        requested!.Host.Should().Be("svxreflector");
        await _daemonService.DidNotReceive().IsRunningAsync(Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        try { File.Delete(_configPath); } catch { /* rien à nettoyer */ }
    }

    /// <summary>
    /// Gestionnaire HTTP scriptable : NSubstitute ne peut pas substituer SendAsync,
    /// qui est protégé.
    /// </summary>
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
