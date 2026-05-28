using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MicrosoftIMELexManager.Models;
using MicrosoftIMELexManager.ViewModels;

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
        // TODO: 实现导出功能
        var dialog = new ContentDialog
        {
            Title = "导出词条",
            Content = "导出功能即将推出",
            CloseButtonText = "确定",
            XamlRoot = XamlRoot
        };
        await dialog.ShowAsync();
    }
}
