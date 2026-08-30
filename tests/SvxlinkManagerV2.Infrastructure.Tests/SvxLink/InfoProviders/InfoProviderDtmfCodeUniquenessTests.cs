using System.Runtime.CompilerServices;
using FluentAssertions;
using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon;
using SvxlinkManagerV2.Infrastructure.SvxLink.InfoProviders;

namespace SvxlinkManagerV2.Infrastructure.Tests.SvxLink.InfoProviders;

/// <summary>
/// Verrouille l'unicité des codes DTMF des <see cref="IInfoProvider"/>.
///
/// DtmfAnnounceService résout le provider par <c>FirstOrDefault(p =&gt; p.DtmfCode == code)</c> :
/// deux providers partageant un code se masqueraient silencieusement, le perdant devenant
/// injoignable sans le moindre message. Le cas s'est produit à l'intégration de deux branches
/// développées en parallèle, chacune ayant réservé 302 et 303 de son côté.
/// </summary>
public class InfoProviderDtmfCodeUniquenessTests
{
    /// <summary>
    /// Instancie chaque provider sans passer par son constructeur : <c>DtmfCode</c> est par
    /// convention une constante, indépendante des dépendances injectées.
    /// </summary>
    private static IReadOnlyList<(Type Type, int Code)> DiscoverProviders() =>
        typeof(CpuTemperatureInfoProvider).Assembly
            .GetTypes()
            .Where(t => typeof(IInfoProvider).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .Select(t => (Type: t, Code: ((IInfoProvider)RuntimeHelpers.GetUninitializedObject(t)).DtmfCode))
            .OrderBy(p => p.Code)
            .ToList();

    [Fact]
    public void InfoProviders_ShouldNotShareTheSameDtmfCode()
    {
        var providers = DiscoverProviders();

        providers.Should().NotBeEmpty("l'assembly Infrastructure doit exposer des IInfoProvider");

        var duplicates = providers
            .GroupBy(p => p.Code)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} : {string.Join(", ", g.Select(p => p.Type.Name))}")
            .ToList();

        duplicates.Should().BeEmpty(
            "deux providers partageant un code DTMF se masquent silencieusement dans DtmfAnnounceService");
    }

    [Fact]
    public void InfoProviders_ShouldStayInsideTheAnnounceRange()
    {
        var providers = DiscoverProviders();

        providers.Should().OnlyContain(
            p => p.Code >= DtmfCodeRanges.AnnounceRangeMin && p.Code <= DtmfCodeRanges.AnnounceRangeMax,
            "les annonces vocales sont routées via la plage réservée 300-399");
    }

    [Fact]
    public void InfoProviders_ShouldNotCollideWithSystemCommands()
    {
        var providers = DiscoverProviders();

        providers.Should().NotContain(
            p => DtmfSystemCommands.IsSystemCommand(p.Code),
            "DtmfAnnounceService ignore les codes système, un provider portant un tel code serait injoignable");
    }
}
