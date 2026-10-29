using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfTools.Core.Models;

namespace PdfTools.Core.Services;

public sealed class PdfSharpWriter : IPdfWriter
{
    public void Save(IReadOnlyList<PageItem> pages, string outputPath)
    {
        if (pages.Count == 0)
        {
            throw new InvalidOperationException("There are no pages to save.");
        }

        // Each source is opened once, no matter how many of its pages are used.
        var inputs = new Dictionary<PdfSourceFile, PdfDocument>();
        try
        {
            using var output = new PdfDocument();
            foreach (var page in pages)
            {
                if (!inputs.TryGetValue(page.Source, out var input))
                {
                    input = PdfReader.Open(page.Source.FilePath, PdfDocumentOpenMode.Import);
                    inputs[page.Source] = input;
                }

                output.AddPage(input.Pages[page.PageIndex]);
            }

            // Write to a temp file first so we never destroy an input that is also the output.
            var tempPath = outputPath + ".tmp";
            output.Save(tempPath);
            foreach (var input in inputs.Values) input.Dispose();
            inputs.Clear();
            File.Move(tempPath, outputPath, overwrite: true);
        }
        finally
        {
            foreach (var input in inputs.Values) input.Dispose();
        }
    }
}
