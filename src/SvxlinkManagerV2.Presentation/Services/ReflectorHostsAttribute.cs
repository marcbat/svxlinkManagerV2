using System.ComponentModel.DataAnnotations;
using SvxlinkManagerV2.Domain.Aggregates.Salon;

namespace SvxlinkManagerV2.Presentation.Services;

/// <summary>
/// Valide une liste de serveurs réflecteur saisie dans un formulaire.
/// </summary>
/// <remarks>
/// Délègue à <see cref="ReflectorHosts"/> plutôt que de redire la règle en expression
/// régulière : l'agrégat refuse de toute façon une saisie mal formée, et deux définitions
/// finiraient par diverger. L'attribut ne sert qu'à le signaler pendant la frappe.
/// </remarks>
public sealed class ReflectorHostsAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        ReflectorHosts.IsValid(value as string);

    public override string FormatErrorMessage(string name) =>
        "Chaque serveur doit être de la forme hôte[:port], séparés par des virgules.";
}

/// <summary>
/// Valide un domaine de découverte SRV saisi dans un formulaire.
/// </summary>
public sealed class ReflectorDnsDomainAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        ReflectorHosts.IsValidDomain(value as string);

    public override string FormatErrorMessage(string name) =>
        "Le domaine de découverte DNS n'est pas un nom de domaine valide.";
}
