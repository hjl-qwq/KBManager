using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;

namespace KBManager.GUI.Views;

/// <summary>
/// Single-line text prompt used by the explorer for "new file" and "rename".
/// </summary>
public partial class TextInputDialog : Window
{
    public TextInputDialog()
    {
        InitializeComponent();
    }

    public TextInputDialog(string title, string prompt, string? initialValue = null) : this()
    {
        Title = title;
        Prompt = prompt;
        _initialValue = initialValue;
    }

    private readonly string? _initialValue;

    public string Prompt { get; } = string.Empty;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // Focus once the window is shown, so the user can type immediately.
        var box = this.FindControl<TextBox>("InputBox");
        if (box == null) return;

        box.Text = _initialValue ?? string.Empty;
        box.Focus();
        box.SelectAll();
    }

    private void OnOkClicked(object? sender, RoutedEventArgs e)
    {
        var box = this.FindControl<TextBox>("InputBox");
        Close(box?.Text);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(null);

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        var box = this.FindControl<TextBox>("InputBox");
        Close(box?.Text);
        e.Handled = true;
    }
}
