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
        Services = ConfigureServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

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
