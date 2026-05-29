using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        Log("开始初始化页面。");

        _lexPage = NavigateFrame<LexPage>(LexFrame);
        _ihPage = NavigateFrame<IHPage>(IHFrame);
        _udlPage = NavigateFrame<UDLPage>(UDLFrame);
        _viewerPage = NavigateFrame<ViewerPage>(ViewerFrame);

        Log($"页面初始化完成。Lex={_lexPage.GetHashCode()}, IH={_ihPage.GetHashCode()}, UDL={_udlPage.GetHashCode()}, Viewer={_viewerPage.GetHashCode()}");

        _ = TryAutoLoadAsync();
    }

    private T NavigateFrame<T>(Frame frame) where T : Page
    {
        var success = frame.Navigate(typeof(T));
        Log($"导航到 {typeof(T).Name}: success={success}, content={frame.Content?.GetType().Name ?? "null"}");

        if (frame.Content is T page)
        {
            return page;
        }

        throw new InvalidOperationException($"无法获取 {typeof(T).Name} 的已显示实例。");
    }

    private async Task TryAutoLoadAsync()
    {
        try
        {
            Log("开始自动加载默认词库目录。");
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var imePath = Path.Combine(appDataPath, @"Microsoft\InputMethod\Chs");
            Log($"检查目录: {imePath}");

            if (Directory.Exists(imePath))
            {
                var lexFile = Path.Combine(imePath, "ChsPinyinEUDPv1.lex");
                var ihFile = Path.Combine(imePath, "ChsPinyinIH.dat");
                var udlFile = Path.Combine(imePath, "ChsPinyinUDL.dat");

                if (File.Exists(lexFile) && _lexPage != null)
                {
                    Log($"自动加载 lex: {lexFile}");
                    await _lexPage.ViewModel.LoadAsync(lexFile);
                }
                if (File.Exists(ihFile) && _ihPage != null)
                {
                    Log($"自动加载 ih: {ihFile}");
                    await _ihPage.ViewModel.LoadAsync(ihFile);
                }
                if (File.Exists(udlFile) && _udlPage != null)
                {
                    Log($"自动加载 udl: {udlFile}");
                    await _udlPage.ViewModel.LoadAsync(udlFile);
                }

                UpdateStatus(imePath);
            }
        }
        catch (Exception ex)
        {
            Log($"自动加载失败: {ex}");
        }
    }

    private async void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        Log("用户点击打开单个文件。");
        var filePicker = new Windows.Storage.Pickers.FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

        // 设置视图和文件类型
        filePicker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
        filePicker.FileTypeFilter.Add(".lex");
        filePicker.FileTypeFilter.Add(".dat");

        var file = await filePicker.PickSingleFileAsync();
        Log(file == null ? "文件选择已取消。" : $"已选择文件: name={file.Name}, path={file.Path}");
        if (file != null)
        {
            try
            {
                // 获取文件路径
                var filePath = file.Path;
                Log($"准备打开文件路径: {filePath}");

                if (string.IsNullOrEmpty(filePath))
                {
                    Log("文件路径为空，终止加载。");
                    await ShowErrorDialog("文件路径错误", "无法获取文件路径，请重新选择文件。");
                    return;
                }

                // 切换到查看标签页并加载文件
                MainTabView.SelectedIndex = 3; // ViewerTab
                Log($"已切换到查看标签页。ViewerPage实例: {_viewerPage?.GetHashCode().ToString() ?? "null"}");
                if (_viewerPage != null)
                {
                    Log("开始调用 ViewerPage.LoadFileAsync。");
                    await _viewerPage.LoadFileAsync(filePath);
                    Log("ViewerPage.LoadFileAsync 调用完成。");
                }
                UpdateStatus($"查看文件: {file.Name}");
            }
            catch (Exception ex)
            {
                Log($"打开文件失败: {ex}");
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
            var createdBackupFiles = new List<string>();
            var existingBackupFiles = new List<string>();

            if (_lexPage != null && _lexPage.ViewModel.IsModified)
            {
                var lexFile = Path.Combine(imePath, "ChsPinyinEUDPv1.lex");
                TrackBackupState(lexFile, createdBackupFiles, existingBackupFiles);
                await _lexPage.ViewModel.SaveAsync(lexFile);
            }

            if (_ihPage != null && _ihPage.ViewModel.IsModified)
            {
                var ihFile = Path.Combine(imePath, "ChsPinyinIH.dat");
                TrackBackupState(ihFile, createdBackupFiles, existingBackupFiles);
                await _ihPage.ViewModel.SaveAsync(ihFile);
            }

            if (_udlPage != null && _udlPage.ViewModel.IsModified)
            {
                var udlFile = Path.Combine(imePath, "ChsPinyinUDL.dat");
                TrackBackupState(udlFile, createdBackupFiles, existingBackupFiles);
                await _udlPage.ViewModel.SaveAsync(udlFile);
            }

            var message = "所有修改已保存";
            if (createdBackupFiles.Count > 0)
            {
                message += $"\n\n已创建备份文件:\n{string.Join("\n", createdBackupFiles.Select(Path.GetFileName))}";
            }

            if (existingBackupFiles.Count > 0)
            {
                message += $"\n\n以下备份已存在，未重复创建:\n{string.Join("\n", existingBackupFiles.Select(Path.GetFileName).Distinct())}";
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

    private void TrackBackupState(string sourcePath, List<string> createdBackupFiles, List<string> existingBackupFiles)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        var backupPath = $"{sourcePath}.bak";
        var backupAlreadyExists = File.Exists(backupPath);
        var backup = BackupService.CreateBackupBeforeWrite(sourcePath);

        if (backupAlreadyExists)
        {
            existingBackupFiles.Add(backupPath);
        }
        else if (backup != null)
        {
            createdBackupFiles.Add(backup);
        }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        Log("用户点击恢复备份文件。");
        var filePicker = new FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

        filePicker.ViewMode = PickerViewMode.List;
        filePicker.FileTypeFilter.Add(".bak");

        var file = await filePicker.PickSingleFileAsync();
        Log(file == null ? "恢复文件选择已取消。" : $"已选择恢复文件: name={file.Name}, path={file.Path}");
        if (file == null)
        {
            return;
        }

        if (!IsSupportedBackupFile(file.Path))
        {
            await ShowErrorDialog("恢复失败", "请选择以 .dat.bak 或 .lex.bak 结尾的备份文件。");
            return;
        }

        try
        {
            var targetPath = BackupService.GetRestoreTargetPath(file.Path);
            BackupService.RestoreFromBackup(file.Path, targetPath);
            await ReloadRestoredFileAsync(targetPath);
            UpdateStatus($"已恢复文件: {Path.GetFileName(targetPath)}");

            var dialog = new ContentDialog
            {
                Title = "恢复成功",
                Content = $"已将备份文件覆盖恢复到:\n{targetPath}",
                CloseButtonText = "确定",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorDialog("恢复失败", ex.Message);
        }
    }

    private static bool IsSupportedBackupFile(string path)
    {
        return path.EndsWith(".dat.bak", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".lex.bak", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ReloadRestoredFileAsync(string targetPath)
    {
        var fileName = Path.GetFileName(targetPath);

        if (_viewerPage != null)
        {
            await _viewerPage.LoadFileAsync(targetPath);
        }

        if (_lexPage != null && string.Equals(fileName, "ChsPinyinEUDPv1.lex", StringComparison.OrdinalIgnoreCase))
        {
            await _lexPage.ViewModel.LoadAsync(targetPath);
        }

        if (_ihPage != null && string.Equals(fileName, "ChsPinyinIH.dat", StringComparison.OrdinalIgnoreCase))
        {
            await _ihPage.ViewModel.LoadAsync(targetPath);
        }

        if (_udlPage != null && string.Equals(fileName, "ChsPinyinUDL.dat", StringComparison.OrdinalIgnoreCase))
        {
            await _udlPage.ViewModel.LoadAsync(targetPath);
        }
    }

    private void UpdateStatus(string path)
    {
        CurrentFileText.Text = $"当前路径: {path}";
        var totalEntries = (_lexPage?.ViewModel.AllEntries.Count ?? 0) +
                           (_ihPage?.ViewModel.AllEntries.Count ?? 0) +
                           (_udlPage?.ViewModel.AllEntries.Count ?? 0);
        EntryCountText.Text = $"总条目数: {totalEntries}";
        Log($"状态栏已更新: path={CurrentFileText.Text}, entries={EntryCountText.Text}");
    }

    private static void Log(string message)
    {
        var formatted = $"[MainWindow {DateTime.Now:HH:mm:ss.fff}] {message}";
        Debug.WriteLine(formatted);
        Console.WriteLine(formatted);
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
