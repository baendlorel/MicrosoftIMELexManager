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

public partial class IHViewModel : ObservableObject
{
    private readonly IHFileService _service = new();
    private string _filePath = string.Empty;

    [ObservableProperty]
    private bool _isModified;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<IHEntry> AllEntries { get; } = new();
    public ObservableCollection<IHEntry> FilteredEntries { get; } = new();

    public IHViewModel()
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
    public async Task SaveAsync(string path)
    {
        await Task.Run(() => _service.Write(_filePath, path, AllEntries.ToList()));
        _filePath = path;
        IsModified = false;
    }

    [RelayCommand]
    public void DeleteSelected(object? selectedItems)
    {
        if (selectedItems is null) return;

        var items = selectedItems as IList<object> ?? new List<object>();
        if (items.Count == 0) return;

        foreach (var item in items.OfType<IHEntry>().ToList())
        {
            AllEntries.Remove(item);
        }

        ApplyFilter();
        IsModified = true;
    }

    [RelayCommand]
    public void ResetFrequency(IHEntry? entry)
    {
        if (entry is null) return;
        entry.Frequency = 1;
        IsModified = true;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredEntries.Clear();
        var filtered = (string.IsNullOrWhiteSpace(SearchText)
            ? AllEntries
            : AllEntries.Where(e => e.Word.Contains(SearchText, StringComparison.Ordinal)))
            .ToList();

        for (int i = 0; i < filtered.Count; i++)
        {
            var entry = filtered[i];
            entry.DisplayIndex = $"{i + 1}.";
            FilteredEntries.Add(entry);
        }
    }
}
