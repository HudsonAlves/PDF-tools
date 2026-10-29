using System.Windows.Media;
using PdfTools.App.Mvvm;
using PdfTools.Core.Models;

namespace PdfTools.App.ViewModels;

public enum DropPosition { None, Before, After }

/// <summary>One thumbnail card in the page grid.</summary>
public sealed class PageViewModel : ObservableObject
{
    private ImageSource? _thumbnail;
    private bool _thumbnailFailed;
    private bool _isSelected;
    private int _position;
    private DropPosition _dropPosition;

    public PageViewModel(PageItem page) => Page = page;

    public PageItem Page { get; }

    public string FileName => Page.Source.FileName;

    public string FilePath => Page.Source.FilePath;

    public string PageLabel => $"Page {Page.PageNumber} of {Page.Source.PageCount}";

    /// <summary>One-based position in the output document.</summary>
    public int Position { get => _position; set => SetProperty(ref _position, value); }

    public ImageSource? Thumbnail { get => _thumbnail; set => SetProperty(ref _thumbnail, value); }

    public bool ThumbnailFailed { get => _thumbnailFailed; set => SetProperty(ref _thumbnailFailed, value); }

    /// <summary>Pixel width the current <see cref="Thumbnail"/> was rendered at (0 = not rendered yet).</summary>
    internal int RenderedWidth { get; set; }

    /// <summary>True while the card is on screen; visible cards are rendered first.</summary>
    internal bool IsOnScreen { get; set; }

    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    /// <summary>Where the drop marker is drawn while dragging over this card.</summary>
    public DropPosition DropPosition { get => _dropPosition; set => SetProperty(ref _dropPosition, value); }
}
