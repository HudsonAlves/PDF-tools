using PdfTools.Core.Models;
using PdfTools.Core.Services;

namespace PdfTools.App.Controls;

/// <summary>
/// Thumbnail grid of pages with drag-and-drop reordering.
/// It only displays pages and reports user gestures; the page order itself lives in
/// <see cref="Core.PageCollection"/> and is changed by the form in response to the events below.
/// </summary>
public sealed class PageListView : ListView
{
    private const string LoadingKey = "__loading";
    private const string ErrorKey = "__error";

    private readonly ImageList _thumbnails = new();
    private readonly HashSet<Guid> _requested = new();
    private IPageRenderer? _renderer;

    public PageListView()
    {
        View = View.LargeIcon;
        MultiSelect = true;
        HideSelection = false;
        AllowDrop = true;
        DoubleBuffered = true;

        _thumbnails.ColorDepth = ColorDepth.Depth32Bit;
        _thumbnails.ImageSize = new Size(120, 160);
        _thumbnails.Images.Add(LoadingKey, MakePlaceholder("Loading…"));
        _thumbnails.Images.Add(ErrorKey, MakePlaceholder("Preview\nunavailable"));
        LargeImageList = _thumbnails;
    }

    /// <summary>User dragged pages within the list. Args: pages, target index.</summary>
    public event Action<IReadOnlyList<PageItem>, int>? PagesDropped;

    /// <summary>User dropped files from Explorer onto the list.</summary>
    public event Action<string[]>? FilesDropped;

    public IReadOnlyList<PageItem> SelectedPages =>
        SelectedItems.Cast<ListViewItem>().Select(i => (PageItem)i.Tag!).ToList();

    public void SetRenderer(IPageRenderer renderer) => _renderer = renderer;

    /// <summary>Redraws the list for the given order, keeping the given pages selected.</summary>
    public void ShowPages(IReadOnlyList<PageItem> pages, IEnumerable<PageItem>? selected = null)
    {
        var keep = (selected ?? SelectedPages).ToHashSet();

        BeginUpdate();
        Items.Clear();
        for (var i = 0; i < pages.Count; i++)
        {
            var page = pages[i];
            var key = page.Id.ToString();
            var item = new ListViewItem($"{i + 1}. {page.Source.FileName}\npage {page.PageNumber}")
            {
                Tag = page,
                ImageKey = _thumbnails.Images.ContainsKey(key) ? key : LoadingKey,
                Selected = keep.Contains(page),
                ToolTipText = page.Source.FilePath,
            };
            Items.Add(item);
        }
        EndUpdate();

        _ = LoadMissingThumbnailsAsync(pages);
    }

    private async Task LoadMissingThumbnailsAsync(IReadOnlyList<PageItem> pages)
    {
        if (_renderer is null) return;

        foreach (var page in pages.Where(p => _requested.Add(p.Id)))
        {
            var key = page.Id.ToString();
            var size = _thumbnails.ImageSize;
            try
            {
                var png = await Task.Run(() => _renderer.RenderPng(page, size.Width, size.Height));
                using var ms = new MemoryStream(png);
                _thumbnails.Images.Add(key, FitInto(Image.FromStream(ms), size));
            }
            catch
            {
                key = ErrorKey;
            }

            if (IsDisposed) return;
            foreach (ListViewItem item in Items)
            {
                if (item.Tag == page) item.ImageKey = key;
            }
        }
    }

    // ---- Drag & drop -------------------------------------------------------

    protected override void OnItemDrag(ItemDragEventArgs e)
    {
        base.OnItemDrag(e);
        DoDragDrop(SelectedPages, DragDropEffects.Move);
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);

        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
            return;
        }

        if (e.Data?.GetData(typeof(List<PageItem>)) is null)
        {
            e.Effect = DragDropEffects.None;
            return;
        }

        e.Effect = DragDropEffects.Move;
        var (index, after) = HitInsertionPoint(e.X, e.Y);
        InsertionMark.Index = index;
        InsertionMark.AppearsAfterItem = after;
    }

    protected override void OnDragLeave(EventArgs e)
    {
        base.OnDragLeave(e);
        InsertionMark.Index = -1;
    }

    protected override void OnDragDrop(DragEventArgs e)
    {
        base.OnDragDrop(e);
        InsertionMark.Index = -1;

        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files)
        {
            FilesDropped?.Invoke(files);
            return;
        }

        if (e.Data?.GetData(typeof(List<PageItem>)) is List<PageItem> pages)
        {
            var (index, after) = HitInsertionPoint(e.X, e.Y);
            var target = index < 0 ? Items.Count : index + (after ? 1 : 0);
            PagesDropped?.Invoke(pages, target);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Control && e.KeyCode == Keys.A)
        {
            BeginUpdate();
            foreach (ListViewItem item in Items) item.Selected = true;
            EndUpdate();
            e.Handled = true;
        }
    }

    /// <summary>Finds the item nearest to a screen point and whether the drop goes after it.</summary>
    private (int Index, bool After) HitInsertionPoint(int screenX, int screenY)
    {
        var pt = PointToClient(new Point(screenX, screenY));
        var index = InsertionMark.NearestIndex(pt);
        if (index < 0) return (-1, false);

        var bounds = GetItemRect(index);
        return (index, pt.X > bounds.Left + bounds.Width / 2);
    }

    // ---- Image helpers -----------------------------------------------------

    /// <summary>Centres an image on a white canvas of exactly <paramref name="size"/>.</summary>
    private static Bitmap FitInto(Image source, Size size)
    {
        using (source)
        {
            var scale = Math.Min((float)size.Width / source.Width, (float)size.Height / source.Height);
            var w = (int)(source.Width * scale);
            var h = (int)(source.Height * scale);

            var bmp = new Bitmap(size.Width, size.Height);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);
            var rect = new Rectangle((size.Width - w) / 2, (size.Height - h) / 2, w, h);
            g.FillRectangle(Brushes.White, rect);
            g.DrawImage(source, rect);
            g.DrawRectangle(Pens.Gray, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            return bmp;
        }
    }

    private Bitmap MakePlaceholder(string text)
    {
        var bmp = new Bitmap(_thumbnails.ImageSize.Width, _thumbnails.ImageSize.Height);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.WhiteSmoke);
        g.DrawRectangle(Pens.LightGray, 0, 0, bmp.Width - 1, bmp.Height - 1);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, SystemFonts.DefaultFont, Brushes.Gray, new RectangleF(0, 0, bmp.Width, bmp.Height), format);
        return bmp;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _thumbnails.Dispose();
        base.Dispose(disposing);
    }
}
