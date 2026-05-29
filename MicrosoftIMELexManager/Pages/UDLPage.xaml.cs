using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MicrosoftIMELexManager.ViewModels;

namespace MicrosoftIMELexManager.Pages;

public sealed partial class UDLPage : Page
{
    public UDLViewModel ViewModel => (UDLViewModel)Resources["ViewModel"];

    public UDLPage()
    {
        InitializeComponent();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (UDLListView.SelectedItems.Count > 0)
        {
            ViewModel.DeleteSelectedCommand.Execute(UDLListView.SelectedItems);
        }
    }

    private void EditableField_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.MarkModified();
    }

    private void EditableField_LostFocus(object sender, RoutedEventArgs e)
    {
        ViewModel.RefreshAfterEdit();
    }
}
