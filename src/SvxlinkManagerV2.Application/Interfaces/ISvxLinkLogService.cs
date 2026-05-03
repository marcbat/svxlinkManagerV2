using SvxlinkManagerV2.Application.Models;

namespace SvxlinkManagerV2.Application.Interfaces;

public interface ISvxLinkLogService
{
    IReadOnlyList<SvxLinkLogEntry> GetLogs();

    int MaxLines { get; set; }

    void Clear();

    void AddLog(string rawLine);

    event Action<SvxLinkLogEntry>? OnLogReceived;
}
