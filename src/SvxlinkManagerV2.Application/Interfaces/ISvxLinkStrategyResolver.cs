using SvxlinkManagerV2.Domain.Aggregates.Salon.Enums;

namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Resolves the appropriate <see cref="ISvxLinkVersionStrategy"/> for a given reflector protocol.
/// </summary>
public interface ISvxLinkStrategyResolver
{
    /// <summary>
    /// Returns the strategy matching the specified protocol.
    /// </summary>
    /// <param name="protocol">The reflector protocol to resolve.</param>
    /// <returns>The corresponding version strategy.</returns>
    /// <exception cref="ArgumentException">Thrown when no strategy is registered for the protocol.</exception>
    ISvxLinkVersionStrategy Resolve(ReflectorProtocol protocol);

    /// <summary>
    /// Returns all registered version strategies.
    /// </summary>
    IEnumerable<ISvxLinkVersionStrategy> GetAll();
}
