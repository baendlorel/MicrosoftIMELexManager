using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicrosoftIMELexManager.Models;
using MicrosoftIMELexManager.Services;

namespace MicrosoftIMELexManager.ViewModels;

public partial class LexViewModel : ObservableObject
{
    private readonly LexFileService _service = new();
    private string _filePath = string.Empty;

    [ObservableProperty]
    private bool _isModified;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<LexEntry> AllEntries { get; } = new();
    public ObservableCollection<LexEntry> FilteredEntries { get; } = new();

    public LexViewModel()
    {
    }

    [RelayCommand]
    public async Task LoadAsync(string path)
    {
        _filePath = path;
        AllEntries.Clear();

        var entries = await Task.Run(() => _service.Read(path));
        foreach (var entry in entries)
            AllEntries.Add(entry);

        ApplyFilter();
        IsModified = false;
    }

    [RelayCommand]
    public void AddNew()
    {
        var newEntry = new LexEntry
        {
            Pinyin = "new",
            Phrase = "新词条",
            CandidateIndex = 1
        };
        AllEntries.Add(newEntry);
        ApplyFilter();
        IsModified = true;
    }

    [RelayCommand]
    public void Delete(LexEntry? entry)
    {
        if (entry is null) return;
        AllEntries.Remove(entry);
        ApplyFilter();
        IsModified = true;
    }

    [RelayCommand]
    public async Task SaveAsync(string path)
    {
        await Task.Run(() => _service.Write(path, AllEntries.ToList()));
        _filePath = path;
        IsModified = false;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredEntries.Clear();
        var filtered = (string.IsNullOrWhiteSpace(SearchText)
            ? AllEntries
            : AllEntries.Where(e => e.Pinyin.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                    e.Phrase.Contains(SearchText, StringComparison.Ordinal)))
            .ToList();

        for (int i = 0; i < filtered.Count; i++)
        {
            var entry = filtered[i];
            entry.DisplayIndex = $"{i + 1}.";
            FilteredEntries.Add(entry);
        }
    }
}
