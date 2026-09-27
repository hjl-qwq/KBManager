using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KBManager.core;
using KBManager.GUI.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KBManager.GUI.ViewModels;

/// <summary>Which panel the sidebar is currently showing.</summary>
public enum SidebarMode
{
    Explorer,
    Search
}

/// <summary>
/// The workspace shell: owns the activity bar, the sidebar panels, the open
/// document tabs, and the status bar. Also implements
/// <see cref="IWorkspaceShell"/> so sidebar panels can open documents without
/// depending on this class directly.
/// </summary>
public partial class MainViewModel : ViewModelBase, IWorkspaceShell
{
    private readonly IServiceProvider _services;
    private readonly GitHelper _gitHelper;
    private readonly IDialogService _dialogService;
    private readonly IKnowledgeBaseService _kbService;

    public MainViewModel(
        IServiceProvider services,
        GitHelper gitHelper,
        IDialogService dialogService,
        IKnowledgeBaseService kbService,
        ExplorerViewModel explorer,
        SearchViewModel search)
    {
        _services = services;
        _gitHelper = gitHelper;
        _dialogService = dialogService;
        _kbService = kbService;

        Explorer = explorer;
        Search = search;

        // Late-bound shell callbacks: avoids a DI cycle while keeping the panels
        // decoupled from the shell type.
        Explorer.Shell = this;
        Search.Shell = this;
    }

    public ExplorerViewModel Explorer { get; }

    public SearchViewModel Search { get; }

    /// <summary>Open documents, in tab order.</summary>
    public ObservableCollection<DocumentViewModel> Documents { get; } = new();

    // ── Shell state ────────────────────────────────────────────────────────

