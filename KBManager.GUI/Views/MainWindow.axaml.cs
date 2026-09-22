using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using KBManager.GUI.ViewModels;
using System;

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

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is MainViewModel vm)
            _ = vm.InitializeAsync();
    }
}
