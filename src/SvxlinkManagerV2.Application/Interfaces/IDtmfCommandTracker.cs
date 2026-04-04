namespace SvxlinkManagerV2.Application.Interfaces;

/// <summary>
/// Service de tracking des commandes DTMF reçues via les logs SVXLink.
/// Émet un événement lorsqu'une commande DTMF complète est détectée.
/// </summary>
public interface IDtmfCommandTracker
{
    /// <summary>
    /// Événement déclenché lorsqu'une commande DTMF est reçue.
    /// Le paramètre string contient le code DTMF brut (ex: "96").
    /// </summary>
    event Action<string>? OnDtmfCommandReceived;
}