    [ObservableProperty]
    private DocumentViewModel? _activeDocument;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExplorerActive))]
    [NotifyPropertyChangedFor(nameof(IsSearchActive))]
    [NotifyPropertyChangedFor(nameof(SidebarContent))]
    private SidebarMode _sidebarMode = SidebarMode.Explorer;

    /// <summary>ViewModel for the sidebar panel currently shown.</summary>
    public ViewModelBase SidebarContent => SidebarMode == SidebarMode.Explorer ? Explorer : Search;

    public bool IsExplorerActive => SidebarMode == SidebarMode.Explorer;

    public bool IsSearchActive => SidebarMode == SidebarMode.Search;

    /// <summary>True when the settings tool page is the active document.</summary>
    public bool IsSettingsActive => ActiveDocument is ToolDocumentViewModel { Page: SettingsViewModel };

    /// <summary>True when the Git tool page is the active document.</summary>
    public bool IsGitActive => ActiveDocument is ToolDocumentViewModel { Page: RepoOpsViewModel };

    /// <summary>True when the log tool page is the active document.</summary>
    public bool IsLogActive => ActiveDocument is ToolDocumentViewModel { Page: LogViewModel };

    [ObservableProperty]
    private string _repositoryPath = "(未配置)";

    [ObservableProperty]
    private int _fileCount;

    [ObservableProperty]
    private int _tagCount;

    // ── Derived document state for the tab strip / status bar ──────────────

    public bool HasDocuments => Documents.Count > 0;

    public bool HasNoDocuments => Documents.Count == 0;

    public bool HasActiveDocument => ActiveDocument != null;

    public bool HasActiveFileDocument => ActiveDocument is FileDocumentViewModel;

    public FileDocumentViewModel? ActiveFile => ActiveDocument as FileDocumentViewModel;

    public bool CanSaveActiveDocument => ActiveDocument is { CanSave: true };

    public string ActiveDocumentLocation => ActiveDocument?.LocationLabel ?? string.Empty;

    partial void OnActiveDocumentChanged(DocumentViewModel? value)
    {
        foreach (var document in Documents)
            document.IsActive = ReferenceEquals(document, value);

        OnPropertyChanged(nameof(HasActiveDocument));
        OnPropertyChanged(nameof(HasActiveFileDocument));
        OnPropertyChanged(nameof(ActiveFile));
        OnPropertyChanged(nameof(CanSaveActiveDocument));
        OnPropertyChanged(nameof(ActiveDocumentLocation));
        OnPropertyChanged(nameof(IsSettingsActive));
        OnPropertyChanged(nameof(IsGitActive));
        OnPropertyChanged(nameof(IsLogActive));

        CloseDocumentCommand.NotifyCanExecuteChanged();
        SaveActiveDocumentCommand.NotifyCanExecuteChanged();

        // Whatever is in front of the user is also marked in the explorer, so an open
        // tab can always be traced back to its place in the tree.
        if (value is FileDocumentViewModel file)
            Explorer.RevealFile(file.RelativePath);
    }

    private void RaiseDocumentCollectionChanged()
    {
        OnPropertyChanged(nameof(HasDocuments));
        OnPropertyChanged(nameof(HasNoDocuments));
        CloseDocumentCommand.NotifyCanExecuteChanged();
        SaveActiveDocumentCommand.NotifyCanExecuteChanged();
    }

    // ── Startup ────────────────────────────────────────────────────────────

    /// <summary>Load the repository configuration, the file tree and the counters.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            var config = _gitHelper.ReadGitConfig();
            AppLog.Info($"仓库目录：{config.RepositoryDirectory ?? "(未配置)"}");
            AppLog.Info($"配置：user={config.UserName ?? "(空)"} email={config.UserEmail ?? "(空)"} " +
                        $"ssh={(string.IsNullOrWhiteSpace(config.RemoteAddressSsh) ? "(空)" : "已设置")} " +
                        $"https={(string.IsNullOrWhiteSpace(config.RemoteAddressHttps) ? "(空)" : "已设置")}");

            if (!string.IsNullOrWhiteSpace(config.RepositoryDirectory))
            {
                bool exists = System.IO.Directory.Exists(config.RepositoryDirectory);
                AppLog.Info($"仓库目录存在：{exists}");
                if (!exists)
                    AppLog.Warn("仓库目录不存在，资源管理器不会有内容。请在「设置」中修正路径。");
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("读取配置失败", ex);
        }

        RefreshRepoInfo();
        await Explorer.LoadFilesAsync();
        await UpdateCountersAsync();
        AppLog.Info($"索引就绪：文件 {FileCount} 个，标签 {TagCount} 个");
    }

    public void RefreshRepoInfo()
    {
        try
        {
            var config = _gitHelper.ReadGitConfig();
            RepositoryPath = string.IsNullOrWhiteSpace(config.RepositoryDirectory)
                ? "(未配置)"
                : config.RepositoryDirectory;
        }
        catch
        {
            RepositoryPath = "(配置读取失败)";
        }
    }

    /// <summary>Re-read configuration, tree and counters (after a settings save).</summary>
    public async Task ReloadWorkspaceAsync()
    {
        RefreshRepoInfo();
        await Explorer.LoadFilesAsync();
        await UpdateCountersAsync();
        Search.Invalidate();
    }

    private async Task UpdateCountersAsync()
    {
        var repository = _gitHelper.ReadGitConfig().RepositoryDirectory;
        if (string.IsNullOrWhiteSpace(repository))
        {
            FileCount = 0;
            TagCount = 0;
            return;
        }

        try
        {
            // The file count shown to the user must match the explorer, which is
            // driven by the real directory rather than by the index.
            FileCount = Explorer.TotalFileCount;

            var tags = await _kbService.GetTagsWithFileCountAsync(repository);
            TagCount = tags.Success && tags.Data != null ? tags.Data.Count : 0;
        }
        catch
        {
            FileCount = Explorer.TotalFileCount;
            TagCount = 0;
        }
    }

    // ── Sidebar / activity bar ─────────────────────────────────────────────

    [RelayCommand]
    private void ShowExplorer() => SidebarMode = SidebarMode.Explorer;

    [RelayCommand]
    private async Task ShowSearchAsync()
    {
        SidebarMode = SidebarMode.Search;
        // With an empty query this lists every tag, so refresh it on entry.
        Search.Invalidate();
        await Search.LoadSuggestionsCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var existing = Documents.FirstOrDefault(d => d is ToolDocumentViewModel { Page: SettingsViewModel });
        if (existing != null)
        {
            Activate(existing);
            return;
        }

        var settings = _services.GetRequiredService<SettingsViewModel>();
        settings.Load();
        AddDocument(new ToolDocumentViewModel("设置", settings));
    }

    [RelayCommand]
    private void OpenGit()
    {
        var existing = Documents.FirstOrDefault(d => d is ToolDocumentViewModel { Page: RepoOpsViewModel });
        if (existing != null)
        {
            Activate(existing);
            return;
        }

        var git = _services.GetRequiredService<RepoOpsViewModel>();
        git.Activate();
        AddDocument(new ToolDocumentViewModel("Git 操作", git));
    }

    [RelayCommand]
    private void OpenLog()
    {
        var existing = Documents.FirstOrDefault(d => d is ToolDocumentViewModel { Page: LogViewModel });
        if (existing != null)
        {
            // Refresh on focus so it always shows the latest activity.
            if (existing is ToolDocumentViewModel { Page: LogViewModel logVm }) logVm.Refresh();
            Activate(existing);
            return;
        }

        var log = _services.GetRequiredService<LogViewModel>();
        AddDocument(new ToolDocumentViewModel("日志", log));
    }

    /// <summary>
    /// Open the stale-record cleanup page as a document tab. A full list belongs in
    /// the editor area, not squeezed into the explorer sidebar.
    /// </summary>
    [RelayCommand]
    public async Task OpenStaleRecordsAsync()
    {
        var existing = Documents.FirstOrDefault(d => d is ToolDocumentViewModel { Page: StaleRecordsViewModel });
        if (existing is ToolDocumentViewModel { Page: StaleRecordsViewModel staleVm })
        {
            Activate(existing);
            await staleVm.LoadAsync();      // always show the current state
            return;
        }

        var page = _services.GetRequiredService<StaleRecordsViewModel>();
        page.IndexChanged = () => RefreshIndexAsync(rebuildTree: true);

        await page.LoadAsync();
        AddDocument(new ToolDocumentViewModel("清理失效记录", page));
    }

    // ── Document management ────────────────────────────────────────────────

    private void AddDocument(DocumentViewModel document)
    {
        document.ActivateRequested = Activate;
        document.CloseRequested = doc => _ = CloseDocumentAsync(doc);

        Documents.Add(document);
        RaiseDocumentCollectionChanged();
        ActiveDocument = document;
    }

    private void Activate(DocumentViewModel document) => ActiveDocument = document;

    [RelayCommand(CanExecute = nameof(HasActiveDocument))]
    private async Task CloseDocumentAsync(DocumentViewModel? document)
    {
        document ??= ActiveDocument;
        if (document == null) return;

        if (document.IsDirty)
        {
            var choice = await _dialogService.ConfirmUnsavedChangesAsync(document.Title);
            switch (choice)
            {
                case UnsavedChangesChoice.Cancel:
                    return;
                case UnsavedChangesChoice.Save:
                    if (!await document.SaveAsync())
                    {
                        StatusMessage = $"保存失败，已取消关闭: {document.Title}";
                        return;
                    }
                    break;
            }
        }

        RemoveDocument(document);
    }

    private void RemoveDocument(DocumentViewModel document)
    {
        int index = Documents.IndexOf(document);
        if (index < 0) return;

        bool wasActive = ReferenceEquals(ActiveDocument, document);
        Documents.RemoveAt(index);
        RaiseDocumentCollectionChanged();

        if (!wasActive) return;

        // Prefer the tab that took its place, else the one before it.
        if (Documents.Count == 0) ActiveDocument = null;
        else ActiveDocument = Documents[Math.Min(index, Documents.Count - 1)];
    }

    [RelayCommand(CanExecute = nameof(CanSaveActiveDocument))]
    private async Task SaveActiveDocumentAsync()
    {
        var document = ActiveDocument;
        if (document == null) return;

        var saved = await document.SaveAsync();
        StatusMessage = saved ? $"已保存: {document.Title}" : $"保存失败: {document.Title}";

        if (saved) await OnTagsMaybeChangedAsync();
    }

    /// <summary>Reload the active document from disk, discarding buffer edits.</summary>
    [RelayCommand]
    private async Task ReloadActiveDocumentAsync()
    {
        if (ActiveDocument is not FileDocumentViewModel file) return;

        // Delegate so the unsaved-changes prompt lives in one place.
        await file.ReloadCommand.ExecuteAsync(null);
        StatusMessage = $"重新加载: {file.Title}";
    }

    private async Task OnTagsMaybeChangedAsync()
    {
        // Rebuild rather than only refreshing badges: a file can become indexed by
        // being opened and saved, in which case it is not in the tree yet.
        await RefreshIndexAsync(rebuildTree: true);
    }

    // ── IWorkspaceShell ────────────────────────────────────────────────────

    /// <summary>
    /// Open a file in the editor area.
    ///
    /// <paramref name="preview"/> is the single-click behaviour: the file opens into
    /// one provisional tab (italic title) that the next single click replaces, so
    /// browsing the tree never piles up tabs. Opening a file for real reuses or
    /// promotes that tab rather than showing the same file twice.
    /// </summary>
    public async Task OpenFileAsync(string relativePath, bool preview)
    {
        var normalized = relativePath.Replace('\\', '/');

        try
        {
            // Already open: bring it forward. Opening "for real" also settles a
            // provisional tab — that is the double click on a file already previewed.
            var existing = Documents
                .OfType<FileDocumentViewModel>()
                .FirstOrDefault(d => string.Equals(d.RelativePath, normalized, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                if (!preview) existing.PromoteToPermanent();
                Activate(existing);
                Explorer.RevealFile(existing.RelativePath);
                StatusMessage = $"已切换到: {existing.Title}";
                return;
            }

            AppLog.Info(preview ? $"预览文件：{normalized}" : $"打开文件：{normalized}");

            // Reuse the provisional tab if there is one. A tab that is dirty or still
            // loading is never reused: the first has been edited (an edit promotes it
            // anyway), and the second is already reading a file this click would
            // silently redirect.
            var recycled = preview
                ? Documents.OfType<FileDocumentViewModel>()
                    .FirstOrDefault(d => d.IsPreview && !d.IsDirty && !d.IsLoading)
                : null;

            var document = recycled ?? _services.GetRequiredService<FileDocumentViewModel>();

            if (recycled == null)
            {
                document.Initialize(normalized);
                document.IsPreview = preview;
                document.TagsChanged += OnTagsMaybeChangedAsync;

                // Registered before the read so the tab is visible while it loads: a
                // second click on the same file then finds it and promotes it instead
                // of opening a second tab for one file.
                AddDocument(document);

                if (!await document.LoadAsync())
                {
                    RemoveDocument(document);
                    await ReportOpenFailureAsync(document, normalized);
                    return;
                }
            }
            else
            {
                // Bring the provisional tab forward while it loads. ReloadAsAsync only
                // commits the new path once the read succeeded, so a failure cannot
                // leave the buffer holding one file's text under another file's path.
                Activate(document);

                if (!await document.ReloadAsAsync(normalized))
                {
                    await ReportOpenFailureAsync(document, normalized);
                    return;
                }
            }

            Explorer.RevealFile(normalized);
            StatusMessage = preview
                ? $"预览: {normalized}（双击可在标签页中固定打开）"
                : $"已打开: {normalized}";
            AppLog.Info($"打开成功：{normalized}（{document.LineCount} 行 / {document.CharacterCount} 字符）");
        }
        catch (Exception ex)
        {
            // Never fail silently: an unexpected error must leave a trace and be visible.
            AppLog.Error($"打开文件时发生异常：{normalized}", ex);
            StatusMessage = $"打开失败: {normalized}（详情见日志）";
            await _dialogService.ShowErrorAsync(
                "打开失败",
                $"打开 {normalized} 时发生未预期的错误：\n{ex.GetType().Name}: {ex.Message}" +
                BuildLogHint());
        }
    }

    private async Task ReportOpenFailureAsync(FileDocumentViewModel document, string normalized)
    {
        StatusMessage = $"打开失败: {normalized}（详情见日志）";
        AppLog.Error($"打开文件失败：{normalized}");
        await _dialogService.ShowErrorAsync("打开失败", document.InfoMessage + BuildLogHint());
    }

    /// <summary>Point the user at the log file whenever something fails.</summary>
    private static string BuildLogHint() =>
        $"\n\n———\n详细信息已写入日志：\n{AppLog.LogFilePath}";

    public async Task OnFileRenamedAsync(string oldRelativePath, string newRelativePath)
    {
        var normalizedOld = oldRelativePath.Replace('\\', '/');

        foreach (var document in Documents.OfType<FileDocumentViewModel>())
        {
            if (string.Equals(document.RelativePath, normalizedOld, StringComparison.OrdinalIgnoreCase))
                document.MoveTo(newRelativePath);
        }

        await OnTagsMaybeChangedAsync();
    }

    public Task OnFileDeletedAsync(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');

        foreach (var document in Documents.OfType<FileDocumentViewModel>().ToList())
        {
            if (string.Equals(document.RelativePath, normalized, StringComparison.OrdinalIgnoreCase))
                RemoveDocument(document);
        }

        return Task.CompletedTask;
    }

    public async Task RefreshIndexAsync(bool rebuildTree)
    {
        if (rebuildTree) await Explorer.LoadFilesAsync();
        else await Explorer.RefreshTagsAsync();

        await UpdateCountersAsync();
        Search.Invalidate();
    }

    public void ReportStatus(string message) => StatusMessage = message;
}
