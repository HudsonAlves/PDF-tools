using PdfTools.Core;
using PdfTools.Core.Models;
using PdfTools.Core.Sorting;

namespace PdfTools.Tests;

public class PageCollectionTests
{
    private static readonly PdfSourceFile A = new("a.pdf", 3);
    private static readonly PdfSourceFile B = new("b.pdf", 2);

    private static PageCollection Build()
    {
        var c = new PageCollection();
        c.AddDocument(A);
        c.AddDocument(B);
        return c;
    }

    private static string Order(PageCollection c) =>
        string.Join(",", c.Pages.Select(p => $"{p.Source.FileName[0]}{p.PageNumber}"));

    [Fact]
    public void AddDocument_AppendsAllPages()
    {
        Assert.Equal("a1,a2,a3,b1,b2", Order(Build()));
    }

    [Fact]
    public void MovePages_MovesBlockToTarget()
    {
        var c = Build();
        c.MovePages([c.Pages[3], c.Pages[4]], 0);
        Assert.Equal("b1,b2,a1,a2,a3", Order(c));

        c.MovePages([c.Pages[0]], c.Count);
        Assert.Equal("b2,a1,a2,a3,b1", Order(c));
    }

    [Fact]
    public void Nudge_MovesSelectionAndStopsAtEdges()
    {
        var c = Build();
        c.Nudge([c.Pages[0], c.Pages[2]], -1);
        Assert.Equal("a1,a3,a2,b1,b2", Order(c));

        c.Nudge([c.Pages[4]], +1);
        Assert.Equal("a1,a3,a2,b1,b2", Order(c));
    }

    [Fact]
    public void Remove_DropsPagesAndUnusedSources()
    {
        var c = Build();
        c.Remove(c.Pages.Where(p => p.Source == B).ToList());
        Assert.Equal("a1,a2,a3", Order(c));
        Assert.Equal([A], c.Sources);
    }

    [Fact]
    public void Changed_IsRaised()
    {
        var c = new PageCollection();
        var raised = 0;
        c.Changed += (_, _) => raised++;
        c.AddDocument(A);
        c.Sort(new ReverseSorter());
        Assert.Equal(2, raised);
    }

    [Theory]
    [InlineData(typeof(OriginalOrderSorter), "a1,a2,a3,b1,b2")]
    [InlineData(typeof(ReverseSorter), "b2,b1,a3,a2,a1")]
    [InlineData(typeof(InterleaveSorter), "a1,b1,a2,b2,a3")]
    [InlineData(typeof(OddThenEvenSorter), "a1,a3,b2,a2,b1")]
    public void Sorters_ProduceExpectedOrder(Type sorterType, string expected)
    {
        var c = Build();
        c.Sort((IPageSorter)Activator.CreateInstance(sorterType)!);
        Assert.Equal(expected, Order(c));
    }

    [Fact]
    public void FileNameSorter_GroupsByName()
    {
        var c = new PageCollection();
        c.AddDocument(B);
        c.AddDocument(A);
        c.Sort(new FileNameSorter());
        Assert.Equal("a1,a2,a3,b1,b2", Order(c));
    }

    [Fact]
    public void AllRegisteredSorters_KeepEveryPage()
    {
        foreach (var sorter in PageSorters.All)
        {
            var c = Build();
            var before = c.Pages.ToHashSet();
            c.Sort(sorter);
            Assert.True(before.SetEquals(c.Pages), sorter.Name);
        }
    }
}
