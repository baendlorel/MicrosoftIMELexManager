namespace MicrosoftIMELexManager.Models;

public sealed class UDLEntry
{
    public string Word { get; set; } = string.Empty;

    /// <summary>
    /// Pinyin decoded from internal index table (readable string).
    /// </summary>
    public string PinyinText { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp (lower 32 bits of .NET DateTime.Ticks).
    /// </summary>
    public uint Timestamp { get; set; }

    /// <summary>
    /// 0-based index of this record in the file (needed for delete).
    /// </summary>
    public int RecordIndex { get; set; }

    public override string ToString() => $"{Word} ({PinyinText}) ts=0x{Timestamp:X8}";
}
