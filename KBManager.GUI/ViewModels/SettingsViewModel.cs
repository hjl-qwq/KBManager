using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KBManager.core;
using KBManager.GUI.Services;
using System.Threading.Tasks;

namespace KBManager.GUI.ViewModels;

/// <summary>
/// ViewModel for the Settings page.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly GitHelper _gitHelper;
    private readonly IDialogService _dialogService;
    private readonly MainViewModel _mainVm;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _userEmail = string.Empty;

    [ObservableProperty]
    private string _remoteHttps = string.Empty;

    [ObservableProperty]
    private string _remoteSsh = string.Empty;

    [ObservableProperty]
    private string _repositoryDirectory = string.Empty;

    [ObservableProperty]
    private bool _isLoaded;

    public SettingsViewModel(GitHelper gitHelper, IDialogService dialogService, MainViewModel mainVm)
    {
        _gitHelper = gitHelper;
        _dialogService = dialogService;
        _mainVm = mainVm;
    }

    /// <summary>
    /// Load current config into the form.
    /// </summary>
    [RelayCommand]
    private void LoadConfig()
    {
        var config = _gitHelper.ReadGitConfig();
        UserName = config.UserName ?? string.Empty;
        UserEmail = config.UserEmail ?? string.Empty;
        RemoteHttps = config.RemoteAddressHttps ?? string.Empty;
        RemoteSsh = config.RemoteAddressSsh ?? string.Empty;
        RepositoryDirectory = config.RepositoryDirectory ?? string.Empty;
        IsLoaded = true;

        StatusMessage = "配置已加载";
    }

    /// <summary>
    /// Save config from form fields.
    /// </summary>
    [RelayCommand]
    private async Task SaveConfigAsync()
    {
        if (string.IsNullOrWhiteSpace(RepositoryDirectory))
        {
            await _dialogService.ShowErrorAsync("验证失败", "仓库目录不能为空");
            return;
        }

        var config = new GitConfigModel
        {
            UserName = UserName?.Trim() ?? string.Empty,
            UserEmail = UserEmail?.Trim() ?? string.Empty,
            RemoteAddressHttps = RemoteHttps?.Trim() ?? string.Empty,
            RemoteAddressSsh = RemoteSsh?.Trim() ?? string.Empty,
            RepositoryDirectory = RepositoryDirectory?.Trim() ?? string.Empty
        };

        SetBusy("保存中...");
        var ok = _gitHelper.SaveGitConfig(config);
        ClearBusy();

        if (ok)
        {
            StatusMessage = "配置保存成功 ✓";
            _mainVm.RefreshRepoInfo();
            await _dialogService.ShowInfoAsync("成功", "配置已保存。");
        }
        else
        {
            StatusMessage = "配置保存失败 ✗";
            await _dialogService.ShowErrorAsync("失败", "无法保存配置，请检查权限。");
        }
    }

    /// <summary>
    /// Browse for repository directory.
    /// </summary>
    [RelayCommand]
    private async Task BrowseDirectoryAsync()
    {
        var path = await _dialogService.PickFolderAsync("选择仓库目录");
        if (!string.IsNullOrWhiteSpace(path))
        {
            RepositoryDirectory = path;
        }
    }

    /// <summary>
    /// Called when the page becomes active.
    /// </summary>
    [RelayCommand]
    private void Activate()
    {
        LoadConfig();
    }
}
