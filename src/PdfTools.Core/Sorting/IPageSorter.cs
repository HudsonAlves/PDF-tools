using PdfTools.Core.Models;

namespace PdfTools.Core.Sorting;

/// <summary>
/// A strategy that re-orders the page list. To add a new sort:
/// 1. create a class implementing this interface, and
/// 2. add it to <see cref="PageSorters.All"/> — the UI menu picks it up automatically.
/// </summary>
public interface IPageSorter
{
    /// <summary>Text shown in the UI.</summary>
    string Name { get; }

    /// <summary>
    /// Returns the same pages in a new order. Must not add or drop pages.
    /// </summary>
    /// <param name="pages">Current page order.</param>
    /// <param name="sources">Loaded files, in the order they were added.</param>
    IEnumerable<PageItem> Sort(IReadOnlyList<PageItem> pages, IReadOnlyList<PdfSourceFile> sources);
}
