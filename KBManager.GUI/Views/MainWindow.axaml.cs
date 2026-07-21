using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using KBManager.GUI.ViewModels;

namespace KBManager.GUI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
