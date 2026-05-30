using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MicrosoftIMELexManager.Models;
using MicrosoftIMELexManager.ViewModels;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace MicrosoftIMELexManager.Pages;

public sealed partial class LexPage : Page
{
    public LexViewModel ViewModel => (LexViewModel)Resources["ViewModel"];

    public LexPage()
    {
        InitializeComponent();
    }

    private void AddNew_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AddNewCommand.Execute(null);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (LexListView.SelectedItems.Count > 0)
        {
            foreach (var item in LexListView.SelectedItems.ToList())
            {
                ViewModel.DeleteCommand.Execute(item);
            }
        }
    }

    private void CandidateIndexMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { CommandParameter: LexEntry entry, Tag: string tag })
        {
            return;
        }

        if (!int.TryParse(tag, out int candidateIndex))
        {
            return;
        }

        entry.CandidateIndex = candidateIndex;
        ViewModel.IsModified = true;
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var filePicker = new FileOpenPicker();
            if (App.MainWindow is not null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);
            }

            filePicker.ViewMode = PickerViewMode.List;
            filePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            filePicker.FileTypeFilter.Add(".csv");

            var file = await filePicker.PickSingleFileAsync();
            if (file is null) return;

            var text = await FileIO.ReadTextAsync(file, Windows.Storage.Streams.UnicodeEncoding.Utf8);
            var rows = ParseCsv(text);

            if (rows.Count == 0)
            {
                var emptyDialog = new ContentDialog
                {
                    Title = "导入失败",
                    Content = "CSV 文件中未找到有效数据。请确保文件包含「拼音,词语,候选词位置」格式的行。",
                    CloseButtonText = "确定",
                    XamlRoot = XamlRoot
                };
                await emptyDialog.ShowAsync();
                return;
            }

            var (imported, skipped) = ViewModel.ImportFromCsv(rows);

            var dialog = new ContentDialog
            {
                Title = "导入完成",
                Content = $"导入完成。\n\n成功导入：{imported} 条\n跳过重复：{skipped} 条",
                CloseButtonText = "确定",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "导入失败",
                Content = ex.Message,
                CloseButtonText = "确定",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    private static System.Collections.Generic.List<(string Pinyin, string Phrase, int CandidateIndex)> ParseCsv(string text)
    {
        var result = new System.Collections.Generic.List<(string, string, int)>();
        using var reader = new StringReader(text);
        bool firstLine = true;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (firstLine)
            {
                firstLine = false;
                if (line.StartsWith("拼音", StringComparison.Ordinal) ||
                    line.StartsWith("pinyin", StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = SplitCsvLine(line);
            if (cols.Count < 2) continue;

            var pinyin = cols[0].Trim();
            var phrase = cols[1].Trim();
            if (string.IsNullOrEmpty(pinyin) || string.IsNullOrEmpty(phrase)) continue;

            int candidateIndex = 1;
            if (cols.Count >= 3 && int.TryParse(cols[2].Trim(), out int parsed))
                candidateIndex = parsed;

            result.Add((pinyin, phrase, candidateIndex));
        }
        return result;
    }

    private static System.Collections.Generic.List<string> SplitCsvLine(string line)
    {
        var fields = new System.Collections.Generic.List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
        }
        fields.Add(sb.ToString());
        return fields;
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var filePicker = new FileSavePicker();
            if (App.MainWindow is not null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);
            }

            filePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            filePicker.SuggestedFileName = "自定义短语导出";
            filePicker.FileTypeChoices.Add("CSV 文件", new[] { ".csv" });

            var file = await filePicker.PickSaveFileAsync();
            if (file is null)
            {
                return;
            }

            var csv = BuildCsv(ViewModel.AllEntries);
            await FileIO.WriteTextAsync(file, csv, Windows.Storage.Streams.UnicodeEncoding.Utf8);

            var dialog = new ContentDialog
            {
                Title = "导出完成",
                Content = $"已导出到：{file.Path}",
                CloseButtonText = "确定",
                XamlRoot = XamlRoot
            };

            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "导出失败",
                Content = ex.Message,
                CloseButtonText = "确定",
                XamlRoot = XamlRoot
            };

            await dialog.ShowAsync();
        }
    }

    private static string BuildCsv(System.Collections.Generic.IEnumerable<LexEntry> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("拼音,词语,候选词位置");

        foreach (var entry in entries)
        {
            builder.Append(EscapeCsv(entry.Pinyin));
            builder.Append(',');
            builder.Append(EscapeCsv(entry.Phrase));
            builder.Append(',');
            builder.Append(entry.CandidateIndex);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
