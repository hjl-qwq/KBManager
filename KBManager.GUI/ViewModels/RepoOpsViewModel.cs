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
/// All Git operations run on background threads to avoid UI freeze.
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
        Console.SetOut(new LogWriter(this));
    }

    private class LogWriter : System.IO.TextWriter
    {
        private readonly RepoOpsViewModel _owner;
        private readonly StringBuilder _sb = new();
        public LogWriter(RepoOpsViewModel owner) => _owner = owner;
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            _sb.Append(value);
            if (value == '\n') { _owner.AppendLog(_sb.ToString()); _sb.Clear(); }
        }

        public override void WriteLine(string? value)
        {
            _owner.AppendLog((value ?? "") + Environment.NewLine);
        }
    }

    private void AppendLog(string text)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss");
        LogOutput += $"[{ts}] {text}";
        if (LogOutput.Length > 10000) LogOutput = LogOutput[^8000..];
        OnPropertyChanged(nameof(LogOutput));
    }

    [RelayCommand]
    private void ShowConfig()
    {
        var c = _gitHelper.ReadGitConfig();
        CurrentConfigDisplay =
            $"用户名:     {c.UserName ?? "(未设置)"}\n" +
            $"邮箱:       {c.UserEmail ?? "(未设置)"}\n" +
            $"HTTPS:      {c.RemoteAddressHttps ?? "(未设置)"}\n" +
            $"SSH:        {c.RemoteAddressSsh ?? "(未设置)"}\n" +
            $"本地目录:   {c.RepositoryDirectory ?? "(未设置)"}";
        AppendLog("配置信息已加载\n");
    }

    [RelayCommand]
    private async Task CloneRepositoryAsync()
    {
        var c = _gitHelper.ReadGitConfig();
        if (!c.ValidateCloneConfig()) { AppendLog("克隆失败: 配置不完整\n"); return; }

        SetBusy("克隆中..."); AppendLog("开始克隆仓库...\n");
        try
        {
            bool ok = await Task.Run(() => _gitHelper.CloneRepository(c));
            AppendLog(ok ? "克隆成功 ✓\n" : "克隆失败 ✗\n");
        }
        catch (Exception ex) { AppendLog($"克隆异常: {ex.Message}\n"); }
        ClearBusy(); _mainVm.RefreshRepoInfo();
    }

    [RelayCommand]
    private async Task MainAddAsync()
    {
        var c = _gitHelper.ReadGitConfig();
        SetBusy("暂存中..."); AppendLog("暂存主仓库变更...\n");
        try
        {
            bool ok = await Task.Run(() => _gitHelper.ExecuteGitAdd(c));
            AppendLog(ok ? "暂存成功 ✓\n" : "暂存失败 ✗\n");
        }
        catch (Exception ex) { AppendLog($"暂存异常: {ex.Message}\n"); }
        ClearBusy();
    }

    [RelayCommand]
    private async Task MainCommitAsync()
    {
        if (string.IsNullOrWhiteSpace(CommitMessage)) { AppendLog("提交失败: 请输入提交信息\n"); return; }

        var c = _gitHelper.ReadGitConfig();
        var cm = new GitCommitModel { CommitMessage = CommitMessage.Trim() };
        SetBusy("提交中..."); AppendLog($"提交: {cm.CommitMessage}\n");
        try
        {
            bool ok = await Task.Run(() => _gitHelper.ExecuteGitCommit(c, cm));
            if (ok) CommitMessage = string.Empty;
            AppendLog(ok ? "提交成功 ✓\n" : "提交失败 ✗\n");
        }
        catch (Exception ex) { AppendLog($"提交异常: {ex.Message}\n"); }
        ClearBusy();
    }

    [RelayCommand]
    private async Task SubmoduleAddAsync()
    {
        var c = _gitHelper.ReadGitConfig();
        SetBusy("暂存子模块..."); AppendLog("暂存子模块变更...\n");
        try
        {
            bool ok = await Task.Run(() => _gitHelper.ExecuteSubmoduleAdd(c));
            AppendLog(ok ? "子模块暂存成功 ✓\n" : "子模块暂存失败 ✗\n");
        }
        catch (Exception ex) { AppendLog($"子模块暂存异常: {ex.Message}\n"); }
        ClearBusy();
    }

    [RelayCommand]
    private async Task SubmoduleCommitAsync()
    {
        if (string.IsNullOrWhiteSpace(CommitMessage)) { AppendLog("提交失败: 请输入提交信息\n"); return; }

        var c = _gitHelper.ReadGitConfig();
        var cm = new GitCommitModel { CommitMessage = CommitMessage.Trim() };
        SetBusy("提交子模块..."); AppendLog($"子模块提交: {cm.CommitMessage}\n");
        try
        {
            bool ok = await Task.Run(() => _gitHelper.ExecuteSubmoduleCommit(c, cm));
            if (ok) CommitMessage = string.Empty;
            AppendLog(ok ? "子模块提交成功 ✓\n" : "子模块提交失败 ✗\n");
        }
        catch (Exception ex) { AppendLog($"子模块提交异常: {ex.Message}\n"); }
        ClearBusy();
    }

    [RelayCommand]
    private async Task PushAsync()
    {
        var c = _gitHelper.ReadGitConfig();
        if (string.IsNullOrWhiteSpace(c.RemoteAddressSsh)) { AppendLog("Push 失败: 未配置 SSH 地址\n"); return; }

        var confirmed = await _dialogService.ConfirmAsync("确认推送",
            $"将通过 SSH 推送到:\n{c.RemoteAddressSsh}\n\n确定要继续吗？");
        if (!confirmed) return;

        SetBusy("推送中..."); AppendLog("开始推送 (SSH)...\n");
        try
        {
            bool ok = await Task.Run(() => _gitHelper.ExecuteGitPush(c, string.Empty));
            AppendLog(ok ? "推送成功 ✓\n" : "推送失败 ✗\n");
        }
        catch (Exception ex) { AppendLog($"推送异常: {ex.Message}\n"); }
        ClearBusy();
    }

    [RelayCommand]
    private void Activate() => ShowConfig();
}
