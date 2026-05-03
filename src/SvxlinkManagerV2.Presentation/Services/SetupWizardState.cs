using SvxlinkManagerV2.Application.Features.Setup;

namespace SvxlinkManagerV2.Presentation.Services;

/// <summary>
/// Service scoped stockant l'état transitoire du wizard de configuration initiale.
/// En Blazor Server, un service scoped est lié au circuit SignalR — un état par session utilisateur.
/// Les données ne sont pas persistées entre les étapes ; elles sont envoyées en une seule commande à la fin.
/// </summary>
public class SetupWizardState
{
    /// <summary>
    /// Données collectées au fil des étapes du wizard.
    /// </summary>
    public SetupData Data { get; set; } = new();
}
