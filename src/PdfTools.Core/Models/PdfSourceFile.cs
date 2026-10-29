namespace PdfTools.Core.Models;

/// <summary>
/// A PDF file that was loaded into the workspace. Pages reference it by object,
/// so the same file can contribute pages in any order (or more than once).
/// </summary>
public sealed class PdfSourceFile
{
    public PdfSourceFile(string filePath, int pageCount)
    {
        FilePath = Path.GetFullPath(filePath);
        PageCount = pageCount;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public string FilePath { get; }

    public string FileName => Path.GetFileName(FilePath);

    public int PageCount { get; }

    public override string ToString() => $"{FileName} ({PageCount} pages)";
}
