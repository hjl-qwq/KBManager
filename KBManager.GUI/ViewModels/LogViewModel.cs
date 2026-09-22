using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KBManager.core;
using KBManager.GUI.Services;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace KBManager.GUI.ViewModels;

/// <summary>
/// Shows this session's diagnostic log inside the editor area, so a failure can
/// be investigated without hunting for a file on disk. The log file path is shown
/// and can be opened with the OS default application.
/// </summary>
public partial class LogViewModel : ViewModelBase
{
    private readonly IDialogService _dialogService;

    public LogViewModel(IDialogService dialogService)
    {
        _dialogService = dialogService;
        Refresh();
    }

    [ObservableProperty]
    private string _logText = string.Empty;

    [ObservableProperty]
    private string _logFilePath = string.Empty;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private int _errorCount;

    public void Refresh()
    {
        LogFilePath = AppLog.LogFilePath;
        LogText = AppLog.SnapshotText();
        ErrorCount = AppLog.ErrorCount;
        Summary = ErrorCount > 0
            ? $"本次会话记录 {ErrorCount} 条错误"
            : "本次会话没有记录到错误";
    }

    [RelayCommand]
    private void Reload() => Refresh();

    /// <summary>Open the log file with the OS default application.</summary>
    [RelayCommand]
    private async Task OpenLogFileAsync()
    {
        try
        {
            var path = AppLog.LogFilePath;
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                await _dialogService.ShowInfoAsync("日志文件", "日志文件尚未创建。");
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", $"\"{path}\"");
            else
                Process.Start("xdg-open", $"\"{path}\"");

            AppLog.Info("已用外部程序打开日志文件");
            Refresh();
        }
        catch (Exception ex)
        {
            AppLog.Error("打开日志文件失败", ex);
            await _dialogService.ShowErrorAsync("打开日志失败", ex.Message);
        }
    }

    /// <summary>Copy the whole log to the clipboard so it can be pasted into an issue.</summary>
    [RelayCommand]
    private async Task CopyLogAsync()
    {
        // Clipboard lives on the TopLevel, so resolve the main window the same way
        // the dialog service does.
        var window = Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        var clipboard = window?.Clipboard;
        if (clipboard == null)
        {
            await _dialogService.ShowErrorAsync("复制失败", "无法访问剪贴板。");
            return;
        }

        await clipboard.SetTextAsync(AppLog.SnapshotText());
        StatusMessage = "日志已复制到剪贴板";
    }
}
