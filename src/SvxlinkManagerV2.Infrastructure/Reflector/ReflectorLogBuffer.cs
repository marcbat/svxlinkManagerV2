using SvxlinkManagerV2.Application.Interfaces;
using SvxlinkManagerV2.Application.Models;

namespace SvxlinkManagerV2.Infrastructure.Reflector;

/// <summary>
/// Buffer circulaire thread-safe pour les logs du daemon svxreflector.
/// Stocke les N dernières lignes en mémoire (pas de persistance DB).
/// Singleton — doit survivre entre les requêtes.
/// </summary>
public class ReflectorLogBuffer : IReflectorLogService
{
    private readonly LinkedList<SvxLinkLogEntry> _buffer = new();
    private readonly object _lock = new();
    private int _maxLines = 1000;

    public int MaxLines
    {
        get
        {
            lock (_lock) return _maxLines;
        }
        set
        {
            lock (_lock)
            {
                _maxLines = Math.Max(100, Math.Min(value, 10000));
                TrimBuffer();
            }
        }
    }

    public event Action<SvxLinkLogEntry>? OnLogReceived;

    public void AddLog(string rawLine)
    {
        if (string.IsNullOrEmpty(rawLine))
            return;

        var level = ParseLevel(rawLine);
        var entry = new SvxLinkLogEntry(DateTime.Now, rawLine, level);

        lock (_lock)
        {
            _buffer.AddLast(entry);
            TrimBuffer();
        }

        OnLogReceived?.Invoke(entry);
    }

    public IReadOnlyList<SvxLinkLogEntry> GetLogs()
    {
        lock (_lock)
        {
            return _buffer.ToList().AsReadOnly();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _buffer.Clear();
        }
    }

    private void TrimBuffer()
    {
        while (_buffer.Count > _maxLines)
            _buffer.RemoveFirst();
    }

    private static SvxLinkLogLevel ParseLevel(string line)
    {
        if (line.Contains("*** ERROR", StringComparison.OrdinalIgnoreCase))
            return SvxLinkLogLevel.Error;

        if (line.Contains("*** WARNING", StringComparison.OrdinalIgnoreCase))
            return SvxLinkLogLevel.Warning;

        return SvxLinkLogLevel.Info;
    }
}
