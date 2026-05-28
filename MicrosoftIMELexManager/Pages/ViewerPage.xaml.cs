using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MicrosoftIMELexManager.Models;
using MicrosoftIMELexManager.Services;

namespace MicrosoftIMELexManager.Pages;

public sealed partial class ViewerPage : Page
{
    public ViewerPage()
    {
        InitializeComponent();
        ShowEmptyState();
    }

    public async Task LoadFileAsync(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                ShowError($"文件不存在: {filePath ?? "未知路径"}");
                return;
            }

            var fileInfo = new FileInfo(filePath);

            // 更新文件信息
            FileNameText.Text = fileInfo.Name;
            FileInfoText.Text = $"路径: {fileInfo.DirectoryName}";
            FileSizeText.Text = FormatFileSize(fileInfo.Length);
            ModifiedTimeText.Text = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");

            // 根据文件扩展名判断类型并解析
            var extension = fileInfo.Extension.ToLowerInvariant();
            var fileType = "";

            if (extension == ".lex")
            {
                fileType = "自定义短语";
                await LoadLexFileAsync(filePath);
            }
            else if (extension == ".dat")
            {
                // 判断是 IH 还是 UDL
                if (fileInfo.Name.Contains("IH.dat"))
                {
                    fileType = "输入历史 (IH.dat)";
                    await LoadIHFileAsync(filePath);
                }
                else if (fileInfo.Name.Contains("UDL.dat"))
                {
                    fileType = "自学习词汇 (UDL.dat)";
                    await LoadUDLFileAsync(filePath);
                }
                else
                {
                    fileType = "数据文件";
                    ShowUnknownFile(filePath);
                }
            }
            else
            {
                fileType = "未知文件";
                ShowUnknownFile(filePath);
            }

            FileTypeText.Text = fileType;
            HideEmptyState();
        }
        catch (Exception ex)
        {
            ShowError($"加载文件失败: {ex.Message}\n\n堆栈跟踪: {ex.StackTrace}");
        }
    }

    private async Task LoadLexFileAsync(string path)
    {
        var service = new LexFileService();
        var entries = await Task.Run(() => service.Read(path));

        EntryCountText.Text = entries.Count.ToString();

        // 显示前 100 条记录作为预览
        var preview = entries.Take(100)
            .Select(e => $"{e.Pinyin} → {e.Phrase} (候选位置: {e.CandidateIndex})")
            .ToList();

        ContentView.ItemsSource = preview;
    }

    private async Task LoadIHFileAsync(string path)
    {
        var service = new IHFileService();
        var entries = await Task.Run(() => service.Read(path));

        EntryCountText.Text = entries.Count.ToString();

        // 显示前 100 条记录作为预览
        var preview = entries.Take(100)
            .Select(e => $"{e.Word} (词频: {e.Frequency}, 时间戳: 0x{e.Timestamp:X8})")
            .ToList();

        ContentView.ItemsSource = preview;
    }

    private async Task LoadUDLFileAsync(string path)
    {
        var service = new UDLFileService();
        var entries = await Task.Run(() => service.Read(path));

        EntryCountText.Text = entries.Count.ToString();

        // 显示前 100 条记录作为预览
        var preview = entries.Take(100)
            .Select(e => $"{e.Word} ({e.PinyinText}, 插入时间: 0x{e.Timestamp:X8})")
            .ToList();

        ContentView.ItemsSource = preview;
    }

    private void ShowUnknownFile(string path)
    {
        EntryCountText.Text = "未知";
        ContentView.ItemsSource = new[] { "无法识别的文件格式" };
    }

    private void ShowError(string message)
    {
        FileNameText.Text = "加载失败";
        FileInfoText.Text = message;
        FileTypeText.Text = "-";
        FileSizeText.Text = "-";
        EntryCountText.Text = "-";
        ModifiedTimeText.Text = "-";
        ShowEmptyState();
    }

    private void ShowEmptyState()
    {
        EmptyState.Visibility = Visibility.Visible;
        ContentView.Visibility = Visibility.Collapsed;
    }

    private void HideEmptyState()
    {
        EmptyState.Visibility = Visibility.Collapsed;
        ContentView.Visibility = Visibility.Visible;
    }

    private static string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB" };
        int counter = 0;
        double number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}
