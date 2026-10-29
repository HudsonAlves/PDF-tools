using System.Collections.Concurrent;
using PDFtoImage;
using PdfTools.Core.Models;

namespace PdfTools.Core.Services;

/// <summary>Renders previews with PDFium (via the PDFtoImage package).</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
[System.Runtime.Versioning.SupportedOSPlatform("linux")]
[System.Runtime.Versioning.SupportedOSPlatform("macos")]
public sealed class PdfiumPageRenderer : IPageRenderer
{
    // File bytes are cached so rendering many pages of one file doesn't re-read it from disk.
    private readonly ConcurrentDictionary<PdfSourceFile, byte[]> _fileCache = new();

    public byte[] RenderPng(PageItem page, int maxWidth, int maxHeight)
    {
        var bytes = _fileCache.GetOrAdd(page.Source, s => File.ReadAllBytes(s.FilePath));
        var options = new RenderOptions(Width: maxWidth, Height: maxHeight, WithAspectRatio: true);

        using var png = new MemoryStream();
        Conversion.SavePng(png, bytes, page.PageIndex, password: null, options);
        return png.ToArray();
    }
}
