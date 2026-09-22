using System.Threading.Tasks;

namespace KBManager.GUI.Services;

/// <summary>
/// Opens a file with the operating system's default application. Editing happens
/// in the workspace's own editor, so this is only the "open externally" escape
/// hatch used by the explorer.
/// </summary>
public interface IFileOpener
{
    /// <summary>Open a file with the system default application.</summary>
    /// <param name="filePath">Absolute path to the file.</param>
    Task OpenFileAsync(string filePath);
}
