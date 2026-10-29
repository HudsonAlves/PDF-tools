using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PdfTools.Tests;

/// <summary>Creates throwaway PDFs whose pages are identifiable by their width.</summary>
internal static class TestPdfs
{
    /// <summary>Creates a PDF with one page per width (in points).</summary>
    public static string Create(string directory, string name, params int[] pageWidths)
    {
        var path = Path.Combine(directory, name);
        using var doc = new PdfDocument();
        foreach (var width in pageWidths)
        {
            var page = doc.AddPage();
            page.Width = PdfSharp.Drawing.XUnit.FromPoint(width);
            page.Height = PdfSharp.Drawing.XUnit.FromPoint(800);
        }
        doc.Save(path);
        return path;
    }

    public static int[] ReadPageWidths(string path)
    {
        using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        return doc.Pages.Cast<PdfPage>().Select(p => (int)Math.Round(p.Width.Point)).ToArray();
    }
}

public sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("pdftools-tests-").FullName;

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
