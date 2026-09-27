using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KBManager.GUI.ViewModels;
using System.Collections.Generic;

namespace KBManager.GUI.Views;

/// <summary>
/// A Markdown document: a one-row tag strip, the text surface, and a status strip.
///
/// The line-number gutter is rendered here rather than in the ViewModel because it
/// depends on the real text layout: with soft wrapping switched on, one logical line
/// can occupy several visual rows, and only the layout knows how many. The numbers
/// therefore come from the editor's own <see cref="TextLayout"/> — the same object the
/// editor renders — so they stay aligned at any window width, and because the gutter
/// lives outside the editor's scrolling axis it is always on screen.
/// </summary>
public partial class FileEditorView : UserControl
{
    private TextBox? _newTagTextBox;
    private ListBox? _suggestionListBox;
    private TextBox? _editor;
    private TextBlock? _gutter;
    private TextPresenter? _presenter;

    /// <summary>Last layout/text combination the gutter was built from.</summary>
    private TextLayout? _measuredLayout;
    private int _measuredTextLength = -1;

    private bool _gutterUpdateQueued;

    public FileEditorView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _newTagTextBox = this.FindControl<TextBox>("NewTagTextBox");
        if (_newTagTextBox != null)
            _newTagTextBox.KeyDown += OnNewTagKeyDown;

        _suggestionListBox = this.FindControl<ListBox>("TagSuggestionListBox");
        if (_suggestionListBox != null)
            _suggestionListBox.Tapped += OnSuggestionTapped;

        _editor = this.FindControl<TextBox>("EditorBox");
        _gutter = this.FindControl<TextBlock>("GutterLines");

        if (_editor != null)
        {
            // Two independent signals for a text change: the TextBox event, and the
            // property change every assignment goes through (the second one also
            // catches text arriving from the two-way binding).
            _editor.TextChanged += (_, _) => QueueGutterUpdate();
            _editor.PropertyChanged += OnEditorPropertyChanged;

            // Any layout change can re-wrap the text, so the gutter is rebuilt
            // whenever the editor completes a layout pass or is resized.
            _editor.LayoutUpdated += (_, _) => QueueGutterUpdate();

            // Working in the text surface turns a provisional tab into a real one, and
            // means the tag autocomplete is no longer wanted.
            _editor.GotFocus += (_, _) =>
            {
                if (DataContext is not FileDocumentViewModel document) return;
                document.PromoteToPermanent();
                document.DismissTagSuggestions();
            };

            _editor.ApplyTemplate();
            _presenter = _editor.FindDescendantOfType<TextPresenter>();
        }

        QueueGutterUpdate();
    }

    private void OnEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty || e.Property == TextBox.TextProperty) QueueGutterUpdate();
    }

    // ── Line numbers ───────────────────────────────────────────────────────

    /// <summary>
    /// Coalesce gutter rebuilds: a layout pass produces many changes, and the work is
    /// only worth doing once the pass is over. Background priority runs after layout,
    /// so the layout read back is the one that was just rendered.
    /// </summary>
    private void QueueGutterUpdate()
    {
        if (_gutterUpdateQueued) return;
        _gutterUpdateQueued = true;

        Dispatcher.UIThread.Post(
            () =>
            {
                _gutterUpdateQueued = false;
                UpdateGutter();
            },
            DispatcherPriority.Background);
    }

    /// <summary>
    /// Render one number per logical line, on the first visual row of that line.
    /// Rows a soft-wrapped line spills onto stay empty, which is how editors that
    /// wrap show line numbers (a wrapped line keeps its single line number).
    ///
    /// Nothing here re-queues itself: text changes arrive through TextChanged and
    /// every re-wrap arrives through a bounds change or a layout pass, so a stale
    /// result is always followed by a fresh attempt.
    /// </summary>
    private void UpdateGutter()
    {
        if (_gutter == null || _editor == null) return;

        _presenter ??= _editor.FindDescendantOfType<TextPresenter>();
        var layout = _presenter?.TextLayout;
        if (layout == null) return;

        var text = _editor.Text ?? string.Empty;

        // A layout that still reports newline-bearing text as a single line was built
        // before the first measure; the next pass produces the real one.
        if (text.Contains('\n') && layout.TextLines.Count < 2) return;

        if (ReferenceEquals(layout, _measuredLayout) && text.Length == _measuredTextLength) return;
        _measuredLayout = layout;
        _measuredTextLength = text.Length;

        var rows = new List<string>(layout.TextLines.Count);
        int lineNumber = 1;

        foreach (var line in layout.TextLines)
        {
            int start = line.FirstTextSourceIndex;

            // A visual line begins a logical line when it starts at the very
            // beginning or right after a newline; a soft wrap leaves the previous
            // character as ordinary text (usually the space it wrapped at).
            bool startsLogicalLine = start <= 0 || (start <= text.Length && text[start - 1] == '\n');

            if (startsLogicalLine)
            {
                rows.Add(lineNumber.ToString());
                lineNumber++;
            }
            else
            {
                // A row the line only spilled onto: no number, so the numbers keep
                // pointing at the line they belong to.
                rows.Add(string.Empty);
            }
        }

        _gutter.Text = string.Join("\n", rows);
    }

    // ── Tags ───────────────────────────────────────────────────────────────

    /// <summary>Enter in the tag box commits the typed tag.</summary>
    private async void OnNewTagKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not FileDocumentViewModel vm) return;

        await vm.AddTagCommand.ExecuteAsync(null);
        e.Handled = true;
    }

    /// <summary>A single click on a suggestion accepts it (double-clicking a dropdown
    /// row is a discovery problem, not a feature).</summary>
    private async void OnSuggestionTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not ListBox listBox) return;
        if (listBox.SelectedItem is not TagSuggestionItem suggestion) return;
        if (DataContext is not FileDocumentViewModel vm) return;

        await vm.AcceptTagSuggestionCommand.ExecuteAsync(suggestion);
    }

    /// <summary>Remove a tag chip.</summary>
    private async void OnRemoveTagClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: FileTagItem tag }) return;
        if (DataContext is not FileDocumentViewModel vm) return;

        await vm.RemoveTagCommand.ExecuteAsync(tag);
        e.Handled = true;
    }
}
