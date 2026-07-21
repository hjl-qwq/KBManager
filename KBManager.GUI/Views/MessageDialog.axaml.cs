using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using KBManager.GUI.Services;

namespace KBManager.GUI.Views;

/// <summary>
/// Simple info/error message dialog.
/// </summary>
public partial class MessageDialog : Window
{
    public MessageDialog()
    {
        InitializeComponent();
    }

    public MessageDialog(string title, string message, MessageDialogType type) : this()
    {
        Title = title;
        Message = message;
        DialogType = type;
    }

    public string Message { get; } = string.Empty;
    public MessageDialogType DialogType { get; }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnOkClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
