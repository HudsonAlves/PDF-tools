# PDF Tools – Merge & Sort

Windows desktop app (.NET 10, WPF with the built-in Windows 11 Fluent theme; follows light/dark mode) to merge PDFs, reorder their pages, and save a new PDF.

## Run

```bash
dotnet run --project src/PdfTools.App
```

```bash
dotnet test
```

In VS Code, press F5 (needs the C# Dev Kit extension).

## Using it

- **Add PDFs** (Ctrl+O), drag files from Explorer into the window, or pass them on the command line. Every page appears as a card.
- **Reorder**: drag cards (multi-select with Ctrl/Shift), or use the arrow buttons (Ctrl+←/→ or Ctrl+↑/↓).
- **Sort** menu: original order, reverse, by file name, interleave files (front/back scans), odd-then-even.
- **Remove** (Delete) drops selected pages; the × button clears everything. Ctrl+A selects all.
- **Files panel**: hover a file and click × to remove all its pages.
- **Zoom**: −/+ buttons or slider at the bottom right, Ctrl+scroll wheel, Ctrl+= / Ctrl+−; click the % (or Ctrl+0) to reset. Maximum zoom fits one page to the width. Previews are re-rendered at higher resolution as you zoom in (visible pages first), so text stays sharp.
- **Save PDF** (Ctrl+S) writes the pages, in the shown order, to a new PDF.

## Project layout

```
src/PdfTools.Core      UI-independent logic (reuse it from a CLI, web API, etc.)
  Models/              PdfSourceFile (a loaded file), PageItem (one output page)
  PageCollection.cs    The ordered page list: add, move, nudge, remove, sort
  Sorting/             IPageSorter + built-in sorters, registered in PageSorters.All
  Services/            IPdfReader / IPdfWriter / IPageRenderer and their implementations
src/PdfTools.App       WPF UI (MVVM)
  App.xaml(.cs)        Theme (ThemeMode), shared styles, composition root
  MainWindow.xaml      Layout: header buttons, files panel, page cards, status bar
  MainWindow.xaml.cs   View-only code: drag & drop, sort menu, keyboard shortcuts
  ViewModels/          MainViewModel (state + commands), PageViewModel (one card)
  Services/            IDialogService (file pickers, message boxes)
  Mvvm/                ObservableObject, RelayCommand
tests/PdfTools.Tests   xUnit tests (build real PDFs, merge, sort, verify output)
```

## Extending

| To add…                     | Do this |
|-----------------------------|---------|
| A new sort order            | Implement `IPageSorter`, add it to `PageSorters.All`. It shows up in the Sort menu. |
| A per-page option (rotate…) | Add a property to `PageItem`, apply it in `PdfSharpWriter.Save`. |
| A new page operation        | Add a method to `PageCollection` (raise `OnChanged()`), a command in `MainViewModel`, and a button bound to it in `MainWindow.xaml`. |
| A change to the look        | Page card: `ListBox.ItemContainerStyle` / `ItemTemplate` in `MainWindow.xaml`. Shared styles: `App.xaml`. Use `{DynamicResource …Brush}` theme colors so light and dark mode both work. |
| A different PDF library     | Implement the interfaces in `Core/Services/` and swap them in `App.xaml.cs`. |

## Libraries

- [PDFsharp](https://www.pdfsharp.com/) **6.2.4** (MIT) – reading/merging/writing. Versions 6.5+ are AGPL/commercial, so don't upgrade without checking the license.
- [PDFtoImage](https://github.com/sungaila/PDFtoImage) (MIT, uses PDFium) – thumbnails.
