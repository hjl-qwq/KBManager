using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KBManager.GUI.Views;

/// <summary>
/// The editor area: the document tab strip plus the active document's content,
/// or an empty state when no document is open.
/// </summary>
public partial class EditorHostView : UserControl
{
    public EditorHostView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
