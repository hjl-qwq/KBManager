using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KBManager.GUI.Views;

public partial class RepoOpsView : UserControl
{
    public RepoOpsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    // NOTE: the shell calls RepoOpsViewModel.Activate() when the Git tab is opened.
    // Doing it here as well would re-log the config on every tab switch.
}
