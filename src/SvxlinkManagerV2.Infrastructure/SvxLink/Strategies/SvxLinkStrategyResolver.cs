using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;

namespace SvxlinkManagerV2.Infrastructure.SvxLink.Strategies;

/// <summary>
/// Resolves the appropriate <see cref="ISvxLinkVersionStrategy"/> from a dictionary
/// indexed by <see cref="ReflectorProtocol"/>.
/// </summary>
public class SvxLinkStrategyResolver : ISvxLinkStrategyResolver
{
    private readonly IReadOnlyDictionary<ReflectorProtocol, ISvxLinkVersionStrategy> _strategies;

    public SvxLinkStrategyResolver(IEnumerable<ISvxLinkVersionStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(s => s.Protocol);
    }

    public ISvxLinkVersionStrategy Resolve(ReflectorProtocol protocol)
    {
        if (_strategies.TryGetValue(protocol, out var strategy))
            return strategy;

        throw new ArgumentException($"No SVXLink version strategy registered for protocol {protocol}");
    }

    public IEnumerable<ISvxLinkVersionStrategy> GetAll() => _strategies.Values;
}
