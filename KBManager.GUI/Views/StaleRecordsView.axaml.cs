using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KBManager.GUI.Views;

/// <summary>
/// Stale-index cleanup page: lists every index record whose file is gone and puts
/// the decision button at the bottom, so a long list stays readable.
/// </summary>
public partial class StaleRecordsView : UserControl
{
    public StaleRecordsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
