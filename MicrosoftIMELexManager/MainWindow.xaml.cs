using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MicrosoftIMELexManager.Pages;
using Windows.Storage.Pickers;

namespace MicrosoftIMELexManager;

public sealed partial class MainWindow : Window
{
    private LexPage? _lexPage;
    private IHPage? _ihPage;
    private UDLPage? _udlPage;

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

        LexFrame.Navigate(_lexPage.GetType());
        IHFrame.Navigate(_ihPage.GetType());
        UDLFrame.Navigate(_udlPage.GetType());

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
            var dialog = new ContentDialog
            {
                Title = "加载失败",
                Content = ex.Message,
                CloseButtonText = "确定",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var imePath = Path.Combine(appDataPath, @"Microsoft\InputMethod\Chs");

        try
        {
            if (_lexPage != null && _lexPage.ViewModel.IsModified)
            {
                var lexFile = Path.Combine(imePath, "ChsPinyinEUDPv1.lex");
                await _lexPage.ViewModel.SaveAsync(lexFile);
            }

            if (_ihPage != null && _ihPage.ViewModel.IsModified)
            {
                var ihFile = Path.Combine(imePath, "ChsPinyinIH.dat");
                await _ihPage.ViewModel.SaveAsync(ihFile);
            }

            if (_udlPage != null && _udlPage.ViewModel.IsModified)
            {
                var udlFile = Path.Combine(imePath, "ChsPinyinUDL.dat");
                await _udlPage.ViewModel.SaveAsync(udlFile);
            }

            var dialog = new ContentDialog
            {
                Title = "保存成功",
                Content = "所有修改已保存",
                CloseButtonText = "确定",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "保存失败",
                Content = ex.Message,
                CloseButtonText = "确定",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    private async void Backup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "备份功能",
            Content = "备份功能即将推出",
            CloseButtonText = "确定",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void UpdateStatus(string path)
    {
        CurrentFileText.Text = $"当前路径: {path}";
        var totalEntries = (_lexPage?.ViewModel.AllEntries.Count ?? 0) +
                           (_ihPage?.ViewModel.AllEntries.Count ?? 0) +
                           (_udlPage?.ViewModel.AllEntries.Count ?? 0);
        EntryCountText.Text = $"总条目数: {totalEntries}";
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        // Cleanup if needed
    }
}
