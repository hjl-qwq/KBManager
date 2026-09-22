using Avalonia;
using KBManager.core;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace KBManager.GUI;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // The log must exist before anything else can fail, so a user always has
        // somewhere to look. Path is surfaced in the UI and in error dialogs.
        AppLog.Initialize();
        AppLog.Info($"KBManager GUI 启动 · 版本 {typeof(Program).Assembly.GetName().Version} · " +
                    $"{RuntimeInformation.OSDescription} · .NET {Environment.Version}");

        // 注册全局未处理异常捕获，写入 crash.log 与应用日志
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            WriteCrashLog("UnhandledException", e.ExceptionObject as Exception);
        };

        // 未观察的 Task 异常（async void 等）不再静默丢失
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLog.Error("未观察的任务异常", e.Exception);
            e.SetObserved();
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
        finally
        {
            AppLog.Info("KBManager GUI 退出");
        }
    }

    private static void WriteCrashLog(string source, Exception? ex)
    {
        AppLog.Error($"未处理异常（{source}）", ex);
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
