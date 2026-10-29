# PDF Tools – Merge & Sort

Windows desktop app (.NET 10, WinForms) to merge PDFs, reorder their pages, and save a new PDF.

## Run

```bash
dotnet run --project src/PdfTools.App
```

```bash
dotnet test
```

## Using it

- **Add PDFs…** (Ctrl+O) or drag files from Explorer into the window. Every page appears as a thumbnail.
- **Reorder**: drag thumbnails, or select and use **Move up / Move down** (Ctrl+↑ / Ctrl+↓).
- **Sort** menu: original order, reverse, by file name, interleave files (front/back scans), odd-then-even.
- **Remove** (Delete) drops selected pages. Ctrl+A selects all.
- **Save as…** (Ctrl+S) writes the pages, in the shown order, to a new PDF.

## Project layout

```
src/PdfTools.Core      UI-independent logic (reuse it from a CLI, web API, etc.)
  Models/              PdfSourceFile (a loaded file), PageItem (one output page)
  PageCollection.cs    The ordered page list: add, move, nudge, remove, sort
  Sorting/             IPageSorter + built-in sorters, registered in PageSorters.All
  Services/            IPdfReader / IPdfWriter / IPageRenderer and their implementations
src/PdfTools.App       WinForms UI
  Program.cs           Composition root – choose service implementations here
  MainForm.cs          Toolbar, shortcuts, actions
  Controls/PageListView.cs  Thumbnail grid with drag-and-drop
tests/PdfTools.Tests   xUnit tests (build real PDFs, merge, sort, verify output)
```

## Extending

| To add…                     | Do this |
|-----------------------------|---------|
| A new sort order            | Implement `IPageSorter`, add it to `PageSorters.All`. It shows up in the Sort menu. |
| A per-page option (rotate…) | Add a property to `PageItem`, apply it in `PdfSharpWriter.Save`. |
| A new page operation        | Add a method to `PageCollection` (raise `OnChanged()`), add a button in `MainForm.BuildToolbar`. |
| A different PDF library     | Implement the interfaces in `Services/` and swap them in `Program.cs`. |

## Libraries

- [PDFsharp](https://www.pdfsharp.com/) **6.2.4** (MIT) – reading/merging/writing. Versions 6.5+ are AGPL/commercial, so don't upgrade without checking the license.
- [PDFtoImage](https://github.com/sungaila/PDFtoImage) (MIT, uses PDFium) – thumbnails.
