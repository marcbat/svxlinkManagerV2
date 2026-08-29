using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using static LanguageExt.Prelude;

namespace SvxlinkManagerV2.Infrastructure.SvxLink.InfoProviders;

/// <summary>
/// Fournisseur d'information pour la commande DTMF 302.
/// Annonce l'adresse IPv4 de l'interface réseau active, groupe par groupe
/// (« 192 point 168 point 1 point 42 ») pour rester compréhensible en phonie.
/// </summary>
public class IpAddressInfoProvider : IInfoProvider
{
    private readonly ILogger<IpAddressInfoProvider> _logger;
    private readonly Func<IReadOnlyList<ActiveIpv4Address>> _addressResolver;

    /// <summary>Texte annoncé lorsque aucune adresse IPv4 exploitable n'est disponible.</summary>
    internal const string NoAddressText = "Aucune adresse IP. Le nœud n'est connecté à aucun réseau";

    /// <inheritdoc/>
    public int DtmfCode => 302;

    /// <inheritdoc/>
    public string Description => "Adresse IP du nœud";

    /// <param name="logger">Logger.</param>
    /// <param name="addressResolver">
    /// Résolveur des adresses IPv4 actives, par ordre de préférence (sans fil puis filaire).
    /// Laissé à <c>null</c> en production : la résolution système est alors utilisée.
    /// </param>
    public IpAddressInfoProvider(
        ILogger<IpAddressInfoProvider> logger,
        Func<IReadOnlyList<ActiveIpv4Address>>? addressResolver = null)
    {
        _logger = logger;
        _addressResolver = addressResolver ?? ResolveSystemAddresses;
    }

    /// <inheritdoc/>
    public Task<Validation<Error, string>> GetInfoTextAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var active = _addressResolver().FirstOrDefault();

            if (active is null)
            {
                _logger.LogInformation("Commande DTMF 302 : aucune adresse IPv4 active détectée");
                return Task.FromResult(Validation<Error, string>.Success(NoAddressText));
            }

            _logger.LogInformation("Commande DTMF 302 : adresse IPv4 {IpAddress} sur {Interface}",
                active.IpAddress, active.InterfaceName);

            return Task.FromResult(Validation<Error, string>.Success(BuildInfoText(active.IpAddress)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception lors de la résolution de l'adresse IPv4");
            return Task.FromResult(Validation<Error, string>.Fail(Seq1(Error.New(ex))));
        }
    }

    /// <summary>
    /// Construit la phrase annoncée pour une adresse IPv4 donnée.
    /// </summary>
    internal static string BuildInfoText(string ipAddress) =>
        $"L'adresse IP du nœud est {FormatIpAddress(ipAddress)}";

    /// <summary>
    /// Met une adresse IPv4 sous une forme énonçable en phonie : chaque groupe est séparé
    /// par le mot « point » et les zéros de tête sont supprimés
    /// (« 192.168.001.042 » devient « 192 point 168 point 1 point 42 »).
    /// </summary>
    internal static string FormatIpAddress(string ipAddress)
    {
        var groups = ipAddress
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(g => int.TryParse(g.Trim(), out var value) ? value.ToString() : g.Trim());

        return string.Join(" point ", groups);
    }

    /// <summary>
    /// Énumère les adresses IPv4 des interfaces système actives, hors loopback, hors tunnels
    /// et hors adresses auto-attribuées APIPA (169.254.0.0/16).
    /// Les interfaces sans fil sont annoncées en priorité, le filaire ensuite.
    /// </summary>
    private static IReadOnlyList<ActiveIpv4Address> ResolveSystemAddresses() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
            .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback
                       && nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .OrderBy(nic => nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 0 : 1)
            .SelectMany(nic => nic.GetIPProperties().UnicastAddresses
                .Select(a => a.Address)
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IsApipa(a))
                .Select(a => new ActiveIpv4Address(nic.Name, a.ToString())))
            .ToList();

    /// <summary>Indique si l'adresse est une adresse auto-attribuée APIPA (169.254.0.0/16).</summary>
    private static bool IsApipa(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;
    }
}

/// <summary>
/// Adresse IPv4 active portée par une interface réseau du système.
/// </summary>
/// <param name="InterfaceName">Nom de l'interface (ex : <c>wlan0</c>).</param>
/// <param name="IpAddress">Adresse IPv4 en notation pointée.</param>
public record ActiveIpv4Address(string InterfaceName, string IpAddress);
