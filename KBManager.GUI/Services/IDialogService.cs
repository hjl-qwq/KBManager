using System.Threading.Tasks;

namespace KBManager.GUI.Services;

/// <summary>
/// Abstraction for dialog interactions (file/folder pickers, confirmations).
/// Keeps ViewModels testable by avoiding direct Avalonia API calls.
/// </summary>
public interface IDialogService
{
    /// <summary>Show a confirmation dialog. Returns true when user accepts.</summary>
    Task<bool> ConfirmAsync(string title, string message);

    /// <summary>Show an information dialog.</summary>
    Task ShowInfoAsync(string title, string message);

    /// <summary>Show an error dialog.</summary>
    Task ShowErrorAsync(string title, string message);

    /// <summary>Open a folder picker dialog. Returns the selected path or null.</summary>
    Task<string?> PickFolderAsync(string title, string? defaultPath = null);

    /// <summary>Open a file picker dialog. Returns the selected path or null.</summary>
    Task<string?> PickFileAsync(string title, string? defaultPath = null);
}
