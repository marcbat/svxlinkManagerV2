using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Application.Interfaces;

namespace SvxlinkManagerV2.Infrastructure.Persistence;

/// <summary>
/// Implémentation du service de token à usage unique pour l'auto-login post-wizard.
/// Utilise un ConcurrentDictionary en mémoire avec TTL de 5 minutes.
/// Doit être enregistré comme singleton.
/// </summary>
public class PendingSetupLoginService : IPendingSetupLoginService
{
    private readonly ConcurrentDictionary<string, (string Username, DateTime Expiry)> _tokens = new();
    private readonly ILogger<PendingSetupLoginService> _logger;
    private static readonly TimeSpan TokenTtl = TimeSpan.FromMinutes(5);

    public PendingSetupLoginService(ILogger<PendingSetupLoginService> logger)
    {
        _logger = logger;
    }

    public string GenerateToken(string username)
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _tokens)
        {
            if (kvp.Value.Expiry < now)
                _tokens.TryRemove(kvp.Key, out _);
        }

        var token = Guid.NewGuid().ToString("N");
        _tokens[token] = (username, now.Add(TokenTtl));
        _logger.LogDebug("Token de setup généré pour {Username}", username);
        return token;
    }

    public string? ConsumeToken(string token)
    {
        if (_tokens.TryRemove(token, out var entry))
        {
            if (entry.Expiry >= DateTime.UtcNow)
            {
                _logger.LogDebug("Token de setup consommé pour {Username}", entry.Username);
                return entry.Username;
            }
            _logger.LogWarning("Token de setup expiré");
        }
        else
        {
            _logger.LogWarning("Token de setup invalide ou déjà utilisé");
        }
        return null;
    }
}
