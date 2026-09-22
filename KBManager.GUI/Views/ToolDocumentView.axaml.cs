using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KBManager.GUI.Views;

/// <summary>
/// Hosts a tool page (Settings, Git operations) inside an editor-area tab.
/// </summary>
public partial class ToolDocumentView : UserControl
{
    public ToolDocumentView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
