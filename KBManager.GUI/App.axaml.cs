using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KBManager.GUI.Services;
using KBManager.GUI.ViewModels;
using KBManager.GUI.Views;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace KBManager.GUI;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 1) 加载主题 — 必须在任何 UI 创建之前
        ThemeManager themeManager;
        try
        {
            themeManager = new ThemeManager();
            themeManager.LoadTheme();
            themeManager.ApplyToApplication(this);
        }
        catch (Exception ex)
        {
            // 主题加载失败时使用默认值，确保应用仍能启动
            System.Diagnostics.Debug.WriteLine($"Theme load failed: {ex.Message}");
            themeManager = new ThemeManager(); // 使用默认主题
            themeManager.ApplyToApplication(this);
        }

        // 2) DI 容器
        Services = ConfigureServices(themeManager);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IServiceProvider ConfigureServices(ThemeManager themeManager)
    {
        var services = new ServiceCollection();

        // Theme
        services.AddSingleton(themeManager);
        services.AddSingleton(themeManager.CurrentTheme);

        // Core services (UI-agnostic)
        services.AddSingleton<KBManager.core.GitHelper>();
        services.AddSingleton<KBManager.core.IKnowledgeBaseService, KBManager.core.KnowledgeBaseService>();
        services.AddSingleton<KBManager.core.IFileScanService, KBManager.core.FileScanService>();

        // GUI services
        services.AddSingleton<IFileOpener, FileOpener>();
        services.AddSingleton<IDialogService, DialogService>();

        // ViewModels
        services.AddTransient<SearchViewModel>();
        services.AddTransient<FileListViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<RepoOpsViewModel>();
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }
}
