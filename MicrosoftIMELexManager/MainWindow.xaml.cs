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
    private const string LexLibraryKey = "Lex";
    private const string IHLibraryKey = "IH";
    private const string UDLLibraryKey = "UDL";

    private LexPage? _lexPage;
    private IHPage? _ihPage;
    private UDLPage? _udlPage;
    private ViewerPage? _viewerPage;
    private string? _currentFolderPath;
    private readonly List<LibraryItem> _libraryItems = new();

    private FrameworkElement RootElement => (FrameworkElement)Content;
    private ListView LibraryListBoxControl => (ListView)RootElement.FindName("LibraryListBox");
    private Border EmptyContentStateControl => (Border)RootElement.FindName("EmptyContentState");

    private sealed class LibraryItem
    {
        public required string Key { get; init; }
        public required string DisplayName { get; init; }
        public required string FilePath { get; init; }
        public int EntryCount { get; init; }
    }

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

        ShowEmptyState();

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
                await LoadFolderAsync(imePath);
            }
        }
        catch (Exception ex)
        {
            Log($"自动加载失败: {ex}");
        }
    }

    private async void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Log("用户点击打开文件夹。");
        var folderPicker = new FolderPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

        folderPicker.FileTypeFilter.Add("*");

        var folder = await folderPicker.PickSingleFolderAsync();
        Log(folder == null ? "文件夹选择已取消。" : $"已选择文件夹: name={folder.Name}, path={folder.Path}");
        if (folder != null)
        {
            await LoadFolderAsync(folder.Path);
        }
    }

    private async Task LoadFolderAsync(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                await ShowErrorDialog("加载失败", "所选文件夹不存在。");
                return;
            }

            _currentFolderPath = path;

            var lexFile = Path.Combine(path, "ChsPinyinEUDPv1.lex");
            var ihFile = Path.Combine(path, "ChsPinyinIH.dat");
            var udlFile = Path.Combine(path, "ChsPinyinUDL.dat");
            var items = new List<LibraryItem>();

            if (File.Exists(lexFile) && _lexPage != null)
            {
                await _lexPage.ViewModel.LoadAsync(lexFile);
                items.Add(new LibraryItem { Key = LexLibraryKey, DisplayName = "自定义短语", FilePath = lexFile, EntryCount = _lexPage.ViewModel.AllEntries.Count });
            }
            if (File.Exists(ihFile) && _ihPage != null)
            {
                await _ihPage.ViewModel.LoadAsync(ihFile);
                items.Add(new LibraryItem { Key = IHLibraryKey, DisplayName = "输入历史", FilePath = ihFile, EntryCount = _ihPage.ViewModel.AllEntries.Count });
            }
            if (File.Exists(udlFile) && _udlPage != null)
            {
                await _udlPage.ViewModel.LoadAsync(udlFile);
                items.Add(new LibraryItem { Key = UDLLibraryKey, DisplayName = "自学习词汇", FilePath = udlFile, EntryCount = _udlPage.ViewModel.AllEntries.Count });
            }

            _libraryItems.Clear();
            _libraryItems.AddRange(items);
            LibraryListBoxControl.ItemsSource = null;
            LibraryListBoxControl.ItemsSource = _libraryItems;

            UpdateStatus(path);

            if (_libraryItems.Count > 0)
            {
                LibraryListBoxControl.SelectedIndex = 0;
            }
            else
            {
                LibraryListBoxControl.SelectedItem = null;
                ShowEmptyState();
                await ShowErrorDialog("未找到词库", "该文件夹下未找到可加载的词库文件。\n\n目前支持：ChsPinyinEUDPv1.lex、ChsPinyinIH.dat、ChsPinyinUDL.dat");
            }
        }
        catch (Exception ex)
        {
            Log($"加载文件夹失败: {ex}");
            await ShowErrorDialog("加载失败", ex.Message);
        }
    }

    private void LibraryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LibraryListBoxControl.SelectedItem is LibraryItem item)
        {
            ShowLibrary(item.Key);
        }
    }

    private void ShowLibrary(string key)
    {
        EmptyContentStateControl.Visibility = Visibility.Collapsed;
        LexFrame.Visibility = key == LexLibraryKey ? Visibility.Visible : Visibility.Collapsed;
        IHFrame.Visibility = key == IHLibraryKey ? Visibility.Visible : Visibility.Collapsed;
        UDLFrame.Visibility = key == UDLLibraryKey ? Visibility.Visible : Visibility.Collapsed;
        ViewerFrame.Visibility = Visibility.Collapsed;
    }

    private void ShowEmptyState()
    {
        EmptyContentStateControl.Visibility = Visibility.Visible;
        LexFrame.Visibility = Visibility.Collapsed;
        IHFrame.Visibility = Visibility.Collapsed;
        UDLFrame.Visibility = Visibility.Collapsed;
        ViewerFrame.Visibility = Visibility.Collapsed;
    }

    private bool HasLibrary(string key)
    {
        return _libraryItems.Any(item => item.Key == key);
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

        if (string.IsNullOrWhiteSpace(_currentFolderPath) || !Directory.Exists(_currentFolderPath))
        {
            await ShowErrorDialog("保存失败", "请先打开一个包含词库文件的文件夹。");
            return;
        }

        try
        {
            var createdBackupFiles = new List<string>();
            var existingBackupFiles = new List<string>();
            var savedLibraries = new List<string>();

            if (_lexPage != null && _lexPage.ViewModel.IsModified && HasLibrary(LexLibraryKey))
            {
                var lexFile = Path.Combine(_currentFolderPath, "ChsPinyinEUDPv1.lex");
                TrackBackupState(lexFile, createdBackupFiles, existingBackupFiles);
                await _lexPage.ViewModel.SaveAsync(lexFile);
                savedLibraries.Add("自定义短语");
            }

            if (_ihPage != null && _ihPage.ViewModel.IsModified && HasLibrary(IHLibraryKey))
            {
                var ihFile = Path.Combine(_currentFolderPath, "ChsPinyinIH.dat");
                TrackBackupState(ihFile, createdBackupFiles, existingBackupFiles);
                await _ihPage.ViewModel.SaveAsync(ihFile);
                savedLibraries.Add("输入历史");
            }

            if (_udlPage != null && _udlPage.ViewModel.IsModified && HasLibrary(UDLLibraryKey))
            {
                var udlFile = Path.Combine(_currentFolderPath, "ChsPinyinUDL.dat");
                TrackBackupState(udlFile, createdBackupFiles, existingBackupFiles);
                await _udlPage.ViewModel.SaveAsync(udlFile);
                savedLibraries.Add("自学习词汇");
            }

            var message = "所有修改已保存";
            if (savedLibraries.Count > 0)
            {
                message += $"\n\n本次已写入: {string.Join("、", savedLibraries)}";
            }
            if (createdBackupFiles.Count > 0)
            {
                message += $"\n\n已创建备份文件:\n{string.Join("\n", createdBackupFiles.Select(Path.GetFileName))}";
            }

            if (existingBackupFiles.Count > 0)
            {
                message += $"\n\n以下备份已存在，未重复创建:\n{string.Join("\n", existingBackupFiles.Select(Path.GetFileName).Distinct())}";
            }

            message += "\n\n若微软拼音仍显示旧词库，通常是因为 TextInputHost/ctfmon 仍在缓存旧数据。可立即刷新输入法，或手动切换输入法/注销后再试。";

            var dialog = new ContentDialog
            {
                Title = "保存成功",
                Content = message,
                PrimaryButtonText = "刷新输入法",
                CloseButtonText = "稍后",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };
            var dialogResult = await dialog.ShowAsync();

            if (dialogResult == ContentDialogResult.Primary)
            {
                var refreshResult = BackupService.RefreshIME();
                var refreshDialog = new ContentDialog
                {
                    Title = refreshResult.Success ? "输入法已刷新" : "输入法刷新失败",
                    Content = refreshResult.Message,
                    CloseButtonText = "确定",
                    XamlRoot = Content.XamlRoot
                };
                await refreshDialog.ShowAsync();
            }
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
            if (!string.IsNullOrWhiteSpace(_currentFolderPath) && string.Equals(Path.GetDirectoryName(targetPath), _currentFolderPath, StringComparison.OrdinalIgnoreCase))
            {
                await LoadFolderAsync(_currentFolderPath);
            }
            else
            {
                await ReloadRestoredFileAsync(targetPath);
                UpdateStatus($"已恢复文件: {Path.GetFileName(targetPath)}");
            }

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

        int lexCount = HasLibrary(LexLibraryKey) ? _lexPage?.ViewModel.AllEntries.Count ?? 0 : 0;
        int ihCount  = HasLibrary(IHLibraryKey)  ? _ihPage?.ViewModel.AllEntries.Count  ?? 0 : 0;
        int udlCount = HasLibrary(UDLLibraryKey) ? _udlPage?.ViewModel.AllEntries.Count ?? 0 : 0;
        int totalEntries = lexCount + ihCount + udlCount;

        EntryCountText.Text = $"总条目数: {totalEntries}";

        var details = new System.Text.StringBuilder();
        if (HasLibrary(LexLibraryKey)) details.Append($"自定义短语 {lexCount} 条");
        if (HasLibrary(IHLibraryKey))  { if (details.Length > 0) details.Append(" · "); details.Append($"输入历史 {ihCount} 条"); }
        if (HasLibrary(UDLLibraryKey)) { if (details.Length > 0) details.Append(" · "); details.Append($"自学习词汇 {udlCount} 条"); }
        EntryCountDetailText.Text = details.Length > 0 ? $"（{details}）" : string.Empty;

        Log($"状态栏已更新: path={CurrentFileText.Text}, entries={EntryCountText.Text}, detail={EntryCountDetailText.Text}");
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
