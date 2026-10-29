using PdfTools.Core.Models;

namespace PdfTools.Core.Sorting;

/// <summary>Registry of the built-in sorters. Add new ones to <see cref="All"/>.</summary>
public static class PageSorters
{
    public static IReadOnlyList<IPageSorter> All { get; } =
    [
        new OriginalOrderSorter(),
        new ReverseSorter(),
        new FileNameSorter(),
        new InterleaveSorter(),
        new OddThenEvenSorter(),
    ];
}

/// <summary>Files in the order they were added, each file's pages 1..N.</summary>
public sealed class OriginalOrderSorter : IPageSorter
{
    public string Name => "Original order";

    public IEnumerable<PageItem> Sort(IReadOnlyList<PageItem> pages, IReadOnlyList<PdfSourceFile> sources) =>
        pages.OrderBy(p => IndexOf(sources, p.Source)).ThenBy(p => p.PageIndex);

    internal static int IndexOf(IReadOnlyList<PdfSourceFile> sources, PdfSourceFile source)
    {
        for (var i = 0; i < sources.Count; i++)
        {
            if (sources[i] == source) return i;
        }
        return int.MaxValue;
    }
}

/// <summary>Flips the current order.</summary>
public sealed class ReverseSorter : IPageSorter
{
    public string Name => "Reverse current order";

    public IEnumerable<PageItem> Sort(IReadOnlyList<PageItem> pages, IReadOnlyList<PdfSourceFile> sources) =>
        pages.Reverse();
}

/// <summary>Groups pages by file name (A→Z), then page number.</summary>
public sealed class FileNameSorter : IPageSorter
{
    public string Name => "By file name (A→Z)";

    public IEnumerable<PageItem> Sort(IReadOnlyList<PageItem> pages, IReadOnlyList<PdfSourceFile> sources) =>
        pages.OrderBy(p => p.Source.FileName, StringComparer.CurrentCultureIgnoreCase)
             .ThenBy(p => OriginalOrderSorter.IndexOf(sources, p.Source))
             .ThenBy(p => p.PageIndex);
}

/// <summary>
/// Takes page 1 of each file, then page 2 of each file, ...
/// Handy for merging separately scanned front/back sides.
/// </summary>
public sealed class InterleaveSorter : IPageSorter
{
    public string Name => "Interleave files (1st of each, 2nd of each…)";

    public IEnumerable<PageItem> Sort(IReadOnlyList<PageItem> pages, IReadOnlyList<PdfSourceFile> sources)
    {
        // Keep each file's pages in their current relative order, then round-robin across files.
        var queues = pages.GroupBy(p => p.Source)
                          .OrderBy(g => OriginalOrderSorter.IndexOf(sources, g.Key))
                          .Select(g => new Queue<PageItem>(g))
                          .ToList();

        while (queues.Any(q => q.Count > 0))
        {
            foreach (var q in queues.Where(q => q.Count > 0))
            {
                yield return q.Dequeue();
            }
        }
    }
}

/// <summary>All odd positions of the current list, then all even positions.</summary>
public sealed class OddThenEvenSorter : IPageSorter
{
    public string Name => "Odd positions, then even";

    public IEnumerable<PageItem> Sort(IReadOnlyList<PageItem> pages, IReadOnlyList<PdfSourceFile> sources) =>
        pages.Where((_, i) => i % 2 == 0).Concat(pages.Where((_, i) => i % 2 == 1));
}
