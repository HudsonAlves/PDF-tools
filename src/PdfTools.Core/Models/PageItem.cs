namespace PdfTools.Core.Models;

/// <summary>
/// One page in the output document: "page N of source file X".
/// This is the unit that gets merged, sorted, removed, etc.
/// Add new per-page settings (rotation, crop, ...) here and honour them in the writer.
/// </summary>
public sealed class PageItem
{
    public PageItem(PdfSourceFile source, int pageIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, source.PageCount);

        Source = source;
        PageIndex = pageIndex;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public PdfSourceFile Source { get; }

    /// <summary>Zero-based page index inside <see cref="Source"/>.</summary>
    public int PageIndex { get; }

    /// <summary>One-based page number, for display.</summary>
    public int PageNumber => PageIndex + 1;

    public override string ToString() => $"{Source.FileName} – p.{PageNumber}";
}
