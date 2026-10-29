using PdfTools.Core.Models;
using PdfTools.Core.Sorting;

namespace PdfTools.Core;

/// <summary>
/// The ordered list of pages that will become the output PDF.
/// All page-list editing goes through here so it stays UI-independent and testable.
/// </summary>
public sealed class PageCollection
{
    private readonly List<PageItem> _pages = new();
    private readonly List<PdfSourceFile> _sources = new();

    /// <summary>Raised after any change to the page order or contents.</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<PageItem> Pages => _pages;

    public IReadOnlyList<PdfSourceFile> Sources => _sources;

    public int Count => _pages.Count;

    /// <summary>Appends every page of <paramref name="source"/> to the end of the list.</summary>
    public void AddDocument(PdfSourceFile source)
    {
        _sources.Add(source);
        for (var i = 0; i < source.PageCount; i++)
        {
            _pages.Add(new PageItem(source, i));
        }
        OnChanged();
    }

    /// <summary>Moves the given pages so the block starts at <paramref name="targetIndex"/> (index in the list before the move).</summary>
    public void MovePages(IEnumerable<PageItem> pages, int targetIndex)
    {
        var moving = _pages.Where(pages.Contains).ToList(); // keep their current relative order
        if (moving.Count == 0) return;

        // Shift target left for every moved page that sat before it.
        var adjusted = targetIndex - _pages.Take(targetIndex).Count(moving.Contains);
        _pages.RemoveAll(moving.Contains);
        _pages.InsertRange(Math.Clamp(adjusted, 0, _pages.Count), moving);
        OnChanged();
    }

    /// <summary>Moves each selected page one step up (-1) or down (+1); blocks stop at the edges.</summary>
    public void Nudge(IEnumerable<PageItem> pages, int direction)
    {
        var set = pages.ToHashSet();
        if (set.Count == 0 || direction == 0) return;

        var step = Math.Sign(direction);
        var indices = Enumerable.Range(0, _pages.Count).Where(i => set.Contains(_pages[i]));
        indices = step < 0 ? indices.OrderBy(i => i) : indices.OrderByDescending(i => i);

        foreach (var i in indices)
        {
            var j = i + step;
            if (j < 0 || j >= _pages.Count || set.Contains(_pages[j])) continue;
            (_pages[i], _pages[j]) = (_pages[j], _pages[i]);
        }
        OnChanged();
    }

    public void Remove(IEnumerable<PageItem> pages)
    {
        var set = pages.ToHashSet();
        if (_pages.RemoveAll(set.Contains) == 0) return;

        // Forget source files that no longer contribute any page.
        _sources.RemoveAll(s => _pages.All(p => p.Source != s));
        OnChanged();
    }

    public void Clear()
    {
        _pages.Clear();
        _sources.Clear();
        OnChanged();
    }

    /// <summary>Re-orders the whole list using a sorting strategy.</summary>
    public void Sort(IPageSorter sorter)
    {
        var sorted = sorter.Sort(_pages, _sources).ToList();
        if (sorted.Count != _pages.Count || sorted.Except(_pages).Any())
        {
            throw new InvalidOperationException($"Sorter '{sorter.Name}' must return the same pages it was given.");
        }

        _pages.Clear();
        _pages.AddRange(sorted);
        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
