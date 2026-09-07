using FluentAssertions;
using SvxlinkManagerV2.Domain.Aggregates.Salon;

namespace SvxlinkManagerV2.Domain.Tests.Aggregates.Salon;

/// <summary>
/// Tests de la liste de serveurs réflecteur.
/// </summary>
/// <remarks>
/// SVXLink tente les entrées de <c>HOSTS</c> dans l'ordre : <c>HOST_PRIO</c> vaut 100 pour la
/// première et <c>HOST_PRIO_INC</c> ajoute 1 à chaque suivante. C'est pourquoi l'ordre suffit
/// et qu'aucune priorité n'est demandée à l'opérateur.
/// </remarks>
public class ReflectorHostsTests
{
    #region Construction de HOSTS

    [Fact]
    public void Build_WithASingleServer_ShouldProduceTheHistoricalValue()
    {
        // Un salon qui ne déclare qu'un serveur doit générer exactement ce qu'il générait
        // avant : c'est la garantie qu'aucun salon existant ne change de comportement.
        ReflectorHosts.Build("rrf2.f5nlg.ovh", 5300, null)
            .Should().Be("rrf2.f5nlg.ovh:5300");
    }

    [Fact]
    public void Build_ShouldPutThePrimaryServerFirst()
    {
        var hosts = ReflectorHosts.Build("principal.example.org", 5300, "secours.example.org:5301");

        hosts.Should().Be("principal.example.org:5300,secours.example.org:5301");
    }

    [Fact]
    public void Build_ShouldKeepTheOrderOfTheAdditionalServers()
    {
        var hosts = ReflectorHosts.Build("a.example.org", 5300, "b.example.org, c.example.org");

        hosts.Should().Be("a.example.org:5300,b.example.org:5300,c.example.org:5300");
    }

    [Fact]
    public void Build_WithoutAPort_ShouldReuseThePrimaryPort()
    {
        ReflectorHosts.Build("a.example.org", 5301, "b.example.org")
            .Should().Be("a.example.org:5301,b.example.org:5301");
    }

    /// <summary>
    /// Déclarer deux fois le même serveur lui donnerait deux priorités, et SVXLink le
    /// retenterait en boucle avant de passer au suivant.
    /// </summary>
    [Fact]
    public void Build_ShouldNotRepeatTheSameServer()
    {
        var hosts = ReflectorHosts.Build("a.example.org", 5300, "a.example.org:5300, b.example.org");

        hosts.Should().Be("a.example.org:5300,b.example.org:5300");
    }

    [Fact]
    public void Build_ShouldIgnoreBlankEntries()
    {
        ReflectorHosts.Build("a.example.org", 5300, " , b.example.org , ")
            .Should().Be("a.example.org:5300,b.example.org:5300");
    }

    #endregion

    #region Validation

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("b.example.org")]
    [InlineData("b.example.org:5300")]
    [InlineData("b.example.org, c.example.org:5301")]
    [InlineData("192.168.1.10:5300")]
    [InlineData("serveur-de-secours.example.org")]
    public void IsValid_WithAcceptableEntries_ShouldReturnTrue(string? hosts)
    {
        ReflectorHosts.IsValid(hosts).Should().BeTrue();
    }

    [Theory]
    [InlineData("b.example.org:0")]
    [InlineData("b.example.org:70000")]
    [InlineData("b.example.org:abc")]
    [InlineData("http://b.example.org")]
    [InlineData("b.example.org/chemin")]
    [InlineData("b example org")]
    [InlineData("-b.example.org")]
    public void IsValid_WithMalformedEntries_ShouldReturnFalse(string hosts)
    {
        ReflectorHosts.IsValid(hosts).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("exemple.org")]
    [InlineData("sous.domaine.exemple.org")]
    public void IsValidDomain_WithAcceptableDomains_ShouldReturnTrue(string? domain)
    {
        ReflectorHosts.IsValidDomain(domain).Should().BeTrue();
    }

    [Theory]
    [InlineData("exemple.org:5300")]
    [InlineData("http://exemple.org")]
    [InlineData("exemple org")]
    public void IsValidDomain_WithMalformedDomains_ShouldReturnFalse(string domain)
    {
        ReflectorHosts.IsValidDomain(domain).Should().BeFalse();
    }

    #endregion
}
