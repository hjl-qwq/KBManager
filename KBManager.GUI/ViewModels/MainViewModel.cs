using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KBManager.core;
using KBManager.GUI.Services;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace KBManager.GUI.ViewModels;

/// <summary>
/// Available navigation pages.
/// </summary>
public enum NavPage
{
    Search,
    FileList,
    Settings,
    RepoOps
}

/// <summary>
/// Main ViewModel — owns navigation and shared state.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly GitHelper _gitHelper;
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private NavPage _currentPage = NavPage.FileList;

    [ObservableProperty]
    private ViewModelBase _currentPageViewModel = null!;

    [ObservableProperty]
    private string _repositoryPath = "(未配置)";

    [ObservableProperty]
    private string _fileCount = "—";

    public MainViewModel(GitHelper gitHelper, IServiceProvider serviceProvider)
    {
        _gitHelper = gitHelper;
        _serviceProvider = serviceProvider;

        // Default to file list
        NavigateTo(NavPage.FileList);
        RefreshRepoInfo();
    }

    /// <summary>
    /// Resolve a page ViewModel from DI container.
    /// </summary>
    private ViewModelBase ResolvePage(NavPage page) => page switch
    {
        NavPage.Search => _serviceProvider.GetRequiredService<SearchViewModel>(),
        NavPage.FileList => _serviceProvider.GetRequiredService<FileListViewModel>(),
        NavPage.Settings => _serviceProvider.GetRequiredService<SettingsViewModel>(),
        NavPage.RepoOps => _serviceProvider.GetRequiredService<RepoOpsViewModel>(),
        _ => throw new ArgumentOutOfRangeException(nameof(page))
    };

    [RelayCommand]
    private void NavigateTo(NavPage page)
    {
        CurrentPage = page;
        CurrentPageViewModel = ResolvePage(page);
    }

    [RelayCommand]
    private void NavigateToSearch() => NavigateTo(NavPage.Search);

    [RelayCommand]
    private void NavigateToFileList() => NavigateTo(NavPage.FileList);

    [RelayCommand]
    private void NavigateToSettings() => NavigateTo(NavPage.Settings);

    [RelayCommand]
    private void NavigateToRepoOps() => NavigateTo(NavPage.RepoOps);

    /// <summary>
    /// Refresh repository info in the status bar.
    /// </summary>
    public void RefreshRepoInfo()
    {
        try
        {
            var config = _gitHelper.ReadGitConfig();
            RepositoryPath = config.RepositoryDirectory ?? "(未配置)";
        }
        catch
        {
            RepositoryPath = "(配置读取失败)";
        }
    }
}
