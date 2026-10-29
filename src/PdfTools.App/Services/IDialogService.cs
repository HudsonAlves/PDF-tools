using System.Windows;
using Microsoft.Win32;

namespace PdfTools.App.Services;

/// <summary>Keeps file pickers and message boxes out of the view models.</summary>
public interface IDialogService
{
    IReadOnlyList<string> PickPdfsToOpen();

    string? PickSavePath(string suggestedName);

    bool Confirm(string message);

    void ShowError(string message);
}

public sealed class WpfDialogService : IDialogService
{
    private const string PdfFilter = "PDF files (*.pdf)|*.pdf";
    private const string Title = "PDF Tools";

    public IReadOnlyList<string> PickPdfsToOpen()
    {
        var dialog = new OpenFileDialog { Title = "Add PDF files", Filter = PdfFilter, Multiselect = true };
        return dialog.ShowDialog(Owner) == true ? dialog.FileNames : [];
    }

    public string? PickSavePath(string suggestedName)
    {
        var dialog = new SaveFileDialog { Title = "Save PDF", Filter = PdfFilter, FileName = suggestedName, OverwritePrompt = true };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public bool Confirm(string message) =>
        MessageBox.Show(Owner, message, Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void ShowError(string message) =>
        MessageBox.Show(Owner, message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);

    private static Window Owner => Application.Current.MainWindow;
}
