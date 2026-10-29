using PdfTools.Core;
using PdfTools.Core.Services;
using PdfTools.Core.Sorting;

namespace PdfTools.Tests;

public class PdfServicesTests
{
    [Fact]
    public void MergeSortAndSave_WritesPagesInListOrder()
    {
        using var tmp = new TempDir();
        var reader = new PdfSharpReader();
        var pages = new PageCollection();
        pages.AddDocument(reader.Open(TestPdfs.Create(tmp.Path, "a.pdf", 301, 302, 303)));
        pages.AddDocument(reader.Open(TestPdfs.Create(tmp.Path, "b.pdf", 401, 402)));

        pages.Sort(new InterleaveSorter());
        pages.Remove([pages.Pages[^1]]);

        var output = Path.Combine(tmp.Path, "out.pdf");
        new PdfSharpWriter().Save(pages.Pages, output);

        Assert.Equal([301, 401, 302, 402], TestPdfs.ReadPageWidths(output));
    }

    [Fact]
    public void Save_CanOverwriteOneOfItsInputs()
    {
        using var tmp = new TempDir();
        var path = TestPdfs.Create(tmp.Path, "a.pdf", 301, 302);
        var pages = new PageCollection();
        pages.AddDocument(new PdfSharpReader().Open(path));
        pages.Sort(new ReverseSorter());

        new PdfSharpWriter().Save(pages.Pages, path);

        Assert.Equal([302, 301], TestPdfs.ReadPageWidths(path));
    }

    [Fact]
    public void Renderer_ProducesPng()
    {
        using var tmp = new TempDir();
        var source = new PdfSharpReader().Open(TestPdfs.Create(tmp.Path, "a.pdf", 600));
        var pages = new PageCollection();
        pages.AddDocument(source);

        var png = new PdfiumPageRenderer().RenderPng(pages.Pages[0], 120, 160);

        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], png[..4]);
    }
}
