using Avalonia.Controls;
using Avalonia.Markup.Xaml;

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
        if (DataContext is ViewModels.FileListViewModel vm)
        {
            _ = vm.LoadFilesCommand.ExecuteAsync(null);
        }
    }
}
