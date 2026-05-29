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
        // TODO: 实现导入功能
        var dialog = new ContentDialog
        {
            Title = "导入词条",
            Content = "导入功能即将推出",
            CloseButtonText = "确定",
            XamlRoot = XamlRoot
        };
        await dialog.ShowAsync();
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
