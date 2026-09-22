using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using KBManager.GUI.Services;

namespace KBManager.GUI.Views;

/// <summary>
/// Three-way prompt shown when closing a document with unsaved changes, so the
/// user can save, discard, or back out instead of being forced into yes/no.
/// </summary>
public partial class UnsavedChangesDialog : Window
{
    public UnsavedChangesDialog()
    {
        InitializeComponent();
    }

    public UnsavedChangesDialog(string fileName) : this()
    {
        Message = $"\"{fileName}\" 尚未保存。是否在关闭前保存？";
    }

    public string Message { get; } = string.Empty;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Save);

    private void OnDiscardClicked(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Discard);

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Cancel);
}
