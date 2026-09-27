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
/// Callbacks the workspace shell exposes to sidebar panels, so the explorer and
/// search panels can open documents without holding a reference to the shell.
/// </summary>
public interface IWorkspaceShell
{
    /// <summary>
    /// Open a repository-relative file in the editor area. With
    /// <paramref name="preview"/> the file opens into a provisional tab (a single
    /// click in the tree), which the next preview replaces; otherwise it is opened
    /// for real and any provisional tab for it becomes permanent.
    /// </summary>
    Task OpenFileAsync(string relativePath, bool preview);

    /// <summary>Reflect a rename in any open document and in the status counters.</summary>
    Task OnFileRenamedAsync(string oldRelativePath, string newRelativePath);

    /// <summary>Close any document that points at a deleted file.</summary>
    Task OnFileDeletedAsync(string relativePath);

    /// <summary>Re-read the index; optionally rebuild the explorer tree.</summary>
    Task RefreshIndexAsync(bool rebuildTree);

    /// <summary>Open (or focus) the stale-record cleanup page in the editor area.</summary>
    Task OpenStaleRecordsAsync();

    /// <summary>Publish a message to the workspace status bar.</summary>
    void ReportStatus(string message);
}

/// <summary>
/// The explorer sidebar: an indexed Markdown file tree with per-file tag
/// preview on hover, plus create / rename / delete / sync operations.
///
/// Shortcuts act on a specific node (passed by the context menu) rather than on
/// an implicit selection, so right-clicking a row always affects that row.
/// </summary>
public partial class ExplorerViewModel : ViewModelBase
{
    private static readonly HashSet<string> MarkdownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".mdown", ".mkd", ".mkdn", ".mdwn"
    };

    private readonly IKnowledgeBaseService _kbService;
    private readonly IFileScanService _fileScanService;
    private readonly IFileContentService _fileContentService;
    private readonly GitHelper _gitHelper;
    private readonly IDialogService _dialogService;
    private readonly IFileOpener _fileOpener;

    private List<FileEntryDto> _fileEntries = new();
    private bool _hasBuiltTreeOnce;

    /// <summary>Set by the shell during composition.</summary>
    public IWorkspaceShell? Shell { get; set; }

    public ExplorerViewModel(
        IKnowledgeBaseService kbService,
        IFileScanService fileScanService,
        IFileContentService fileContentService,
        GitHelper gitHelper,
        IDialogService dialogService,
        IFileOpener fileOpener)
    {
        _kbService = kbService;
        _fileScanService = fileScanService;
        _fileContentService = fileContentService;
        _gitHelper = gitHelper;
        _dialogService = dialogService;
        _fileOpener = fileOpener;
    }

    // ── State ──────────────────────────────────────────────────────────────

    [ObservableProperty]
    private ObservableCollection<FileTreeNode> _fileTree = new();

    [ObservableProperty]
    private FileTreeNode? _selectedNode;

    /// <summary>Filters the tree by path substring, like VS Code's file filter.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFilter))]
    private string _filterText = string.Empty;

    /// <summary>Whether a tree filter is active (shows the clear button).</summary>
    public bool HasFilter => !string.IsNullOrWhiteSpace(FilterText);

    [ObservableProperty]
    private string _fileSummary = "共 0 个文件";

    /// <summary>Markdown files actually present on disk (the status bar uses this).</summary>
    [ObservableProperty]
    private int _totalFileCount;

    /// <summary>Index records whose file no longer exists on disk.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStaleIndexRecords))]
    [NotifyPropertyChangedFor(nameof(StaleSummary))]
    private int _staleIndexCount;

    /// <summary>Whether to surface the "stale records" hint at all.</summary>
    public bool HasStaleIndexRecords => StaleIndexCount > 0;

    /// <summary>
    /// One line only: the full list lives on its own page in the editor area
    /// (opened by the button next to this text), so a long list can never crowd
    /// out the file tree.
    /// </summary>
    public string StaleSummary => $"索引里有 {StaleIndexCount} 条记录，其文件已不在磁盘上";

    [ObservableProperty]
    private bool _isRepositoryConfigured;

    // ── Load / build ───────────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadFilesAsync()
    {
        var repository = _gitHelper.ReadGitConfig().RepositoryDirectory;
        if (string.IsNullOrWhiteSpace(repository))
        {
            IsRepositoryConfigured = false;
            FileSummary = "未配置仓库";
            StatusMessage = "请先在设置中配置仓库目录";
            FileTree = new ObservableCollection<FileTreeNode>();
            TotalFileCount = 0;
            StaleIndexCount = 0;
            AppLog.Warn("资源管理器：尚未配置仓库目录");
            return;
        }

        IsRepositoryConfigured = true;
        SetBusy("正在读取仓库目录…");
        try
        {
            // First run against a configured repository: build the index so tagging
            // works, instead of showing an explorer the user cannot act on.
            if (!_kbService.DatabaseExists(repository))
            {
                AppLog.Info("索引不存在，自动创建并执行首次扫描");
                SetBusy("首次运行：正在创建索引…");
                var created = await _kbService.CreateDatabaseAsync(repository);
                AppLog.Info($"创建索引：{created.Message}");

                var firstScan = await _fileScanService.BatchAddFilesToDatabaseAsync(repository);
                AppLog.Info($"首次扫描：{firstScan.Message}");
            }

            // The tree reflects the real directory; the index only contributes tags.
            // Scanning is blocking I/O, so keep it off the UI thread.
            var scan = await Task.Run(() => _fileScanService.ScanRepositoryFiles(repository));
            var diskFiles = scan.Success && scan.Data != null ? scan.Data : new List<string>();
            if (!scan.Success)
                AppLog.Warn($"资源管理器：扫描仓库失败 — {scan.Message}");

            var index = await _kbService.ListFilesWithTagsAsync(repository);
            var indexEntries = index.Success && index.Data != null ? index.Data : new List<FileEntryDto>();
            if (!index.Success)
                AppLog.Warn($"资源管理器：读取索引失败 — {index.Message}");

            var tagsByPath = BuildTagMap(indexEntries);

            // Records with no file on disk are surfaced for the user to resolve and
            // are never rendered as if they were real files.
            var stale = await _kbService.FindStaleRecordsAsync(repository, diskFiles);
            var staleList = stale.Success && stale.Data != null ? stale.Data : new List<string>();
            StaleIndexCount = staleList.Count;
            if (StaleIndexCount > 0)
                AppLog.Warn($"索引中有 {StaleIndexCount} 条记录在磁盘上不存在：{string.Join(", ", staleList)}");

            _fileEntries = diskFiles
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Select(f => new FileEntryDto
                {
                    FileName = f,
                    Tags = tagsByPath.TryGetValue(f, out var tags) ? tags : new List<string>()
                })
                .ToList();

            TotalFileCount = _fileEntries.Count;
            BuildFileTree();
            FileSummary = _fileEntries.Count == 0
                ? "仓库中没有 Markdown 文件"
                : $"共 {_fileEntries.Count} 个文件";

            // The count is already in the header above; leaving it in the status line as
            // well just printed the same sentence twice in one panel.
            ClearBusy();
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            ClearBusy();
            StatusMessage = $"加载失败: {ex.Message}";
            AppLog.Error("资源管理器：加载文件列表异常", ex);
        }
    }

    /// <summary>Refresh tag badges in place, without rebuilding the tree.</summary>
    public async Task RefreshTagsAsync()
    {
        var repository = _gitHelper.ReadGitConfig().RepositoryDirectory;
        if (string.IsNullOrWhiteSpace(repository)) return;

        var result = await _kbService.ListFilesWithTagsAsync(repository);
        if (!result.Success || result.Data == null) return;

        var tagsByPath = BuildTagMap(result.Data);

        foreach (var node in FlattenTree(FileTree))
        {
            if (node.FullPath == null || !node.IsFile) continue;
            node.SetTags(tagsByPath.TryGetValue(node.FullPath, out var tags)
                ? tags
                : new List<string>());
        }

        // The files on disk have not moved, so only the stale set needs recomputing.
        var stale = await _kbService.FindStaleRecordsAsync(
            repository, _fileEntries.Select(e => e.FileName).ToList());
        StaleIndexCount = stale.Success && stale.Data != null ? stale.Data.Count : 0;
    }

    /// <summary>
    /// Open the cleanup page in the editor area. The sidebar only holds the count:
    /// a long list of stale paths belongs on its own page, not squeezed in here.
    /// </summary>
    [RelayCommand]
    private async Task OpenStaleRecordsAsync()
    {
        if (Shell == null) return;
        await Shell.OpenStaleRecordsAsync();
    }

    /// <summary>Path → tags lookup that tolerates duplicate rows instead of throwing.</summary>
    private static Dictionary<string, List<string>> BuildTagMap(IEnumerable<FileEntryDto> entries)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
            map[entry.FileName] = entry.Tags;
        return map;
    }

    private void BuildFileTree()
    {
        var filter = FilterText.Trim();
        var entries = filter.Length == 0
            ? _fileEntries
            : _fileEntries
                .Where(e => e.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();

        // Remember which directories the user had open so a rebuild does not collapse them.
        var expanded = FlattenTree(FileTree)
            .Where(n => n.IsDirectory && n.IsExpanded)
            .Select(n => n.FullPath ?? n.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var expandAll = filter.Length > 0 || !_hasBuiltTreeOnce;
        var directories = new Dictionary<string, FileTreeNode>(StringComparer.OrdinalIgnoreCase);
        var root = new ObservableCollection<FileTreeNode>();

        foreach (var entry in entries.OrderBy(e => e.FileName, StringComparer.OrdinalIgnoreCase))
        {
            var parts = entry.FileName.Replace('\\', '/').Split('/');
            var current = root;
            var pathSoFar = string.Empty;

            for (int i = 0; i < parts.Length; i++)
            {
                var name = parts[i];
                pathSoFar = pathSoFar.Length == 0 ? name : $"{pathSoFar}/{name}";
                bool isFile = i == parts.Length - 1;

                if (isFile)
                {
                    var fileNode = new FileTreeNode
                    {
                        Name = name,
                        FullPath = entry.FileName,
                        IsDirectory = false
                    };
                    fileNode.SetTags(entry.Tags);
                    WireNode(fileNode);
                    current.Add(fileNode);
                }
                else
                {
                    if (!directories.TryGetValue(pathSoFar, out var directory))
                    {
                        directory = new FileTreeNode
                        {
                            Name = name,
                            FullPath = pathSoFar,
                            IsDirectory = true,
                            IsExpanded = expandAll || expanded.Contains(pathSoFar)
                        };
                        WireNode(directory);
                        directories[pathSoFar] = directory;
                        current.Add(directory);
                    }
                    current = directory.Children;
                }
            }
        }

        foreach (var directory in directories.Values)
            directory.FileCount = CountFiles(directory);

        var selectedPath = SelectedNode?.FullPath;
        FileTree = SortTree(root);
        _hasBuiltTreeOnce = true;

        if (selectedPath != null)
            RestoreSelection(selectedPath);
        else
            SelectedNode = null;
    }

    private static int CountFiles(FileTreeNode node) =>
        node.Children.Sum(c => c.IsFile ? 1 : CountFiles(c));

    /// <summary>Sort each level: directories first, then files, both alphabetical.</summary>
    private static ObservableCollection<FileTreeNode> SortTree(ObservableCollection<FileTreeNode> nodes)
    {
        var ordered = nodes
            .OrderByDescending(n => n.IsDirectory)
            .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new ObservableCollection<FileTreeNode>();
        foreach (var node in ordered)
        {
            if (node.IsDirectory)
            {
                var sortedChildren = SortTree(node.Children);
                node.Children.Clear();
                foreach (var child in sortedChildren) node.Children.Add(child);
            }
            result.Add(node);
        }
        return result;
    }

    private void RestoreSelection(string fullPath)
    {
        var match = FlattenTree(FileTree).FirstOrDefault(n => n.FullPath == fullPath);
        if (match == null)
        {
            SelectedNode = null;
            return;
        }

        SelectedNode = match;
    }

    private static IEnumerable<FileTreeNode> FlattenTree(IEnumerable<FileTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in FlattenTree(node.Children))
                yield return child;
        }
    }

    /// <summary>
    /// Give a node its row actions. Context menus live in a popup and cannot bind
    /// back to this ViewModel, so the node carries the commands itself.
    /// </summary>
    private void WireNode(FileTreeNode node)
    {
        node.ActivateRequested = n => _ = ActivateNodeAsync(n);
        node.OpenExternallyRequested = n => _ = OpenNodeExternallyAsync(n);
        node.OpenContainingFolderRequested = n => _ = OpenContainingFolderAsync(n);
        node.RenameRequested = n => _ = RenameNodeAsync(n);
        node.DeleteRequested = n => _ = DeleteNodeAsync(n);
    }

    // ── Selection / filtering ──────────────────────────────────────────────

    partial void OnFilterTextChanged(string value) => BuildFileTree();

    [RelayCommand]
    private void ClearFilter() => FilterText = string.Empty;

    /// <summary>Collapse every directory (VS Code's "Collapse Folders in Explorer").</summary>
    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var node in FlattenTree(FileTree).Where(n => n.IsDirectory))
            node.IsExpanded = false;

        StatusMessage = "已折叠全部目录";
    }

    // ── Node commands (single/double click + toolbar + context menu) ────────

    /// <summary>Open a file, or toggle a directory, exactly like a VS Code tree.</summary>
    public async Task ActivateNodeAsync(FileTreeNode? node, bool preview = false)
    {
        if (node == null) return;

        if (node.IsDirectory)
        {
            node.IsExpanded = !node.IsExpanded;
            return;
        }

        if (node.FullPath == null) return;
        if (Shell != null) await Shell.OpenFileAsync(node.FullPath, preview);
    }

    /// <summary>
    /// Select the node for the file the shell just activated, so the explorer always
    /// shows where the open document sits. Ancestors are expanded first — a selection
    /// inside a collapsed folder would be invisible, which is the whole complaint this
    /// answers. Directories the user collapsed on purpose are re-expanded on the way
    /// to the file, the same way VS Code reveals a file.
    /// </summary>
    public void RevealFile(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;

        var normalized = relativePath.Replace('\\', '/');
        var trail = new List<FileTreeNode>();
        if (!TryFindTrail(FileTree, normalized, trail))
        {
            // Filtered out of the tree, or no longer on disk: leave the current
            // selection alone rather than pointing the user at nothing.
            return;
        }

        foreach (var directory in trail.Where(n => n.IsDirectory))
            directory.IsExpanded = true;

        SelectedNode = trail[^1];
    }

    /// <summary>
    /// Find a file node and collect the nodes leading to it (ancestors last added,
    /// the file itself at the end), so the caller can expand the way down.
    /// </summary>
    private static bool TryFindTrail(
        IEnumerable<FileTreeNode> nodes, string fullPath, List<FileTreeNode> trail)
    {
        foreach (var node in nodes)
        {
            trail.Add(node);

            if (!node.IsDirectory)
            {
                if (string.Equals(node.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (TryFindTrail(node.Children, fullPath, trail))
            {
                return true;
            }

            trail.RemoveAt(trail.Count - 1);
        }

        return false;
    }

    public async Task OpenNodeExternallyAsync(FileTreeNode? node)
    {
        if (node is not { IsDirectory: false, FullPath: not null }) return;

        var repository = _gitHelper.ReadGitConfig().RepositoryDirectory;
        if (string.IsNullOrWhiteSpace(repository)) return;

        var resolved = _fileContentService.ResolveFullPath(repository, node.FullPath);
        if (!resolved.Success || resolved.Data == null)
        {
            StatusMessage = resolved.Message;
            return;
        }

        await _fileOpener.OpenFileAsync(resolved.Data);
        StatusMessage = $"已用外部程序打开: {node.Name}";
        AppLog.Info($"外部打开：{resolved.Data}");
    }

    /// <summary>Reveal the file in the OS file manager.</summary>
    public async Task OpenContainingFolderAsync(FileTreeNode? node)
    {
        if (node is not { IsDirectory: false, FullPath: not null }) return;

        var repository = _gitHelper.ReadGitConfig().RepositoryDirectory;
        if (string.IsNullOrWhiteSpace(repository)) return;

        var resolved = _fileContentService.ResolveFullPath(repository, node.FullPath);
        if (!resolved.Success || resolved.Data == null)
        {
            StatusMessage = resolved.Message;
            return;
        }

        await _fileOpener.OpenContainingFolderAsync(resolved.Data);
        StatusMessage = $"已在文件管理器中定位: {node.Name}";
        AppLog.Info($"打开所在目录：{resolved.Data}");
    }

    [RelayCommand]
    private async Task SyncFilesAsync()
    {
        var repository = _gitHelper.ReadGitConfig().RepositoryDirectory;
        if (string.IsNullOrWhiteSpace(repository))
        {
            StatusMessage = "请先在设置中配置仓库目录";
            return;
        }

        SetBusy("正在同步索引…");
        try
        {
            AppLog.Info("开始同步索引");
            var result = await _fileScanService.BatchAddFilesToDatabaseAsync(repository);
            AppLog.Info($"同步索引完成：{result.Message}");
            await LoadFilesAsync();
            if (Shell != null) await Shell.RefreshIndexAsync(rebuildTree: false);
            StatusMessage = result.Message;
        }
        catch (Exception ex)
        {
            AppLog.Error("同步索引异常", ex);
            StatusMessage = $"同步失败: {ex.Message}";
        }
        finally
        {
            ClearBusy();
        }
    }

    [RelayCommand]
    private async Task NewFileAsync()
    {
        var repository = _gitHelper.ReadGitConfig().RepositoryDirectory;
        if (string.IsNullOrWhiteSpace(repository))
        {
            StatusMessage = "请先在设置中配置仓库目录";
            return;
        }

        var input = await _dialogService.PromptForTextAsync(
            "新建笔记",
            "输入相对于仓库根目录的路径，例如 notes/新想法.md",
            "untitled.md");

        if (string.IsNullOrWhiteSpace(input)) return;

        var relativePath = EnsureMarkdownExtension(NormalizeSeparators(input));
        if (relativePath == null)
        {
            await _dialogService.ShowErrorAsync(
                "扩展名不受支持",
                "知识库只索引 Markdown 文件（.md / .markdown / .mdown / .mkd / .mkdn / .mdwn）。");
            return;
        }

        var created = await _fileContentService.CreateFileAsync(repository, relativePath, string.Empty);
        if (!created.Success)
        {
            AppLog.Error($"新建文件失败：{relativePath} — {created.Message}");
            await _dialogService.ShowErrorAsync("新建失败", created.Message);
            return;
        }

        AppLog.Info($"新建文件：{relativePath}");
        await _kbService.EnsureFileIndexedAsync(repository, relativePath);
        await LoadFilesAsync();
        if (Shell != null) await Shell.RefreshIndexAsync(rebuildTree: false);

        SelectedNode = FlattenTree(FileTree).FirstOrDefault(n => n.FullPath == relativePath);
        StatusMessage = created.Message;

        if (Shell != null) await Shell.OpenFileAsync(relativePath, preview: false);
    }

    public async Task RenameNodeAsync(FileTreeNode? node)
    {
        if (node is not { IsDirectory: false, FullPath: not null }) return;

        var repository = _gitHelper.ReadGitConfig().RepositoryDirectory;
        if (string.IsNullOrWhiteSpace(repository)) return;

        var oldPath = node.FullPath;
        var input = await _dialogService.PromptForTextAsync(
            "重命名 / 移动",
            "输入新的相对路径（可包含目录，用于移动文件）",
            oldPath);

        if (string.IsNullOrWhiteSpace(input)) return;

        var newPath = EnsureMarkdownExtension(NormalizeSeparators(input));
        if (newPath == null)
        {
            await _dialogService.ShowErrorAsync(
                "扩展名不受支持",
                "知识库只索引 Markdown 文件（.md / .markdown / .mdown / .mkd / .mkdn / .mdwn）。");
            return;
        }

        if (string.Equals(newPath, oldPath, StringComparison.Ordinal))
        {
            StatusMessage = "路径未改变";
            return;
        }

        SetBusy("正在重命名…");
        try
        {
            var moved = await _fileContentService.RenameFileAsync(repository, oldPath, newPath);
            if (!moved.Success)
            {
                await _dialogService.ShowErrorAsync("重命名失败", moved.Message);
                return;
            }

            AppLog.Info($"重命名：{oldPath} -> {newPath}");

            // The file is already on disk at this point, so an index failure is
            // reported rather than rolled back.
            var indexResult = await _kbService.RenameFileAsync(repository, oldPath, newPath);
            await LoadFilesAsync();
            if (Shell != null)
            {
                await Shell.OnFileRenamedAsync(oldPath, newPath);
                await Shell.RefreshIndexAsync(rebuildTree: false);
            }

            SelectedNode = FlattenTree(FileTree).FirstOrDefault(n => n.FullPath == newPath);
            StatusMessage = indexResult.Success
                ? moved.Message
                : $"文件已移动，但索引更新失败: {indexResult.Message}";
        }
        finally
        {
            ClearBusy();
        }
    }

    public async Task DeleteNodeAsync(FileTreeNode? node)
    {
        if (node is not { IsDirectory: false, FullPath: not null }) return;

        var repository = _gitHelper.ReadGitConfig().RepositoryDirectory;
        if (string.IsNullOrWhiteSpace(repository)) return;

        var path = node.FullPath;
        var confirmed = await _dialogService.ConfirmAsync(
            "确认删除",
            $"确定要删除 \"{path}\" 吗？\n\n这会同时从磁盘删除文件并移除索引记录，且不可撤销。");

        if (!confirmed) return;

        SetBusy("正在删除…");
        try
        {
            // Remove the index record first so a locked file still cannot leave a
            // dangling entry pointing at something the user believes is gone.
            await _kbService.DeleteFileAsync(repository, path);
            var deleted = await _fileContentService.DeleteFileAsync(repository, path);

            AppLog.Info($"删除文件：{path}（磁盘结果：{(deleted.Success ? "成功" : deleted.Message)}）");

            await LoadFilesAsync();
            if (Shell != null)
            {
                await Shell.OnFileDeletedAsync(path);
                await Shell.RefreshIndexAsync(rebuildTree: false);
            }

            StatusMessage = deleted.Success
                ? $"已删除 {path}"
                : $"索引记录已移除，但磁盘删除失败: {deleted.Message}";
        }
        finally
        {
            ClearBusy();
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string NormalizeSeparators(string path) =>
        path.Replace('\\', '/').Trim();

    /// <summary>
    /// Return the path with a Markdown extension, defaulting to ".md" when the
    /// user omitted one. Returns null when a non-Markdown extension was given.
    /// </summary>
    private static string? EnsureMarkdownExtension(string path)
    {
        var extension = System.IO.Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension)) return path + ".md";
        return MarkdownExtensions.Contains(extension) ? path : null;
    }
}
