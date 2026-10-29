using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PdfTools.App.ViewModels;
using PdfTools.Core.Models;

namespace PdfTools.App;

/// <summary>
/// View-only code: drag &amp; drop, the sort menu, and a few keyboard shortcuts.
/// Everything that changes data goes through <see cref="MainViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    private const string PagesDragFormat = "PdfTools.Pages";

    private readonly MainViewModel _vm;
    private Point? _dragStart;
    private PageViewModel? _pressedOnSelected;
    private PageViewModel? _dropTarget;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _vm = viewModel;
        Loaded += (_, _) => _vm.RenderScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        DpiChanged += (_, e) => _vm.RenderScale = e.NewDpi.DpiScaleX;
    }

    // ---- Zoom & preview quality ---------------------------------------------------

    private void PageList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;

        var command = e.Delta > 0 ? _vm.ZoomInCommand : _vm.ZoomOutCommand;
        if (command.CanExecute(null)) command.Execute(null);
        e.Handled = true;
    }

    private void PageList_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Room for one card: list width minus list padding, item margin, card padding/border and the scrollbar.
        var chrome = 16 + 12 + 20 + SystemParameters.VerticalScrollBarWidth;
        _vm.AvailableWidth = PageList.ActualWidth - chrome;
    }

    /// <summary>Tells the view model which cards are visible so their previews are sharpened first.</summary>
    private void PageList_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        var viewport = new Rect(0, 0, PageList.ActualWidth, PageList.ActualHeight);
        var onScreen = new HashSet<PageViewModel>();
        foreach (var page in _vm.Pages)
        {
            if (PageList.ItemContainerGenerator.ContainerFromItem(page) is not ListBoxItem { IsVisible: true } item) continue;
            var bounds = item.TransformToAncestor(PageList).TransformBounds(new Rect(item.RenderSize));
            if (bounds.IntersectsWith(viewport)) onScreen.Add(page);
        }
        _vm.SetOnScreenPages(onScreen);
    }

    // ---- Sort menu ------------------------------------------------------------

    private void SortButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = SortButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var sorter in _vm.Sorters)
        {
            menu.Items.Add(new MenuItem { Header = sorter.Name, Command = _vm.SortCommand, CommandParameter = sorter });
        }
        menu.IsOpen = true;
    }

    // ---- Keyboard ---------------------------------------------------------------

    private void PageList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var command = e.Key switch
        {
            Key.Up when ctrl => _vm.MoveUpCommand,
            Key.Left when ctrl => _vm.MoveUpCommand,
            Key.Down when ctrl => _vm.MoveDownCommand,
            Key.Right when ctrl => _vm.MoveDownCommand,
            Key.Delete => _vm.RemoveCommand,
            _ => null,
        };

        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
            e.Handled = true;
        }
    }

    private void PageList_SelectionChanged(object sender, SelectionChangedEventArgs e) => _vm.OnSelectionChanged();

    // ---- Dragging pages within the grid ------------------------------------------

    private void PageList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var page = PageAt(e.OriginalSource);
        _dragStart = page is null ? null : e.GetPosition(PageList);

        // Clicking an already-selected card would normally collapse a multi-selection before
        // the drag starts. Swallow the click so all selected cards can be dragged together;
        // if no drag happens, the mouse-up handler applies the normal single selection.
        _pressedOnSelected = null;
        if (page is { IsSelected: true } && Keyboard.Modifiers == ModifierKeys.None && _vm.SelectedPages.Count > 1)
        {
            _pressedOnSelected = page;
            PageList.Focus();
            e.Handled = true;
        }
    }

    private void PageList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_pressedOnSelected is { } page)
        {
            foreach (var p in _vm.Pages) p.IsSelected = p == page;
        }
        _pressedOnSelected = null;
        _dragStart = null;
    }

    private void PageList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is not { } start || e.LeftButton != MouseButtonState.Pressed) return;

        var delta = e.GetPosition(PageList) - start;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        _dragStart = null;
        _pressedOnSelected = null;
        var pages = _vm.SelectedPages;
        if (pages.Count == 0) return;

        DragDrop.DoDragDrop(PageList, new DataObject(PagesDragFormat, pages), DragDropEffects.Move);
        ClearDropMarker();
    }

    private void PageList_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(PagesDragFormat)) return; // let file drops bubble to the window

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
        AutoScroll(e.GetPosition(PageList));

        var (target, position) = DropTargetAt(e);
        if (target != _dropTarget) ClearDropMarker();
        _dropTarget = target;
        if (target is not null) target.DropPosition = position;
    }

    private void PageList_DragLeave(object sender, DragEventArgs e) => ClearDropMarker();

    private void PageList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(PagesDragFormat) is not IReadOnlyList<PageItem> pages) return;
        e.Handled = true;

        var (target, position) = DropTargetAt(e);
        ClearDropMarker();

        var index = target is null
            ? _vm.Pages.Count
            : _vm.Pages.IndexOf(target) + (position == DropPosition.After ? 1 : 0);
        _vm.MovePages(pages, index);
    }

    /// <summary>The card under the pointer and which half of it; past the last card means "after the last".</summary>
    private (PageViewModel? Target, DropPosition Position) DropTargetAt(DragEventArgs e)
    {
        if (ContainerAt(e.OriginalSource) is { DataContext: PageViewModel page } container)
        {
            var x = e.GetPosition(container).X;
            return (page, x < container.ActualWidth / 2 ? DropPosition.Before : DropPosition.After);
        }

        return (_vm.Pages.LastOrDefault(), DropPosition.After);
    }

    private void ClearDropMarker()
    {
        if (_dropTarget is not null) _dropTarget.DropPosition = DropPosition.None;
        _dropTarget = null;
    }

    private void AutoScroll(Point position)
    {
        if (FindChild<ScrollViewer>(PageList) is not { } scroll) return;
        const double edge = 40;
        if (position.Y < edge) scroll.LineUp();
        else if (position.Y > PageList.ActualHeight - edge) scroll.LineDown();
    }

    // ---- Dropping PDF files from Explorer (anywhere in the window) -------------------

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files) _vm.AddFiles(files);
    }

    // ---- Visual tree helpers ------------------------------------------------------------

    private static PageViewModel? PageAt(object source) => ContainerAt(source)?.DataContext as PageViewModel;

    private static ListBoxItem? ContainerAt(object source)
    {
        for (var d = source as DependencyObject; d is not null; d = ParentOf(d))
        {
            if (d is ListBoxItem item) return item;
        }
        return null;
    }

    private static DependencyObject? ParentOf(DependencyObject d) =>
        d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            if (FindChild<T>(child) is { } found) return found;
        }
        return null;
    }
}
