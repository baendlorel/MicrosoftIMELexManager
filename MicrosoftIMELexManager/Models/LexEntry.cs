using CommunityToolkit.Mvvm.ComponentModel;

namespace MicrosoftIMELexManager.Models;

public sealed partial class LexEntry : ObservableObject
{
    [ObservableProperty]
    private string _displayIndex = string.Empty;

    [ObservableProperty]
    private string _pinyin = string.Empty;

    [ObservableProperty]
    private string _phrase = string.Empty;

    /// <summary>
    /// Candidate position index (1-9). 1 = highest priority.
    /// </summary>
    [ObservableProperty]
    private int _candidateIndex = 1;

    public override string ToString() => $"{Pinyin} → {Phrase} (pos={CandidateIndex})";
}
