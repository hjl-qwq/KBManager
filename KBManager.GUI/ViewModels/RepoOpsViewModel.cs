using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KBManager.core;
using KBManager.GUI.Services;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace KBManager.GUI.ViewModels;

/// <summary>
/// ViewModel for Git repository operations.
///
/// All Git work runs on background threads. Progress output is captured through
/// <see cref="GitHelper.LogSink"/> instead of redirecting <c>Console.Out</c> for
/// the whole process, and appends are marshalled back to the UI thread because
/// <see cref="GitHelper"/> logs from whichever thread performs the operation.
/// </summary>
public partial class RepoOpsViewModel : ViewModelBase
{
    private readonly GitHelper _gitHelper;
    private readonly IDialogService _dialogService;
    private readonly MainViewModel _mainVm;
    private readonly UiThreadLogSink _logSink;

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

        _logSink = new UiThreadLogSink(AppendLog);

        // Never block a GUI operation on a console prompt, and route Git output here.
        _gitHelper.Interactive = false;
        _gitHelper.LogSink = _logSink;
    }

    /// <summary>
    /// Forwards Git log lines to the panel, coalescing them onto the UI thread.
    /// </summary>
    private sealed class UiThreadLogSink : TextWriter
    {
        private readonly Action<string> _append;

        public UiThreadLogSink(Action<string> append) => _append = append;

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => Write(value.ToString());

        public override void Write(string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            Dispatch(value);
        }

        public override void WriteLine(string? value)
        {
            if (value == null) return;
            Dispatch(value);
        }

        private void Dispatch(string text)
        {
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                _append(text);
                return;
            }
            Avalonia.Threading.Dispatcher.UIThread.Post(() => _append(text));
        }
    }

    private void AppendLog(string text)
    {
        LogOutput += $"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}";
        if (LogOutput.Length > 12000) LogOutput = LogOutput[^9000..];

        // Mirror into the persistent log so Git failures are diagnosable later.
        AppLog.Info($"Git: {text}");
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
        AppendLog("配置信息已加载");
    }

    [RelayCommand]
    private async Task CloneRepositoryAsync()
    {
        var c = _gitHelper.ReadGitConfig();
        if (!c.ValidateCloneConfig()) { AppendLog("克隆失败: 配置不完整"); return; }

        SetBusy("克隆中..."); AppendLog("开始克隆仓库...");
        try
        {
            bool ok = await Task.Run(() => _gitHelper.CloneRepository(c));
            AppendLog(ok ? "克隆成功 ✓" : "克隆失败 ✗");
        }
        catch (Exception ex) { AppendLog($"克隆异常: {ex.Message}"); }
        ClearBusy();
        await _mainVm.ReloadWorkspaceAsync();
    }

    [RelayCommand]
    private async Task MainAddAsync()
    {
        var c = _gitHelper.ReadGitConfig();
        SetBusy("暂存中..."); AppendLog("暂存主仓库变更...");
        try
        {
            bool ok = await Task.Run(() => _gitHelper.ExecuteGitAdd(c));
            AppendLog(ok ? "暂存成功 ✓" : "暂存失败 ✗");
        }
        catch (Exception ex) { AppendLog($"暂存异常: {ex.Message}"); }
        ClearBusy();
    }

    [RelayCommand]
    private async Task MainCommitAsync()
    {
        if (string.IsNullOrWhiteSpace(CommitMessage)) { AppendLog("提交失败: 请输入提交信息"); return; }

        var c = _gitHelper.ReadGitConfig();
        var cm = new GitCommitModel { CommitMessage = CommitMessage.Trim() };
        SetBusy("提交中..."); AppendLog($"提交: {cm.CommitMessage}");
        try
        {
            bool ok = await Task.Run(() => _gitHelper.ExecuteGitCommit(c, cm));
            if (ok) CommitMessage = string.Empty;
            AppendLog(ok ? "提交成功 ✓" : "提交失败 ✗");
        }
        catch (Exception ex) { AppendLog($"提交异常: {ex.Message}"); }
        ClearBusy();
    }

    [RelayCommand]
    private async Task SubmoduleAddAsync()
    {
        var c = _gitHelper.ReadGitConfig();
        SetBusy("暂存子模块..."); AppendLog("暂存子模块变更...");
        try
        {
            bool ok = await Task.Run(() => _gitHelper.ExecuteSubmoduleAdd(c));
            AppendLog(ok ? "子模块暂存成功 ✓" : "子模块暂存失败 ✗");
        }
        catch (Exception ex) { AppendLog($"子模块暂存异常: {ex.Message}"); }
        ClearBusy();
    }

    [RelayCommand]
    private async Task SubmoduleCommitAsync()
    {
        if (string.IsNullOrWhiteSpace(CommitMessage)) { AppendLog("提交失败: 请输入提交信息"); return; }

        var c = _gitHelper.ReadGitConfig();
        var cm = new GitCommitModel { CommitMessage = CommitMessage.Trim() };
        SetBusy("提交子模块..."); AppendLog($"子模块提交: {cm.CommitMessage}");
        try
        {
            bool ok = await Task.Run(() => _gitHelper.ExecuteSubmoduleCommit(c, cm));
            if (ok) CommitMessage = string.Empty;
            AppendLog(ok ? "子模块提交成功 ✓" : "子模块提交失败 ✗");
        }
        catch (Exception ex) { AppendLog($"子模块提交异常: {ex.Message}"); }
        ClearBusy();
    }

    [RelayCommand]
    private async Task PushAsync()
    {
        var c = _gitHelper.ReadGitConfig();
        if (string.IsNullOrWhiteSpace(c.RemoteAddressSsh)) { AppendLog("Push 失败: 未配置 SSH 地址"); return; }

        var confirmed = await _dialogService.ConfirmAsync("确认推送",
            $"将通过 SSH 推送到:\n{c.RemoteAddressSsh}\n\n确定要继续吗？");
        if (!confirmed) return;

        SetBusy("推送中..."); AppendLog("开始推送 (SSH)...");
        try
        {
            bool ok = await Task.Run(() => _gitHelper.ExecuteGitPush(c, string.Empty));
            AppendLog(ok ? "推送成功 ✓" : "推送失败 ✗");
        }
        catch (Exception ex) { AppendLog($"推送异常: {ex.Message}"); }
        ClearBusy();
    }

    /// <summary>Called by the shell when the Git tab is opened.</summary>
    public void Activate() => ShowConfig();
}
