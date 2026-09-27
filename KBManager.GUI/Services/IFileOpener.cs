using System.Threading.Tasks;

namespace KBManager.GUI.Services;

/// <summary>
/// Hands files off to the operating system: opening a document with its default
/// application, or revealing it in the system file manager. Editing happens in
/// the workspace's own editor, so these are the "escape hatch" actions used by
/// the explorer.
/// </summary>
public interface IFileOpener
{
    /// <summary>Open a file with the system default application.</summary>
    /// <param name="filePath">Absolute path to the file.</param>
    Task OpenFileAsync(string filePath);

    /// <summary>
    /// Reveal a file in the OS file manager (Explorer / Finder / xdg), selecting
    /// the file itself where the platform supports it.
    /// </summary>
    /// <param name="filePath">Absolute path to the file.</param>
    Task OpenContainingFolderAsync(string filePath);
}
