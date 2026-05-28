namespace MicrosoftIMELexManager.Models;

public sealed class LexEntry
{
    public string Pinyin { get; set; } = string.Empty;

    public string Phrase { get; set; } = string.Empty;

    /// <summary>
    /// Candidate position index (1-9). 1 = highest priority.
    /// </summary>
    public int CandidateIndex { get; set; } = 1;

    public override string ToString() => $"{Pinyin} → {Phrase} (pos={CandidateIndex})";
}
