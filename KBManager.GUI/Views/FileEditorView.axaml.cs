using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using KBManager.GUI.ViewModels;

namespace KBManager.GUI.Views;

/// <summary>
/// A Markdown document: tag strip on top, line-numbered text surface below.
/// </summary>
public partial class FileEditorView : UserControl
{
    private TextBox? _newTagTextBox;
    private ListBox? _suggestionListBox;

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
            _suggestionListBox.DoubleTapped += OnSuggestionDoubleTapped;
    }

    /// <summary>Enter in the tag box commits the typed tag.</summary>
    private async void OnNewTagKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not FileDocumentViewModel vm) return;

        await vm.AddTagCommand.ExecuteAsync(null);
        e.Handled = true;
    }

    /// <summary>Double-clicking a suggestion accepts it.</summary>
    private async void OnSuggestionDoubleTapped(object? sender, TappedEventArgs e)
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
