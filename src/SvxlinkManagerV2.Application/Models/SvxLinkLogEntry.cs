namespace SvxlinkManagerV2.Application.Models;

public enum SvxLinkLogLevel
{
    Info,
    Warning,
    Error
}

public record SvxLinkLogEntry(DateTime Timestamp, string Message, SvxLinkLogLevel Level);
