using System.Windows;
using PdfTools.App.Services;
using PdfTools.App.ViewModels;
using PdfTools.Core.Services;

namespace PdfTools.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Composition root: swap implementations here (e.g. a different PDF library).
        var viewModel = new MainViewModel(
            new PdfSharpReader(),
            new PdfSharpWriter(),
            new PdfiumPageRenderer(),
            new WpfDialogService());

        MainWindow = new MainWindow(viewModel);
        MainWindow.Show();

        // PDFs passed on the command line (or dropped onto the .exe) are loaded at startup.
        viewModel.AddFiles(e.Args);
    }
}
