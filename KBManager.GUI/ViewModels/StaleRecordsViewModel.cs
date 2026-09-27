using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KBManager.core;
using KBManager.GUI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KBManager.GUI.ViewModels;

/// <summary>
/// The stale-index cleanup page, shown as a document tab in the editor area.
///
/// The sidebar only shows a count plus a button that opens this page: a long list
/// does not fit in the explorer and would crowd out the file tree.
/// </summary>
public partial class StaleRecordsViewModel : ViewModelBase
{
    private readonly IKnowledgeBaseService _kbService;
    private readonly IFileScanService _fileScanService;
    private readonly GitHelper _gitHelper;
    private readonly IDialogService _dialogService;

    /// <summary>Wired by the shell so a cleanup can refresh the explorer and counters.</summary>
    public Func<Task>? IndexChanged { get; set; }

    public StaleRecordsViewModel(
        IKnowledgeBaseService kbService,
        IFileScanService fileScanService,
        GitHelper gitHelper,
        IDialogService dialogService)
    {
        _kbService = kbService;
        _fileScanService = fileScanService;
        _gitHelper = gitHelper;
        _dialogService = dialogService;
    }

    /// <summary>Every stale path, in full — the point of this page.</summary>
    public ObservableCollection<string> Records { get; } = new();

    [ObservableProperty]
    private string _repositoryPath = "(未配置)";

    [ObservableProperty]
    private bool _isRepositoryConfigured;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private bool _hasRecords;

    [ObservableProperty]
    private bool _hasNoRecords = true;

    /// <summary>Label of the decision button at the bottom, with the live count.</summary>
    public string CleanButtonText =>
        Records.Count == 0 ? "没有需要清理的记录" : $"清理这 {Records.Count} 条记录";

    /// <summary>Re-scan the repository and rebuild the list.</summary>
    public async Task LoadAsync()
    {
        var repository = _gitHelper.ReadGitConfig().RepositoryDirectory;
        if (string.IsNullOrWhiteSpace(repository))
        {
            IsRepositoryConfigured = false;
            Records.Clear();
            Summary = "尚未配置仓库目录。请先在左下角「设置」中填写本地知识库路径。";
            RefreshDerived();
            return;
        }

        IsRepositoryConfigured = true;
        RepositoryPath = repository;
        SetBusy("正在检查索引与磁盘…");
        try
        {
            // Scanning is blocking I/O; keep it off the UI thread.
            var scan = await Task.Run(() => _fileScanService.ScanRepositoryFiles(repository));
            var disk = scan.Success && scan.Data != null ? scan.Data : new List<string>();
            if (!scan.Success)
                AppLog.Warn($"清理页：扫描仓库失败 — {scan.Message}");

            var stale = await _kbService.FindStaleRecordsAsync(repository, disk);
            var list = stale.Success && stale.Data != null ? stale.Data : new List<string>();
            if (!stale.Success)
                AppLog.Warn($"清理页：对账失败 — {stale.Message}");

            Records.Clear();
            foreach (var path in list)
                Records.Add(path);

            Summary = list.Count == 0
                ? "索引与磁盘一致，没有失效记录。"
                : $"{list.Count} 条索引记录对应的文件已不在磁盘上：";
            AppLog.Info($"清理页：发现 {list.Count} 条失效记录");
            ClearBusy(Summary);
        }
        catch (Exception ex)
        {
            ClearBusy();
            Summary = $"检查失败: {ex.Message}";
            AppLog.Error("清理页：检查失效记录异常", ex);
        }
        finally
        {
            RefreshDerived();
        }
    }

    private void RefreshDerived()
    {
        HasRecords = Records.Count > 0;
        HasNoRecords = !HasRecords;
        OnPropertyChanged(nameof(CleanButtonText));
    }

    [RelayCommand]
    private async Task ReloadAsync() => await LoadAsync();

    /// <summary>
    /// The decision button: confirm, then drop every listed index record (and the
    /// tag links that only it used). Disk files are never touched.
    /// </summary>
    [RelayCommand]
    private async Task CleanAsync()
    {
        if (Records.Count == 0) return;

        var repository = _gitHelper.ReadGitConfig().RepositoryDirectory;
        if (string.IsNullOrWhiteSpace(repository)) return;

        var paths = Records.ToList();
        var confirmed = await _dialogService.ConfirmAsync(
            "清理失效记录",
            $"将移除 {paths.Count} 条在磁盘上已不存在的索引记录，以及它们关联的标签。\n\n" +
            "磁盘上的文件不会被改动。上方列表就是要移除的全部条目。\n\n确定继续吗？");
        if (!confirmed) return;

        SetBusy("正在清理失效记录…");
        try
        {
            int removed = 0;
            foreach (var path in paths)
            {
                var result = await _kbService.DeleteFileAsync(repository, path);
                if (result.Success) removed++;
                else AppLog.Warn($"清理失效记录失败：{path} — {result.Message}");
            }

            AppLog.Info($"已清理 {removed}/{paths.Count} 条失效索引记录");
            StatusMessage = $"已清理 {removed} 条失效索引记录";

            await LoadAsync();
            if (IndexChanged != null) await IndexChanged();
        }
        finally
        {
            ClearBusy();
        }
    }
}
