using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace KBManager.GUI.Services;

/// <summary>
/// Opens files with the OS default application (Explorer → 外部打开).
/// </summary>
public class FileOpener : IFileOpener
{
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
}
