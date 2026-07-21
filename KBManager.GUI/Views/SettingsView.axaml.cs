using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KBManager.GUI.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
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
        if (DataContext is ViewModels.SettingsViewModel vm)
        {
            vm.ActivateCommand.Execute(null);
        }
    }
}
