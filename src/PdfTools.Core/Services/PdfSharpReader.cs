using PdfSharp.Pdf.IO;
using PdfTools.Core.Models;

namespace PdfTools.Core.Services;

public sealed class PdfSharpReader : IPdfReader
{
    public PdfSourceFile Open(string filePath)
    {
        using var document = PdfReader.Open(filePath, PdfDocumentOpenMode.Import);
        return new PdfSourceFile(filePath, document.PageCount);
    }
}
