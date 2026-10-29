using PdfTools.Core.Services;

namespace PdfTools.App;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Composition root: swap implementations here (e.g. a different PDF library).
        IPdfReader reader = new PdfSharpReader();
        IPdfWriter writer = new PdfSharpWriter();
        IPageRenderer renderer = new PdfiumPageRenderer();

        Application.Run(new MainForm(reader, writer, renderer));
    }
}
