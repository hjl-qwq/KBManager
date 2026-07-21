using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KBManager.GUI.Views;

/// <summary>
/// Simple yes/no confirmation dialog.
/// </summary>
public partial class ConfirmationDialog : Window
{
    public ConfirmationDialog()
    {
        InitializeComponent();
    }

    public ConfirmationDialog(string title, string message) : this()
    {
        Title = title;
        Message = message;
    }

    public string Message { get; } = string.Empty;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnYesClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnNoClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }
}
