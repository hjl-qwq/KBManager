using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace KBManager.GUI.Services;

/// <summary>
/// Default file opener that delegates to the OS default application.
/// Replace or decorate with an embedded Markdown editor later.
/// </summary>
public class FileOpener : IFileOpener
{
    public bool HasBuiltInEditor => false;

    public Task OpenFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Task.CompletedTask;

        try
        {
            // Cross-platform system default opener
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", $"\"{filePath}\"");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", $"\"{filePath}\"");
            }
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine($"Failed to open file: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task OpenWithBuiltInEditorAsync(string filePath)
    {
        // Placeholder — will be replaced when a built-in editor is added.
        return OpenFileAsync(filePath);
    }
}
