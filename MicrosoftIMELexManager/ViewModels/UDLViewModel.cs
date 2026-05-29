using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicrosoftIMELexManager.Models;
using MicrosoftIMELexManager.Services;

namespace MicrosoftIMELexManager.ViewModels;

public partial class UDLViewModel : ObservableObject
{
    private readonly UDLFileService _service = new();
    private string _filePath = string.Empty;
    private readonly HashSet<int> _indicesToDelete = new();

    [ObservableProperty]
    private bool _isModified;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<UDLEntry> AllEntries { get; } = new();
    public ObservableCollection<UDLEntry> FilteredEntries { get; } = new();

    public UDLViewModel()
    {
    }

    [RelayCommand]
    public async Task LoadAsync(string path)
    {
        _filePath = path;
        AllEntries.Clear();
        _indicesToDelete.Clear();

        var entries = await Task.Run(() => _service.Read(path));
        foreach (var entry in entries)
            AllEntries.Add(entry);

        ApplyFilter();
        IsModified = false;
    }

    [RelayCommand]
    public async Task SaveAsync(string path)
    {
        await Task.Run(() => _service.Write(_filePath, path, AllEntries.ToList(), _indicesToDelete));
        await LoadAsync(path);
    }

    [RelayCommand]
    public void AddNew()
    {
        var newEntry = new UDLEntry
        {
            Word = "新词",
            PinyinText = "xin ci",
            Timestamp = 0,
            RecordIndex = UDLEntry.UnassignedRecordIndex,
        };

        AllEntries.Insert(0, newEntry);
        ApplyFilter();
        IsModified = true;
    }

    [RelayCommand]
    public void DeleteSelected(object? selectedItems)
    {
        if (selectedItems is null) return;

        var items = selectedItems as IList<object> ?? new List<object>();
        if (items.Count == 0) return;

        foreach (var item in items.OfType<UDLEntry>().ToList())
        {
            AllEntries.Remove(item);
            _indicesToDelete.Add(item.RecordIndex);
        }

        ApplyFilter();
        IsModified = true;
    }

    [RelayCommand]
    public void MarkModified()
    {
        IsModified = true;
    }

    public void RefreshAfterEdit()
    {
        ApplyFilter();
        IsModified = true;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredEntries.Clear();
        var filtered = (string.IsNullOrWhiteSpace(SearchText)
            ? AllEntries.Where(e => !_indicesToDelete.Contains(e.RecordIndex))
            : AllEntries.Where(e => !_indicesToDelete.Contains(e.RecordIndex) &&
                                    (e.Word.Contains(SearchText, StringComparison.Ordinal) ||
                                    e.PinyinText.Contains(SearchText, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        for (int i = 0; i < filtered.Count; i++)
        {
            var entry = filtered[i];
            entry.DisplayIndex = $"{i + 1}.";
            FilteredEntries.Add(entry);
        }
    }
}
