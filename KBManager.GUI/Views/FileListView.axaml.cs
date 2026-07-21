using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using KBManager.GUI.ViewModels;

namespace KBManager.GUI.Views;

public partial class FileListView : UserControl
{
    public FileListView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
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
