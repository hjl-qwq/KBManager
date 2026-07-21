using Avalonia.Controls;
using Avalonia.Platform.Storage;
using KBManager.GUI.Views;
using System.Linq;
using System.Threading.Tasks;

namespace KBManager.GUI.Services;

/// <summary>
/// Avalonia-backed dialog service. Resolves the top-level window from the
/// application lifetime for native dialog parent.
/// </summary>
public class DialogService : IDialogService
{
    private static Window? GetMainWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var window = GetMainWindow();
        if (window == null) return false;

        var dialog = new ConfirmationDialog(title, message);
        var result = await dialog.ShowDialog<bool>(window);
        return result;
    }

    public async Task ShowInfoAsync(string title, string message)
    {
        var window = GetMainWindow();
        if (window == null) return;

        var dialog = new MessageDialog(title, message, MessageDialogType.Info);
        await dialog.ShowDialog(window);
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        var window = GetMainWindow();
        if (window == null) return;

        var dialog = new MessageDialog(title, message, MessageDialogType.Error);
        await dialog.ShowDialog(window);
    }

    public async Task<string?> PickFolderAsync(string title, string? defaultPath = null)
    {
        var window = GetMainWindow();
        if (window == null) return null;

        var storage = window.StorageProvider;
        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.FirstOrDefault()?.Path.LocalPath;
    }

    public async Task<string?> PickFileAsync(string title, string? defaultPath = null)
    {
        var window = GetMainWindow();
        if (window == null) return null;

        var storage = window.StorageProvider;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return files.FirstOrDefault()?.Path.LocalPath;
    }
}

/// <summary>
/// Simple enum for message dialog type.
/// </summary>
public enum MessageDialogType { Info, Error, Warning }
