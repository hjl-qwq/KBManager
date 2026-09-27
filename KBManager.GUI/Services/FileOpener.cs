using KBManager.core;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace KBManager.GUI.Services;

/// <summary>
/// Hands files off to the OS: open with the default application, or reveal in
/// the system file manager.
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
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open file: {ex.Message}");
            AppLog.Error($"外部打开失败：{filePath}", ex);
        }

        return Task.CompletedTask;
    }

    public Task OpenContainingFolderAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Task.CompletedTask;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // `/select,` opens Explorer with the file highlighted. Explorer
                // returns a non-zero exit code even on success, so the exit code is
                // deliberately ignored here.
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{filePath}\"",
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // -R reveals (selects) the file in Finder.
                Process.Start("open", $"-R \"{filePath}\"");
            }
            else
            {
                // Most Linux file managers have no portable "select file" switch,
                // so open the containing directory instead.
                var directory = Path.GetDirectoryName(filePath);
                if (string.IsNullOrEmpty(directory)) return Task.CompletedTask;
                Process.Start("xdg-open", $"\"{directory}\"");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open containing folder: {ex.Message}");
            AppLog.Error($"打开所在目录失败：{filePath}", ex);
        }

        return Task.CompletedTask;
    }
}
