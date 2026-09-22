using System.Threading.Tasks;

namespace KBManager.GUI.Services;

/// <summary>
/// What to do with unsaved edits when a document is closed.
/// </summary>
public enum UnsavedChangesChoice
{
    Save,
    Discard,
    Cancel
}

/// <summary>
/// Abstraction for dialog interactions (file/folder pickers, confirmations,
/// prompts). Keeps ViewModels testable by avoiding direct Avalonia API calls.
/// </summary>
public interface IDialogService
{
    /// <summary>Show a confirmation dialog. Returns true when user accepts.</summary>
    Task<bool> ConfirmAsync(string title, string message);

    /// <summary>Show an information dialog.</summary>
    Task ShowInfoAsync(string title, string message);

    /// <summary>Show an error dialog.</summary>
    Task ShowErrorAsync(string title, string message);

    /// <summary>
    /// Prompt for a single line of text (new file / rename).
    /// Returns null when the user cancels.
    /// </summary>
    Task<string?> PromptForTextAsync(string title, string prompt, string? initialValue = null);

    /// <summary>
    /// Ask what to do about unsaved changes before closing a document.
    /// </summary>
    Task<UnsavedChangesChoice> ConfirmUnsavedChangesAsync(string fileName);

    /// <summary>Open a folder picker dialog. Returns the selected path or null.</summary>
    Task<string?> PickFolderAsync(string title, string? defaultPath = null);

    /// <summary>Open a file picker dialog. Returns the selected path or null.</summary>
    Task<string?> PickFileAsync(string title, string? defaultPath = null);
}
