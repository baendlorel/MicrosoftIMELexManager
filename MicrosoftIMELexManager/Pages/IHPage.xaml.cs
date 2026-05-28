using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MicrosoftIMELexManager.Models;
using MicrosoftIMELexManager.ViewModels;

namespace MicrosoftIMELexManager.Pages;

public sealed partial class IHPage : Page
{
    public IHViewModel ViewModel => (IHViewModel)Resources["ViewModel"];

    public IHPage()
    {
        InitializeComponent();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (IHListView.SelectedItems.Count > 0)
        {
            ViewModel.DeleteSelectedCommand.Execute(IHListView.SelectedItems);
        }
    }

    private void ResetFreq_Click(object sender, RoutedEventArgs e)
    {
        if (IHListView.SelectedItems.Count > 0)
        {
            foreach (var item in IHListView.SelectedItems.OfType<IHEntry>())
            {
                ViewModel.ResetFrequencyCommand.Execute(item);
            }
        }
    }

    private async void ClearAllFreq_Click(object sender, RoutedEventArgs e)
    {
        var confirmDialog = new ContentDialog
        {
            Title = "确认清零词频",
            Content = "确定要将所有词语的词频清零吗？此操作不可撤销。",
            PrimaryButtonText = "确定清零",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        var result = await confirmDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            foreach (var entry in ViewModel.AllEntries)
            {
                entry.Frequency = 1;
            }
            ViewModel.IsModified = true;
        }
    }
}
