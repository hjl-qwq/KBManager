using Avalonia;
using System;
using System.IO;
using System.Runtime.ExceptionServices;

namespace KBManager.GUI;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 注册全局未处理异常捕获，写入桌面 crash.log
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            WriteCrashLog("UnhandledException", e.ExceptionObject as Exception);
        };

        // 保留 FirstChanceException 以便调试（发布后可移除）
        AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
        {
            // 仅记录，不中断
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            WriteCrashLog("MainCatch", ex);
            throw;
        }
    }

    private static void WriteCrashLog(string source, Exception? ex)
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var logPath = Path.Combine(desktop, "KBManager_crash.log");
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var entry = $"[{timestamp}] [{source}]\n{ex}\n\n";
            File.AppendAllText(logPath, entry);
        }
        catch
        {
            // 无法写入日志，静默忽略
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
