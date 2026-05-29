using System;
using System.Diagnostics;
using System.Collections.Generic;
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
        Log("ViewerPage 已构造。");
        ShowEmptyState();
    }

    public async Task LoadFileAsync(string filePath)
    {
        Log($"LoadFileAsync 开始: path={filePath}");
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Log("文件不存在或路径为空。");
            ShowError("文件不存在", filePath ?? "未知路径");
            return;
        }

        ShowLoading("正在读取文件...");

        try
        {
            var fileInfo = new FileInfo(filePath);
            Log($"文件信息: name={fileInfo.Name}, length={fileInfo.Length}, instance={GetHashCode()}");

            FileNameText.Text = fileInfo.Name;
            FileInfoText.Text = $"路径: {fileInfo.DirectoryName}";
            FileSizeText.Text = FormatFileSize(fileInfo.Length);
            ModifiedTimeText.Text = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");

            var extension = fileInfo.Extension.ToLowerInvariant();
            Log($"识别扩展名: {extension}");

            if (extension == ".lex")
            {
                Log("按 .lex 文件处理。");
                FileTypeText.Text = "自定义短语 (.lex)";
                await LoadLexFileAsync(filePath);
            }
            else if (extension == ".dat")
            {
                if (fileInfo.Name.Contains("IH", StringComparison.OrdinalIgnoreCase))
                {
                    Log("识别为 IH.dat 文件。");
                    FileTypeText.Text = fileInfo.Name.Contains("Beta")
                        ? "输入历史 Beta (IH.dat)"
                        : "输入历史 (IH.dat)";
                    await LoadIHFileAsync(filePath);
                }
                else if (fileInfo.Name.Contains("UDL", StringComparison.OrdinalIgnoreCase))
                {
                    Log("识别为 UDL.dat 文件。");
                    FileTypeText.Text = fileInfo.Name.Contains("Beta")
                        ? "自学习词汇 Beta (UDL.dat)"
                        : "自学习词汇 (UDL.dat)";
                    await LoadUDLFileAsync(filePath);
                }
                else
                {
                    Log("无法识别具体 .dat 文件类型。");
                    FileTypeText.Text = "数据文件";
                    ShowError("无法识别的数据文件", $"不支持直接查看此 .dat 文件: {fileInfo.Name}\n\n目前支持: ChsPinyinIH.dat, ChsPinyinUDL.dat");
                }
            }
            else
            {
                Log("不支持的扩展名。");
                FileTypeText.Text = "未知格式";
                ShowError("无法识别的文件格式", $"不支持的文件扩展名: {extension}\n\n请打开 .lex 或 .dat 文件");
            }
        }
        catch (Exception ex)
        {
            Log($"LoadFileAsync 异常: {ex}");
            ShowError("加载失败", ex.Message);
        }
    }

    private async Task LoadLexFileAsync(string path)
    {
        ShowLoading("正在解析自定义短语...");

        var service = new LexFileService();
        var entries = await Task.Run(() => service.Read(path));
        Log($"Lex 解析完成: {entries.Count} 条。");

        EntryCountText.Text = entries.Count.ToString();

        ShowLoading($"正在渲染 {entries.Count:N0} 条记录...");

        var display = entries
            .Select(e => $"{e.Pinyin} → {e.Phrase}  (候选位置: {e.CandidateIndex})")
            .ToList();

        ContentView.ItemsSource = display;
        ShowContent();
    }

    private async Task LoadIHFileAsync(string path)
    {
        ShowLoading("正在解析输入历史...");

        var service = new IHFileService();
        var entries = await Task.Run(() => service.Read(path));
        Log($"IH 解析完成: {entries.Count} 条。");

        EntryCountText.Text = entries.Count.ToString();

        ShowLoading($"正在渲染 {entries.Count:N0} 条记录...");

        var display = entries
            .Select(e => $"{e.Word}  (词频: {e.Frequency}, 时间戳: 0x{e.Timestamp:X8})")
            .ToList();

        ContentView.ItemsSource = display;
        ShowContent();
    }

    private async Task LoadUDLFileAsync(string path)
    {
        ShowLoading("正在解析自学习词汇...");

        var service = new UDLFileService();
        var entries = await Task.Run(() => service.Read(path));
        Log($"UDL 解析完成: {entries.Count} 条。");

        EntryCountText.Text = entries.Count.ToString();

        ShowLoading($"正在渲染 {entries.Count:N0} 条记录...");

        var display = entries
            .Select(e => $"{e.Word}  ({e.PinyinText}, 时间戳: 0x{e.Timestamp:X8})")
            .ToList();

        ContentView.ItemsSource = display;
        ShowContent();
    }

    private void ShowLoading(string message)
    {
        Log($"显示加载状态: {message}");
        LoadingText.Text = message;
        LoadingOverlay.Visibility = Visibility.Visible;
        ErrorState.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Collapsed;
        ContentView.Visibility = Visibility.Collapsed;
    }

    private void ShowContent()
    {
        Log("显示内容视图。");
        LoadingOverlay.Visibility = Visibility.Collapsed;
        ErrorState.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Collapsed;
        ContentView.Visibility = Visibility.Visible;
    }

    private void ShowError(string title, string message)
    {
        Log($"显示错误: {title} - {message}");
        ErrorTitleText.Text = title;
        ErrorMessageText.Text = message;
        ErrorState.Visibility = Visibility.Visible;
        LoadingOverlay.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Collapsed;
        ContentView.Visibility = Visibility.Collapsed;
    }

    private void ShowEmptyState()
    {
        Log("显示空状态。");
        EmptyState.Visibility = Visibility.Visible;
        LoadingOverlay.Visibility = Visibility.Collapsed;
        ErrorState.Visibility = Visibility.Collapsed;
        ContentView.Visibility = Visibility.Collapsed;
    }

    private void Log(string message)
    {
        var formatted = $"[ViewerPage#{GetHashCode()} {DateTime.Now:HH:mm:ss.fff}] {message}";
        Debug.WriteLine(formatted);
        Console.WriteLine(formatted);
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
