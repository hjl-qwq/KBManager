using System.Threading.Tasks;

namespace KBManager.GUI.Services;

/// <summary>
/// Abstraction for opening files with external or built-in editors.
/// Allows future replacement with an embedded Markdown editor.
/// </summary>
public interface IFileOpener
{
    /// <summary>
    /// Open a file with the system default application, or with a built-in
    /// editor if one is registered.
    /// </summary>
    /// <param name="filePath">Absolute path to the file.</param>
    Task OpenFileAsync(string filePath);

    /// <summary>
    /// True when a built-in editor is available and should be preferred
    /// over the system default opener.
    /// </summary>
    bool HasBuiltInEditor { get; }

    /// <summary>
    /// Open the file using the built-in editor (if available).
    /// </summary>
    Task OpenWithBuiltInEditorAsync(string filePath);
}
