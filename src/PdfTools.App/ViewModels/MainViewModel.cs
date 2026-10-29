using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PdfTools.App.Mvvm;
using PdfTools.App.Services;
using PdfTools.Core;
using PdfTools.Core.Models;
using PdfTools.Core.Services;
using PdfTools.Core.Sorting;

namespace PdfTools.App.ViewModels;

/// <summary>
/// State and commands for the main window. The page order lives in <see cref="PageCollection"/>;
/// this class mirrors it into <see cref="Pages"/> for binding.
/// To add a feature: add a command here and a button bound to it in MainWindow.xaml.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    // Card width limits (device-independent units) for the zoom slider/buttons.
    public const double MinThumbnailWidth = 100;
    public const double MaxThumbnailWidth = 1000;
    public const double DefaultThumbnailWidth = 150;
    private const double ZoomStep = 1.25;

    // Page previews use a 3:4 box (portrait A4/Letter fit nicely; landscape pages letterbox).
    private const double PreviewAspect = 4.0 / 3.0;

    // Render resolution follows the zoom in steps of this many pixels, so small zoom
    // changes don't trigger re-rendering. Capped to keep memory reasonable.
    private const int RenderStep = 200;
    private const int MinRenderWidth = 300;
    private const int MaxRenderWidth = 2400;

    private readonly PageCollection _collection = new();
    private readonly IPdfReader _reader;
    private readonly IPdfWriter _writer;
    private readonly IPageRenderer _renderer;
    private readonly IDialogService _dialogs;

    private string _statusText = "";
    private bool _isBusy;
    private double _thumbnailWidth = DefaultThumbnailWidth;
    private double _renderScale = 1;
    private double _availableWidth = double.MaxValue;
    private bool _loadingThumbnails;
    private readonly DispatcherTimer _rerenderDelay = new() { Interval = TimeSpan.FromMilliseconds(250) };

    public MainViewModel(IPdfReader reader, IPdfWriter writer, IPageRenderer renderer, IDialogService dialogs)
    {
        _reader = reader;
        _writer = writer;
        _renderer = renderer;
        _dialogs = dialogs;

        AddFilesCommand = new RelayCommand(() => AddFiles(_dialogs.PickPdfsToOpen()), () => !IsBusy);
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => HasPages && !IsBusy);
        MoveUpCommand = new RelayCommand(() => _collection.Nudge(SelectedPages, -1), () => HasSelection);
        MoveDownCommand = new RelayCommand(() => _collection.Nudge(SelectedPages, +1), () => HasSelection);
        RemoveCommand = new RelayCommand(() => _collection.Remove(SelectedPages), () => HasSelection);
        ClearCommand = new RelayCommand(ClearAll, () => HasPages && !IsBusy);
        SortCommand = new RelayCommand(p => Run(() => _collection.Sort((IPageSorter)p!)), _ => HasPages);
        RemoveSourceCommand = new RelayCommand(p => RemoveSource((PdfSourceFile)p!));
        SelectAllCommand = new RelayCommand(() => SetSelection(_ => true), () => HasPages);
        ZoomInCommand = new RelayCommand(ZoomIn, () => ThumbnailWidth < MaxZoomWidth);
        ZoomOutCommand = new RelayCommand(ZoomOut, () => ThumbnailWidth > MinThumbnailWidth);
        ResetZoomCommand = new RelayCommand(() => ThumbnailWidth = DefaultThumbnailWidth);

        _rerenderDelay.Tick += (_, _) =>
        {
            _rerenderDelay.Stop();
            _ = LoadThumbnailsAsync();
        };

        _collection.Changed += (_, _) => SyncFromCollection();
        UpdateStatus();
    }

    public ObservableCollection<PageViewModel> Pages { get; } = new();

    public ObservableCollection<PdfSourceFile> Sources { get; } = new();

    public IReadOnlyList<IPageSorter> Sorters => PageSorters.All;

    public ICommand AddFilesCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand SortCommand { get; }
    public ICommand RemoveSourceCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ResetZoomCommand { get; }

    public bool HasPages => Pages.Count > 0;

    public bool HasSelection => Pages.Any(p => p.IsSelected);

    public IReadOnlyList<PageItem> SelectedPages => Pages.Where(p => p.IsSelected).Select(p => p.Page).ToList();

    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

    /// <summary>Card width in the grid; bound to the zoom slider.</summary>
    public double ThumbnailWidth
    {
        get => _thumbnailWidth;
        set
        {
            if (!SetProperty(ref _thumbnailWidth, Math.Clamp(value, MinThumbnailWidth, MaxThumbnailWidth))) return;
            OnCardSizeChanged();
        }
    }

    /// <summary>Width the page grid has for one card; set by the view when it resizes.</summary>
    public double AvailableWidth
    {
        get => _availableWidth;
        set
        {
            if (SetProperty(ref _availableWidth, Math.Max(MinThumbnailWidth, value))) OnCardSizeChanged();
        }
    }

    /// <summary>Actual card size: the zoom level, but never wider than the grid.</summary>
    public double CardWidth => Math.Min(ThumbnailWidth, AvailableWidth);

    public double CardHeight => CardWidth * PreviewAspect;

    private void OnCardSizeChanged()
    {
        OnPropertyChanged(nameof(CardWidth));
        OnPropertyChanged(nameof(CardHeight));
        OnPropertyChanged(nameof(ZoomPercent));
        OnPropertyChanged(nameof(MaxZoomWidth));
        ScheduleRerender();
    }

    /// <summary>Zoom level shown in the status bar (100% = default card size).</summary>
    public int ZoomPercent => (int)Math.Round(CardWidth / DefaultThumbnailWidth * 100);

    /// <summary>Largest useful zoom: one page filling the grid width (bound to the slider maximum).</summary>
    public double MaxZoomWidth => Math.Max(MinThumbnailWidth, Math.Min(MaxThumbnailWidth, AvailableWidth));

    /// <summary>Screen DPI scale (1.0 = 96 DPI), set by the view so previews match physical pixels.</summary>
    public double RenderScale
    {
        get => _renderScale;
        set
        {
            if (SetProperty(ref _renderScale, value)) ScheduleRerender();
        }
    }

    // ---- Actions called by the view (drag & drop) or by commands ------------

    public void AddFiles(IEnumerable<string> paths)
    {
        var failed = new List<string>();
        foreach (var path in paths.Where(p => p.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                _collection.AddDocument(_reader.Open(path));
            }
            catch (Exception ex)
            {
                failed.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }

        if (failed.Count > 0)
        {
            _dialogs.ShowError("Some files could not be opened:\n\n" + string.Join("\n", failed));
        }
    }

    public void MovePages(IReadOnlyList<PageItem> pages, int targetIndex) => _collection.MovePages(pages, targetIndex);

    /// <summary>Called by the view on scroll/resize so on-screen cards get sharp previews first.</summary>
    public void SetOnScreenPages(IReadOnlySet<PageViewModel> onScreen)
    {
        foreach (var page in Pages) page.IsOnScreen = onScreen.Contains(page);
    }

    /// <summary>Call when selection changes in the view so status and commands refresh.</summary>
    public void OnSelectionChanged()
    {
        UpdateStatus();
        CommandManager.InvalidateRequerySuggested();
    }

    private async Task SaveAsync()
    {
        var path = _dialogs.PickSavePath("merged.pdf");
        if (path is null) return;

        var pages = _collection.Pages.ToList(); // snapshot
        IsBusy = true;
        StatusText = "Saving…";
        try
        {
            await Task.Run(() => _writer.Save(pages, path));
            StatusText = $"Saved {pages.Count} pages to {path}";
        }
        catch (Exception ex)
        {
            UpdateStatus();
            _dialogs.ShowError("Could not save the PDF:\n\n" + ex.Message);
        }
        finally
        {
            IsBusy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void ClearAll()
    {
        if (_dialogs.Confirm("Remove all pages from the list?")) _collection.Clear();
    }

    private void RemoveSource(PdfSourceFile source) =>
        _collection.Remove(_collection.Pages.Where(p => p.Source == source).ToList());

    private void SetSelection(Func<PageViewModel, bool> predicate)
    {
        foreach (var page in Pages) page.IsSelected = predicate(page);
        OnSelectionChanged();
    }

    private void ZoomIn() => ThumbnailWidth = Math.Min(MaxZoomWidth, ThumbnailWidth * ZoomStep);

    // Start from the visible size, so zooming out after a window shrink responds immediately.
    private void ZoomOut() => ThumbnailWidth = Math.Max(MinThumbnailWidth, CardWidth / ZoomStep);

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception ex) { _dialogs.ShowError(ex.Message); }
    }

    // ---- Keeping the bound collections in sync -------------------------------

    /// <summary>
    /// Reorders <see cref="Pages"/> in place to match the collection, reusing existing view models
    /// so thumbnails and selection survive moves and sorts.
    /// </summary>
    private void SyncFromCollection()
    {
        var existing = Pages.ToDictionary(p => p.Page);
        var desired = _collection.Pages.Select(p => existing.GetValueOrDefault(p) ?? new PageViewModel(p)).ToList();
        var keep = desired.ToHashSet();

        for (var i = Pages.Count - 1; i >= 0; i--)
        {
            if (!keep.Contains(Pages[i])) Pages.RemoveAt(i);
        }

        for (var i = 0; i < desired.Count; i++)
        {
            var vm = desired[i];
            var current = Pages.IndexOf(vm);
            if (current < 0) Pages.Insert(i, vm);
            else if (current != i) Pages.Move(current, i);
            vm.Position = i + 1;
        }

        Sources.Clear();
        foreach (var source in _collection.Sources) Sources.Add(source);

        OnPropertyChanged(nameof(HasPages));
        OnSelectionChanged();
        _ = LoadThumbnailsAsync();
    }

    /// <summary>Pixel width previews should have at the current zoom and DPI.</summary>
    private int TargetRenderWidth
    {
        get
        {
            var pixels = CardWidth * RenderScale;
            var stepped = (int)Math.Ceiling(pixels / RenderStep) * RenderStep;
            return Math.Clamp(stepped, MinRenderWidth, MaxRenderWidth);
        }
    }

    /// <summary>
    /// A preview needs (re)rendering when it is missing, too blurry for the current zoom,
    /// or much larger than needed (re-rendered smaller to free memory after zooming out).
    /// </summary>
    private bool NeedsRender(PageViewModel vm, int target) =>
        !vm.ThumbnailFailed && (vm.RenderedWidth < target || vm.RenderedWidth > target * 2);

    private void ScheduleRerender()
    {
        // Wait until the zoom stops changing (slider drag) before re-rendering.
        _rerenderDelay.Stop();
        _rerenderDelay.Start();
    }

    /// <summary>
    /// Renders previews one at a time in the background, on-screen cards first.
    /// The old preview stays visible until the sharper one is ready.
    /// </summary>
    private async Task LoadThumbnailsAsync()
    {
        if (_loadingThumbnails) return;
        _loadingThumbnails = true;
        try
        {
            while (true)
            {
                var target = TargetRenderWidth; // re-read each time: zoom may change mid-loop
                var vm = Pages.Where(p => NeedsRender(p, target))
                              .OrderByDescending(p => p.IsOnScreen)
                              .FirstOrDefault();
                if (vm is null) break;

                try
                {
                    var height = (int)(target * PreviewAspect);
                    var png = await Task.Run(() => _renderer.RenderPng(vm.Page, target, height));
                    vm.Thumbnail = ToImage(png);
                    vm.RenderedWidth = target;
                }
                catch
                {
                    vm.ThumbnailFailed = true;
                }
            }
        }
        finally
        {
            _loadingThumbnails = false;
        }
    }

    private static BitmapImage ToImage(byte[] png)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = new MemoryStream(png);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void UpdateStatus()
    {
        var selected = Pages.Count(p => p.IsSelected);
        StatusText = Pages.Count == 0
            ? "Add PDFs or drop them anywhere in the window"
            : $"{Pages.Count} pages · {Sources.Count} file(s)" + (selected > 0 ? $" · {selected} selected" : "");
    }
}
