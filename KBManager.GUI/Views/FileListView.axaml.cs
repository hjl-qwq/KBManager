using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using KBManager.GUI.ViewModels;

namespace KBManager.GUI.Views;

public partial class FileListView : UserControl
{
    private ListBox? _tagSuggestionListBox;
    private TextBox? _tagInputTextBox;

    public FileListView()
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

        _tagSuggestionListBox = this.FindControl<ListBox>("TagSuggestionListBox");
        if (_tagSuggestionListBox != null)
            _tagSuggestionListBox.DoubleTapped += TagSuggestionListBox_DoubleTapped;

        _tagInputTextBox = this.FindControl<TextBox>("TagInputTextBox");
        if (_tagInputTextBox != null)
            _tagInputTextBox.KeyDown += TagInputTextBox_KeyDown;
    }

    protected override void OnAttachedToVisualTree(global::Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is FileListViewModel vm)
        {
            _ = vm.LoadFilesCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// Double-click a suggestion: directly add the tag and clear the input.
    /// </summary>
    private void TagSuggestionListBox_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox listBox
            && listBox.SelectedItem is string tagName
            && DataContext is FileListViewModel vm)
        {
            vm.AddTagDirectlyCommand.Execute(tagName);
        }
    }

    /// <summary>
    /// Press Enter in the tag input: same as clicking the + button.
    /// </summary>
    private void TagInputTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is FileListViewModel vm)
        {
            vm.AddTagCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Handle tag chip remove button click.
    /// </summary>
    private void OnRemoveTagClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FileTagItem tagItem
            && DataContext is FileListViewModel vm)
        {
            vm.RemoveTagCommand.Execute(tagItem);
        }
    }
}
