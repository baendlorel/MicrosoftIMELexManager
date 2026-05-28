using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MicrosoftIMELexManager.Pages;
using MicrosoftIMELexManager.Services;
using Windows.Storage.Pickers;

namespace MicrosoftIMELexManager;

public sealed partial class MainWindow : Window
{
    private LexPage? _lexPage;
    private IHPage? _ihPage;
    private UDLPage? _udlPage;
    private ViewerPage? _viewerPage;

    public MainWindow()
    {
        InitializeComponent();
        InitializePages();
        Closed += MainWindow_Closed;
    }

    private void InitializePages()
    {
        _lexPage = new LexPage();
        _ihPage = new IHPage();
        _udlPage = new UDLPage();
        _viewerPage = new ViewerPage();

        LexFrame.Navigate(_lexPage.GetType());
        IHFrame.Navigate(_ihPage.GetType());
        UDLFrame.Navigate(_udlPage.GetType());
        ViewerFrame.Navigate(_viewerPage.GetType());

        _ = TryAutoLoadAsync();
    }

    private async Task TryAutoLoadAsync()
    {
        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var imePath = Path.Combine(appDataPath, @"Microsoft\InputMethod\Chs");

            if (Directory.Exists(imePath))
            {
                var lexFile = Path.Combine(imePath, "ChsPinyinEUDPv1.lex");
                var ihFile = Path.Combine(imePath, "ChsPinyinIH.dat");
                var udlFile = Path.Combine(imePath, "ChsPinyinUDL.dat");

                if (File.Exists(lexFile) && _lexPage != null)
                    await _lexPage.ViewModel.LoadAsync(lexFile);
                if (File.Exists(ihFile) && _ihPage != null)
                    await _ihPage.ViewModel.LoadAsync(ihFile);
                if (File.Exists(udlFile) && _udlPage != null)
                    await _udlPage.ViewModel.LoadAsync(udlFile);

                UpdateStatus(imePath);
            }
        }
        catch
        {
            // Silent fail on auto-load
        }
    }

    private async void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var folderPicker = new FolderPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

        folderPicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
        var folder = await folderPicker.PickSingleFolderAsync();

        if (folder != null)
        {
            await LoadFromPathAsync(folder.Path);
        }
    }

    private async void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var filePicker = new Windows.Storage.Pickers.FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

        // 设置视图和文件类型
        filePicker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
        filePicker.FileTypeFilter.Add(".lex");
        filePicker.FileTypeFilter.Add(".dat");
        filePicker.FileTypeFilter.Add("*"); // 允许所有文件

        var file = await filePicker.PickSingleFileAsync();
        if (file != null)
        {
            try
            {
                // 获取文件路径
                var filePath = file.Path;

                if (string.IsNullOrEmpty(filePath))
                {
                    await ShowErrorDialog("文件路径错误", "无法获取文件路径，请尝试使用\"打开文件夹\"功能");
                    return;
                }

                // 切换到查看标签页并加载文件
                MainTabView.SelectedIndex = 3; // ViewerTab
                if (_viewerPage != null)
                {
                    await _viewerPage.LoadFileAsync(filePath);
                }
                UpdateStatus($"查看文件: {file.Name}");
            }
            catch (Exception ex)
            {
                await ShowErrorDialog("打开文件失败", $"无法打开文件: {ex.Message}\n\n文件: {file.Name}\n路径: {file.Path}");
            }
        }
    }

    private async Task LoadFromPathAsync(string path)
    {
        try
        {
            var lexFile = Path.Combine(path, "ChsPinyinEUDPv1.lex");
            var ihFile = Path.Combine(path, "ChsPinyinIH.dat");
            var udlFile = Path.Combine(path, "ChsPinyinUDL.dat");

            if (File.Exists(lexFile) && _lexPage != null)
                await _lexPage.ViewModel.LoadAsync(lexFile);
            if (File.Exists(ihFile) && _ihPage != null)
                await _ihPage.ViewModel.LoadAsync(ihFile);
            if (File.Exists(udlFile) && _udlPage != null)
                await _udlPage.ViewModel.LoadAsync(udlFile);

            UpdateStatus(path);
        }
        catch (Exception ex)
        {
            await ShowErrorDialog("加载失败", ex.Message);
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        // 检测输入法进程
        if (BackupService.IsIMEProcessRunning())
        {
            var warningDialog = new ContentDialog
            {
                Title = "警告",
                Content = "检测到输入法进程正在运行，建议先关闭输入法再保存文件，否则可能导致文件损坏。\n\n是否继续保存？",
                PrimaryButtonText = "继续保存",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };

            var result = await warningDialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
                return;
        }

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var imePath = Path.Combine(appDataPath, @"Microsoft\InputMethod\Chs");

        try
        {
            // 创建备份
            var backupFiles = new List<string>();

            if (_lexPage != null && _lexPage.ViewModel.IsModified)
            {
                var lexFile = Path.Combine(imePath, "ChsPinyinEUDPv1.lex");
                var backup = BackupService.CreateBackupBeforeWrite(lexFile);
                if (backup != null) backupFiles.Add(backup);
                await _lexPage.ViewModel.SaveAsync(lexFile);
            }

            if (_ihPage != null && _ihPage.ViewModel.IsModified)
            {
                var ihFile = Path.Combine(imePath, "ChsPinyinIH.dat");
                var backup = BackupService.CreateBackupBeforeWrite(ihFile);
                if (backup != null) backupFiles.Add(backup);
                await _ihPage.ViewModel.SaveAsync(ihFile);
            }

            if (_udlPage != null && _udlPage.ViewModel.IsModified)
            {
                var udlFile = Path.Combine(imePath, "ChsPinyinUDL.dat");
                var backup = BackupService.CreateBackupBeforeWrite(udlFile);
                if (backup != null) backupFiles.Add(backup);
                await _udlPage.ViewModel.SaveAsync(udlFile);
            }

            var message = "所有修改已保存";
            if (backupFiles.Count > 0)
            {
                message += $"\n\n已创建备份文件:\n{string.Join("\n", backupFiles.Select(Path.GetFileName))}";
            }

            var dialog = new ContentDialog
            {
                Title = "保存成功",
                Content = message,
                CloseButtonText = "确定",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorDialog("保存失败", ex.Message);
        }
    }

    private async void Backup_Click(object sender, RoutedEventArgs e)
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var imePath = Path.Combine(appDataPath, @"Microsoft\InputMethod\Chs");

        if (!Directory.Exists(imePath))
        {
            await ShowErrorDialog("错误", "词库目录不存在");
            return;
        }

        try
        {
            var backupFiles = new List<string>();

            // 备份所有词库文件
            var lexFile = Path.Combine(imePath, "ChsPinyinEUDPv1.lex");
            var ihFile = Path.Combine(imePath, "ChsPinyinIH.dat");
            var udlFile = Path.Combine(imePath, "ChsPinyinUDL.dat");

            if (File.Exists(lexFile))
                backupFiles.Add(BackupService.CreateBackup(lexFile));
            if (File.Exists(ihFile))
                backupFiles.Add(BackupService.CreateBackup(ihFile));
            if (File.Exists(udlFile))
                backupFiles.Add(BackupService.CreateBackup(udlFile));

            var dialog = new ContentDialog
            {
                Title = "备份完成",
                Content = $"已创建 {backupFiles.Count} 个备份文件:\n\n{string.Join("\n", backupFiles.Select(Path.GetFileName))}",
                CloseButtonText = "确定",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorDialog("备份失败", ex.Message);
        }
    }

    private async void CleanBackups_Click(object sender, RoutedEventArgs e)
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var imePath = Path.Combine(appDataPath, @"Microsoft\InputMethod\Chs");

        if (!Directory.Exists(imePath))
        {
            await ShowErrorDialog("错误", "词库目录不存在");
            return;
        }

        // 先显示当前备份文件数量
        var allBackups = BackupService.GetBackupFiles(imePath, "*.bak");

        var confirmDialog = new ContentDialog
        {
            Title = "清理旧备份",
            Content = $"当前共有 {allBackups.Length} 个备份文件。\n\n将保留最新的 5 个备份，删除其他旧备份。\n\n是否继续？",
            PrimaryButtonText = "清理",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        var result = await confirmDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            try
            {
                BackupService.CleanAllOldBackups(imePath, keepCount: 5);

                var remainingBackups = BackupService.GetBackupFiles(imePath, "*.bak");
                var deletedCount = allBackups.Length - remainingBackups.Length;

                var dialog = new ContentDialog
                {
                    Title = "清理完成",
                    Content = $"已删除 {deletedCount} 个旧备份文件。\n\n当前保留 {remainingBackups.Length} 个备份文件。",
                    CloseButtonText = "确定",
                    XamlRoot = Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorDialog("清理失败", ex.Message);
            }
        }
    }

    private void UpdateStatus(string path)
    {
        CurrentFileText.Text = $"当前路径: {path}";
        var totalEntries = (_lexPage?.ViewModel.AllEntries.Count ?? 0) +
                           (_ihPage?.ViewModel.AllEntries.Count ?? 0) +
                           (_udlPage?.ViewModel.AllEntries.Count ?? 0);
        EntryCountText.Text = $"总条目数: {totalEntries}";
    }

    private async Task ShowErrorDialog(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "确定",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        // Cleanup if needed
    }
}
