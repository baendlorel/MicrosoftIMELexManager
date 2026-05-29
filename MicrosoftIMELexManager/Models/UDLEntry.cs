namespace MicrosoftIMELexManager.Models;

public sealed class UDLEntry
{
    public const int UnassignedRecordIndex = -1;

    public string DisplayIndex { get; set; } = string.Empty;

    public string Word { get; set; } = string.Empty;

    /// <summary>
    /// Pinyin decoded from internal index table (readable string).
    /// </summary>
    public string PinyinText { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp in seconds since 2000-01-01 00:00:00 UTC.
    /// </summary>
    public uint Timestamp { get; set; }

    /// <summary>
    /// 0-based index of this record in the file (needed for delete).
    /// </summary>
    public int RecordIndex { get; set; } = UnassignedRecordIndex;

    public bool IsNew => RecordIndex == UnassignedRecordIndex;

    public override string ToString() => $"{Word} ({PinyinText}) ts=0x{Timestamp:X8}";
}
