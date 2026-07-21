using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KBManager.core;
using KBManager.GUI.Services;
using System;
using System.Text;
using System.Threading.Tasks;

namespace KBManager.GUI.ViewModels;

/// <summary>
/// ViewModel for Git Repository Operations.
/// </summary>
public partial class RepoOpsViewModel : ViewModelBase
{
    private readonly GitHelper _gitHelper;
    private readonly IDialogService _dialogService;
    private readonly MainViewModel _mainVm;

    [ObservableProperty]
    private string _logOutput = string.Empty;

    [ObservableProperty]
    private string _commitMessage = string.Empty;

    [ObservableProperty]
    private string _currentConfigDisplay = string.Empty;

    public RepoOpsViewModel(GitHelper gitHelper, IDialogService dialogService, MainViewModel mainVm)
    {
        _gitHelper = gitHelper;
        _dialogService = dialogService;
        _mainVm = mainVm;

        // Redirect Console output for GitHelper to our log
        Console.SetOut(new LogWriter(this));
    }

    /// <summary>
    /// Custom TextWriter that redirects Console output to LogOutput.
    /// </summary>
    private class LogWriter : System.IO.TextWriter
    {
        private readonly RepoOpsViewModel _owner;
        private readonly StringBuilder _sb = new();

        public LogWriter(RepoOpsViewModel owner)
        {
            _owner = owner;
        }

        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

        public override void Write(char value)
        {
            _sb.Append(value);
            if (value == '\n')
            {
                _owner.AppendLog(_sb.ToString());
                _sb.Clear();
            }
        }

        public override void WriteLine(string? value)
        {
            _owner.AppendLog((value ?? "") + Environment.NewLine);
        }
    }

    private void AppendLog(string text)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogOutput += $"[{timestamp}] {text}";
        // Keep log manageable
        if (LogOutput.Length > 10000)
        {
            LogOutput = LogOutput[^8000..];
        }
        OnPropertyChanged(nameof(LogOutput));
    }

    /// <summary>
    /// Load config and show it.
    /// </summary>
    [RelayCommand]
    private void ShowConfig()
    {
        var config = _gitHelper.ReadGitConfig();
        CurrentConfigDisplay =
            $"用户名:     {config.UserName ?? "(未设置)"}\n" +
            $"邮箱:       {config.UserEmail ?? "(未设置)"}\n" +
            $"HTTPS:      {config.RemoteAddressHttps ?? "(未设置)"}\n" +
            $"SSH:        {config.RemoteAddressSsh ?? "(未设置)"}\n" +
            $"本地目录:   {config.RepositoryDirectory ?? "(未设置)"}";

        AppendLog("配置信息已加载\n");
    }

    [RelayCommand]
    private void CloneRepository()
    {
        var config = _gitHelper.ReadGitConfig();
        if (!config.ValidateCloneConfig())
        {
            AppendLog("克隆失败: 配置不完整\n");
            return;
        }

        SetBusy("克隆中...");
        AppendLog("开始克隆仓库...\n");
        bool ok = _gitHelper.CloneRepository(config);
        AppendLog(ok ? "克隆成功 ✓\n" : "克隆失败 ✗\n");
        ClearBusy();
        _mainVm.RefreshRepoInfo();
    }

    [RelayCommand]
    private void MainAdd()
    {
        var config = _gitHelper.ReadGitConfig();
        SetBusy("暂存中...");
        AppendLog("暂存主仓库变更...\n");
        bool ok = _gitHelper.ExecuteGitAdd(config);
        AppendLog(ok ? "暂存成功 ✓\n" : "暂存失败 ✗\n");
        ClearBusy();
    }

    [RelayCommand]
    private void MainCommit()
    {
        if (string.IsNullOrWhiteSpace(CommitMessage))
        {
            AppendLog("提交失败: 请输入提交信息\n");
            return;
        }

        var config = _gitHelper.ReadGitConfig();
        var commit = new GitCommitModel { CommitMessage = CommitMessage.Trim() };

        SetBusy("提交中...");
        AppendLog($"提交: {commit.CommitMessage}\n");
        bool ok = _gitHelper.ExecuteGitCommit(config, commit);
        if (ok)
        {
            CommitMessage = string.Empty;
        }
        AppendLog(ok ? "提交成功 ✓\n" : "提交失败 ✗\n");
        ClearBusy();
    }

    [RelayCommand]
    private void SubmoduleAdd()
    {
        var config = _gitHelper.ReadGitConfig();
        SetBusy("暂存子模块...");
        AppendLog("暂存子模块变更...\n");
        bool ok = _gitHelper.ExecuteSubmoduleAdd(config);
        AppendLog(ok ? "子模块暂存成功 ✓\n" : "子模块暂存失败 ✗\n");
        ClearBusy();
    }

    [RelayCommand]
    private void SubmoduleCommit()
    {
        if (string.IsNullOrWhiteSpace(CommitMessage))
        {
            AppendLog("提交失败: 请输入提交信息\n");
            return;
        }

        var config = _gitHelper.ReadGitConfig();
        var commit = new GitCommitModel { CommitMessage = CommitMessage.Trim() };

        SetBusy("提交子模块...");
        AppendLog($"子模块提交: {commit.CommitMessage}\n");
        bool ok = _gitHelper.ExecuteSubmoduleCommit(config, commit);
        if (ok) CommitMessage = string.Empty;
        AppendLog(ok ? "子模块提交成功 ✓\n" : "子模块提交失败 ✗\n");
        ClearBusy();
    }

    [RelayCommand]
    private async Task PushAsync()
    {
        var config = _gitHelper.ReadGitConfig();

        if (string.IsNullOrWhiteSpace(config.RemoteAddressSsh))
        {
            AppendLog("Push 失败: 未配置 SSH 地址\n");
            return;
        }

        var confirmed = await _dialogService.ConfirmAsync(
            "确认推送",
            $"将通过 SSH 推送到:\n{config.RemoteAddressSsh}\n\n确定要继续吗？");

        if (!confirmed) return;

        SetBusy("推送中...");
        AppendLog("开始推送 (SSH)...\n");
        bool ok = _gitHelper.ExecuteGitPush(config);
        AppendLog(ok ? "推送成功 ✓\n" : "推送失败 ✗\n");
        ClearBusy();
    }

    /// <summary>
    /// Called when the page becomes active.
    /// </summary>
    [RelayCommand]
    private void Activate()
    {
        ShowConfig();
    }
}
