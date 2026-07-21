using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KBManager.core;
using KBManager.GUI.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KBManager.GUI.ViewModels;

/// <summary>
/// Display model for a single tag with a remove button.
/// </summary>
public partial class FileTagItem : ObservableObject
{
    public string TagName { get; set; } = string.Empty;
}

/// <summary>
/// ViewModel for the File List / Tag Management page.
/// Features: TreeView, tag chips with removal, auto-complete suggestions,
/// preserves selection across tag edits.
/// </summary>
public partial class FileListViewModel : ViewModelBase
{
    private readonly IKnowledgeBaseService _kbService;
    private readonly IFileScanService _fileScanService;
    private readonly GitHelper _gitHelper;
    private readonly IFileOpener _fileOpener;
    private readonly IDialogService _dialogService;

    // Allowed markdown extensions (must match FileScanService)
    private static readonly HashSet<string> MdExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".mdown", ".mkd", ".mkdn", ".mdwn"
    };

    private static bool IsMarkdownFile(string fileName) =>
        MdExtensions.Contains(System.IO.Path.GetExtension(fileName));

    private List<FileEntryDto> _fileEntries = new();
    private HashSet<string> _allTags = new();

    [ObservableProperty]
    private ObservableCollection<FileTreeNode> _fileTree = new();

    [ObservableProperty]
    private FileTreeNode? _selectedNode;

    [ObservableProperty]
    private string _newTagName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<FileTagItem> _currentTags = new();

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private string _fileSummary = "共 0 个文件";

    [ObservableProperty]
    private ObservableCollection<string> _tagSuggestions = new();

    public FileListViewModel(
        IKnowledgeBaseService kbService,
        IFileScanService fileScanService,
        GitHelper gitHelper,
        IFileOpener fileOpener,
        IDialogService dialogService)
    {
        _kbService = kbService;
        _fileScanService = fileScanService;
        _gitHelper = gitHelper;
        _fileOpener = fileOpener;
        _dialogService = dialogService;
    }

    // =========================================================================
    //  Load & Build Tree
    // =========================================================================

    [RelayCommand]
    public async Task LoadFilesAsync()
    {
        var config = _gitHelper.ReadGitConfig();
        if (string.IsNullOrWhiteSpace(config.RepositoryDirectory))
        {
            StatusMessage = "请先在设置中配置仓库目录";
            return;
        }

        SetBusy("加载文件列表...");
        try
        {
            var result = await _kbService.ListFilesWithTagsAsync(config.RepositoryDirectory);
            _fileEntries = result.Success && result.Data != null
                ? result.Data.Where(f => IsMarkdownFile(f.FileName)).ToList()
                : new();
            _allTags = _fileEntries.SelectMany(f => f.Tags).ToHashSet();

            BuildFileTree();
            FileSummary = $"共 {_fileEntries.Count} 个文件";
            ClearBusy(FileSummary);
        }
        catch (System.Exception ex)
        {
            ClearBusy();
            StatusMessage = $"加载失败: {ex.Message}";
        }
    }

    private void BuildFileTree()
    {
        var root = new ObservableCollection<FileTreeNode>();

        foreach (var entry in _fileEntries.OrderBy(f => f.FileName))
        {
            var parts = entry.FileName.Replace('\\', '/').Split('/');
            var current = root;

            for (int i = 0; i < parts.Length; i++)
            {
                var isFile = i == parts.Length - 1;
                var name = parts[i];
                var existing = current.FirstOrDefault(n => n.Name == name);

                if (existing != null)
                {
                    if (isFile)
                    {
                        existing.TagCount = entry.Tags.Count;
                        existing.FullPath = entry.FileName;
                        existing.IsDirectory = false;
                    }
                    current = existing.Children;
                }
                else
                {
                    var node = new FileTreeNode
                    {
                        Name = name,
                        IsDirectory = !isFile,
                        FullPath = isFile ? entry.FileName : null,
                        TagCount = isFile ? entry.Tags.Count : 0,
                        IsExpanded = !isFile
                    };
                    current.Add(node);
                    current = node.Children;
                }
            }
        }

        var selectedPath = SelectedNode?.FullPath;
        FileTree = root;

        if (selectedPath != null)
            RestoreSelection(selectedPath);
    }

    private void RestoreSelection(string fullPath)
    {
        foreach (var node in FlattenTree(FileTree))
        {
            if (node.FullPath == fullPath)
            {
                SelectedNode = node;
                node.IsSelected = true;
                break;
            }
        }
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

    // =========================================================================
    //  Selection
    // =========================================================================

    partial void OnSelectedNodeChanged(FileTreeNode? value)
    {
        HasSelection = value != null && !value.IsDirectory;
        if (value != null && !value.IsDirectory)
            _ = RefreshSelectedFileTagsAsync(value.FullPath!);
        else
            CurrentTags.Clear();
    }

    private async Task RefreshSelectedFileTagsAsync(string fileName)
    {
        var config = _gitHelper.ReadGitConfig();
        if (string.IsNullOrWhiteSpace(config.RepositoryDirectory)) return;

        var result = await _kbService.GetFileWithTagsAsync(config.RepositoryDirectory, fileName);
        CurrentTags.Clear();
        if (result.Success && result.Data != null)
        {
            foreach (var tag in result.Data.Tags)
                CurrentTags.Add(new FileTagItem { TagName = tag });
        }
    }

    // =========================================================================
    //  Tag Auto-Complete
    // =========================================================================

    /// <summary>
    /// Set while programmatically changing NewTagName (e.g. clicking a
    /// suggestion) to prevent OnNewTagNameChanged from replacing the
    /// ListBox ItemsSource mid-selection and crashing Avalonia.
    /// </summary>
    private bool _suppressTagFilter;

    partial void OnNewTagNameChanged(string value)
    {
        if (_suppressTagFilter) return;

        if (string.IsNullOrWhiteSpace(value))
        {
            TagSuggestions.Clear();
            return;
        }

        var suggestions = _allTags
            .Where(t => t.StartsWith(value, System.StringComparison.OrdinalIgnoreCase)
                     && !CurrentTags.Any(ct => ct.TagName == t))
            .OrderBy(t => t)
            .Take(8)
            .ToList();

        TagSuggestions = new ObservableCollection<string>(suggestions);
    }

    /// <summary>
    /// Double-click a suggestion: directly add the tag without extra clicks.
    /// </summary>
    [RelayCommand]
    private async Task AddTagDirectlyAsync(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName)) return;

        _suppressTagFilter = true;
        NewTagName = tagName;
        _suppressTagFilter = false;
        TagSuggestions.Clear();

        await AddTagAsync();
    }

    // =========================================================================
    //  Add / Remove Tags (preserves selection)
    // =========================================================================

    [RelayCommand]
    private async Task AddTagAsync()
    {
        if (SelectedNode == null || SelectedNode.IsDirectory || string.IsNullOrWhiteSpace(NewTagName))
        {
            StatusMessage = "请先选择文件并输入标签";
            return;
        }

        var config = _gitHelper.ReadGitConfig();
        if (string.IsNullOrWhiteSpace(config.RepositoryDirectory)) return;

        SetBusy("添加标签...");
        var result = await _kbService.AddTagToFileAsync(
            config.RepositoryDirectory, SelectedNode.FullPath!, NewTagName.Trim());

        if (result.Success)
        {
            _allTags.Add(NewTagName.Trim());
            NewTagName = string.Empty;
            TagSuggestions.Clear();
            await RefreshSelectedFileTagsAsync(SelectedNode.FullPath!);
            SelectedNode.TagCount = CurrentTags.Count;
            var entry = _fileEntries.FirstOrDefault(f => f.FileName == SelectedNode.FullPath);
            if (entry != null) entry.Tags = CurrentTags.Select(t => t.TagName).ToList();
        }

        ClearBusy(result.Message);
    }

    [RelayCommand]
    private async Task RemoveTagAsync(FileTagItem? tagItem)
    {
        if (SelectedNode == null || SelectedNode.IsDirectory || tagItem == null) return;

        var config = _gitHelper.ReadGitConfig();
        if (string.IsNullOrWhiteSpace(config.RepositoryDirectory)) return;

        SetBusy("移除标签...");
        var result = await _kbService.RemoveTagFromFileAsync(
            config.RepositoryDirectory, SelectedNode.FullPath!, tagItem.TagName);

        if (result.Success)
        {
            await RefreshSelectedFileTagsAsync(SelectedNode.FullPath!);
            SelectedNode.TagCount = CurrentTags.Count;
            var entry = _fileEntries.FirstOrDefault(f => f.FileName == SelectedNode.FullPath);
            if (entry != null) entry.Tags = CurrentTags.Select(t => t.TagName).ToList();
        }

        ClearBusy(result.Message);
    }

    // =========================================================================
    //  Sync
    // =========================================================================

    [RelayCommand]
    private async Task SyncFilesAsync()
    {
        var config = _gitHelper.ReadGitConfig();
        if (string.IsNullOrWhiteSpace(config.RepositoryDirectory))
        {
            StatusMessage = "请先在设置中配置仓库目录";
            return;
        }

        SetBusy("同步中...");
        var result = await _fileScanService.BatchAddFilesToDatabaseAsync(config.RepositoryDirectory);
        await LoadFilesAsync();
        ClearBusy(result.Message);
    }

    // =========================================================================
    //  Remove file from DB
    // =========================================================================

    [RelayCommand]
    private async Task RemoveFileAsync()
    {
        if (SelectedNode == null || SelectedNode.IsDirectory) return;

        var confirmed = await _dialogService.ConfirmAsync(
            "确认删除",
            $"确定要从数据库中移除 \"{SelectedNode.Name}\" 吗？\n\n⚠ 这只会删除数据库记录，不会删除实际文件。");

        if (!confirmed) return;

        var config = _gitHelper.ReadGitConfig();
        if (string.IsNullOrWhiteSpace(config.RepositoryDirectory)) return;

        SetBusy("删除中...");
        var result = await _kbService.DeleteFileAsync(config.RepositoryDirectory, SelectedNode.FullPath!);
        SelectedNode = null;
        await LoadFilesAsync();
        ClearBusy(result.Message);
    }

    // =========================================================================
    //  Open file
    // =========================================================================

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        if (SelectedNode == null || SelectedNode.IsDirectory) return;

        var config = _gitHelper.ReadGitConfig();
        if (string.IsNullOrWhiteSpace(config.RepositoryDirectory)) return;

        var fullPath = System.IO.Path.Combine(config.RepositoryDirectory, SelectedNode.FullPath!);
        await _fileOpener.OpenFileAsync(fullPath);
        StatusMessage = $"已打开: {SelectedNode.Name}";
    }
}
