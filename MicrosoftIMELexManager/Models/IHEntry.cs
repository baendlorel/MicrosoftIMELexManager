namespace MicrosoftIMELexManager.Models;

public sealed class IHEntry
{
    public string Word { get; set; } = string.Empty;

    /// <summary>
    /// Frequency level (1-4). 4 = most frequent.
    /// </summary>
    public uint Frequency { get; set; } = 1;

    /// <summary>
    /// Timestamp (lower 32 bits of .NET DateTime.Ticks).
    /// </summary>
    public uint Timestamp { get; set; }

    public override string ToString() => $"{Word} (freq={Frequency}, ts=0x{Timestamp:X8})";
}
