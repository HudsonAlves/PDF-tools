using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;
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
    // Thumbnails are rendered at this size (px) and scaled down by the UI, so they stay sharp when zoomed.
    private const int RenderWidth = 300;
    private const int RenderHeight = 400;

    private readonly PageCollection _collection = new();
    private readonly IPdfReader _reader;
    private readonly IPdfWriter _writer;
    private readonly IPageRenderer _renderer;
    private readonly IDialogService _dialogs;

    private string _statusText = "";
    private bool _isBusy;
    private double _thumbnailWidth = 150;
    private bool _loadingThumbnails;

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
            if (SetProperty(ref _thumbnailWidth, value)) OnPropertyChanged(nameof(ThumbnailHeight));
        }
    }

    public double ThumbnailHeight => ThumbnailWidth * RenderHeight / RenderWidth;

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

    /// <summary>Renders missing thumbnails one at a time in the background.</summary>
    private async Task LoadThumbnailsAsync()
    {
        if (_loadingThumbnails) return;
        _loadingThumbnails = true;
        try
        {
            while (Pages.FirstOrDefault(p => !p.ThumbnailRequested) is { } vm)
            {
                vm.ThumbnailRequested = true;
                try
                {
                    var png = await Task.Run(() => _renderer.RenderPng(vm.Page, RenderWidth, RenderHeight));
                    vm.Thumbnail = ToImage(png);
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
