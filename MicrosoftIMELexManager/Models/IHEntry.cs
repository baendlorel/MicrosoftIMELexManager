using MicrosoftIMELexManager.Services;

namespace MicrosoftIMELexManager.Models;

public sealed class IHEntry
{
    public string DisplayIndex { get; set; } = string.Empty;

    public string Word { get; set; } = string.Empty;

    /// <summary>
    /// Frequency level (1-4). 4 = most frequent.
    /// </summary>
    public uint Frequency { get; set; } = 1;

    /// <summary>
    /// Timestamp in seconds since 2000-01-01 00:00:00 UTC.
    /// </summary>
    public uint Timestamp { get; set; }

    public string FormattedTimestamp => MicrosoftImeTimestamp.FormatTimestamp(Timestamp);

    public override string ToString() => $"{Word} (freq={Frequency}, ts=0x{Timestamp:X8})";
}
