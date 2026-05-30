using System;
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
        var duplicates = AllEntries
            .Where(e => !_indicesToDelete.Contains(e.RecordIndex))
            .GroupBy(e => $"{e.Word.Trim()}\t{e.PinyinText.Trim()}")
            .Where(g => g.Count() > 1)
            .Select(g => { var first = g.First(); return $"「{first.Word.Trim()}」（{first.PinyinText.Trim()}）"; })
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                "检测到重复词条，请删除后再保存：\n" + string.Join("\n", duplicates));
        }

        await Task.Run(() => _service.Write(_filePath, path, AllEntries.ToList(), _indicesToDelete));
        await LoadAsync(path);
    }

    /// <summary>
    /// 从 CSV 导入词条（格式：拼音,词语），跳过与已有词条重复的项，返回跳过数量。
    /// </summary>
    public (int imported, int skipped) ImportFromCsv(IEnumerable<(string Pinyin, string Word)> rows)
    {
        var existing = AllEntries
            .Where(e => !_indicesToDelete.Contains(e.RecordIndex))
            .Select(e => (e.Word.Trim(), e.PinyinText.Trim()))
            .ToHashSet();

        int imported = 0, skipped = 0;
        foreach (var (pinyin, word) in rows)
        {
            var key = (word.Trim(), pinyin.Trim());
            if (existing.Contains(key))
            {
                skipped++;
                continue;
            }

            AllEntries.Insert(0, new UDLEntry
            {
                Word = word.Trim(),
                PinyinText = pinyin.Trim(),
                Timestamp = 0,
                RecordIndex = UDLEntry.UnassignedRecordIndex,
            });
            existing.Add(key);
            imported++;
        }

        ApplyFilter();
        if (imported > 0) IsModified = true;
        return (imported, skipped);
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
