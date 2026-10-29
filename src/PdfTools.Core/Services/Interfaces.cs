using PdfTools.Core.Models;

namespace PdfTools.Core.Services;

/// <summary>Opens a PDF and reads the info the workspace needs.</summary>
public interface IPdfReader
{
    PdfSourceFile Open(string filePath);
}

/// <summary>Writes the given pages, in order, to a new PDF file.</summary>
public interface IPdfWriter
{
    void Save(IReadOnlyList<PageItem> pages, string outputPath);
}

/// <summary>Renders a page preview as PNG bytes (keeps Core free of any UI framework).</summary>
public interface IPageRenderer
{
    byte[] RenderPng(PageItem page, int maxWidth, int maxHeight);
}
