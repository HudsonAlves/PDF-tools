using PdfTools.App.Controls;
using PdfTools.Core;
using PdfTools.Core.Models;
using PdfTools.Core.Services;
using PdfTools.Core.Sorting;

namespace PdfTools.App;

/// <summary>
/// Main window. The UI is built in code (no designer file) so it is easy to read and change.
/// To add a feature: add a toolbar button in <see cref="BuildToolbar"/> and a handler below
/// that calls into <see cref="PageCollection"/> or a Core service.
/// </summary>
public sealed class MainForm : Form
{
    private readonly PageCollection _pages = new();
    private readonly IPdfReader _reader;
    private readonly IPdfWriter _writer;

    private readonly PageListView _list = new() { Dock = DockStyle.Fill };
    private readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };

    public MainForm(IPdfReader reader, IPdfWriter writer, IPageRenderer renderer)
    {
        _reader = reader;
        _writer = writer;

        Text = "PDF Tools – Merge & Sort";
        Size = new Size(1100, 750);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        _list.SetRenderer(renderer);
        _list.PagesDropped += (pages, index) => _pages.MovePages(pages, index);
        _list.FilesDropped += AddFiles;
        _list.SelectedIndexChanged += (_, _) => UpdateStatus();

        _pages.Changed += (_, _) => RefreshView();

        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_status);

        // Order matters for docking: Fill control first, then edges.
        Controls.Add(_list);
        Controls.Add(BuildToolbar());
        Controls.Add(statusStrip);

        UpdateStatus();
    }

    private ToolStrip BuildToolbar()
    {
        var sortMenu = new ToolStripDropDownButton("Sort");
        foreach (var sorter in PageSorters.All)
        {
            sortMenu.DropDownItems.Add(sorter.Name, null, (_, _) => Run(() => _pages.Sort(sorter)));
        }

        return new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            Items =
            {
                Button("Add PDFs…", (_, _) => BrowseAndAddFiles()),
                Button("Save as…", (_, _) => SaveAs()),
                new ToolStripSeparator(),
                Button("▲ Move up", (_, _) => _pages.Nudge(_list.SelectedPages, -1)),
                Button("▼ Move down", (_, _) => _pages.Nudge(_list.SelectedPages, +1)),
                Button("Remove", (_, _) => _pages.Remove(_list.SelectedPages)),
                new ToolStripSeparator(),
                sortMenu,
                new ToolStripSeparator(),
                Button("Clear all", (_, _) => ClearAll()),
            },
        };

        static ToolStripButton Button(string text, EventHandler onClick) => new(text, null, onClick);
    }

    // ---- Keyboard shortcuts -------------------------------------------------

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.O: BrowseAndAddFiles(); return true;
            case Keys.Control | Keys.S: SaveAs(); return true;
            case Keys.Control | Keys.Up: _pages.Nudge(_list.SelectedPages, -1); return true;
            case Keys.Control | Keys.Down: _pages.Nudge(_list.SelectedPages, +1); return true;
            case Keys.Delete when _list.Focused: _pages.Remove(_list.SelectedPages); return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ---- Actions ------------------------------------------------------------

    private void BrowseAndAddFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Add PDF files",
            Filter = "PDF files (*.pdf)|*.pdf",
            Multiselect = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            AddFiles(dialog.FileNames);
        }
    }

    private void AddFiles(IEnumerable<string> paths)
    {
        var failed = new List<string>();
        foreach (var path in paths.Where(p => p.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                _pages.AddDocument(_reader.Open(path));
            }
            catch (Exception ex)
            {
                failed.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }

        if (failed.Count > 0)
        {
            ShowError("Some files could not be opened:\n\n" + string.Join("\n", failed));
        }
    }

    private void SaveAs()
    {
        if (_pages.Count == 0)
        {
            ShowError("Add some PDF files first.");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Save merged PDF",
            Filter = "PDF files (*.pdf)|*.pdf",
            FileName = "merged.pdf",
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var pages = _pages.Pages.ToList(); // snapshot
        UseWaitCursor = true;
        Enabled = false;
        _status.Text = "Saving…";

        Task.Run(() => _writer.Save(pages, dialog.FileName)).ContinueWith(t =>
        {
            UseWaitCursor = false;
            Enabled = true;
            UpdateStatus();
            if (t.Exception is not null)
            {
                ShowError("Could not save the PDF:\n\n" + t.Exception.GetBaseException().Message);
            }
            else
            {
                _status.Text = $"Saved {pages.Count} pages to {dialog.FileName}";
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void ClearAll()
    {
        if (_pages.Count == 0) return;
        if (MessageBox.Show(this, "Remove all pages from the list?", Text, MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            _pages.Clear();
        }
    }

    // ---- View helpers -------------------------------------------------------

    private void RefreshView()
    {
        _list.ShowPages(_pages.Pages);
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        _status.Text = _pages.Count == 0
            ? "Add PDFs with the toolbar, Ctrl+O, or drag files here. Drag pages to reorder."
            : $"{_pages.Count} pages from {_pages.Sources.Count} file(s) · {_list.SelectedItems.Count} selected";
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void ShowError(string message) =>
        MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
