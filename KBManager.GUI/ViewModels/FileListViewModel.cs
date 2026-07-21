using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KBManager.core;
using KBManager.GUI.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KBManager.GUI.ViewModels;

/// <summary>
/// ViewModel for the File List / Tag Management page.
/// </summary>
public partial class FileListViewModel : ViewModelBase
{
    private readonly IKnowledgeBaseService _kbService;
    private readonly IFileScanService _fileScanService;
    private readonly GitHelper _gitHelper;
    private readonly IFileOpener _fileOpener;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<FileEntryDisplay> _files = new();

    [ObservableProperty]
    private FileEntryDisplay? _selectedFile;

    [ObservableProperty]
    private string _newTagName = string.Empty;

    [ObservableProperty]
    private string _selectedFileTags = string.Empty;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private string _fileSummary = "共 0 个文件";

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

    /// <summary>
    /// Called when the page becomes active — load files.
    /// </summary>
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
            Files.Clear();
            if (result.Success && result.Data != null)
            {
                foreach (var file in result.Data)
                {
                    Files.Add(new FileEntryDisplay
                    {
                        FileName = file.FileName,
                        TagsDisplay = file.Tags.Count > 0
                            ? string.Join(", ", file.Tags)
                            : "—",
                        TagCount = file.Tags.Count
                    });
                }
            }
            FileSummary = $"共 {Files.Count} 个文件";
            ClearBusy(FileSummary);
        }
        catch (System.Exception ex)
        {
            ClearBusy();
            StatusMessage = $"加载失败: {ex.Message}";
        }
    }

    /// <summary>
    /// Handle selection change — refresh selected file's tags.
    /// </summary>
    partial void OnSelectedFileChanged(FileEntryDisplay? value)
    {
        HasSelection = value != null;
        if (value != null)
        {
            _ = RefreshSelectedFileTagsAsync(value.FileName);
        }
        else
        {
            SelectedFileTags = string.Empty;
        }
    }

    private async Task RefreshSelectedFileTagsAsync(string fileName)
    {
        var config = _gitHelper.ReadGitConfig();
        if (string.IsNullOrWhiteSpace(config.RepositoryDirectory)) return;

        var result = await _kbService.GetFileWithTagsAsync(config.RepositoryDirectory, fileName);
        if (result.Success && result.Data != null)
        {
            SelectedFileTags = result.Data.Tags.Count > 0
                ? string.Join(", ", result.Data.Tags)
                : "(无标签)";
        }
    }

    /// <summary>
    /// Add a tag to the selected file.
    /// </summary>
    [RelayCommand]
    private async Task AddTagAsync()
    {
        if (SelectedFile == null || string.IsNullOrWhiteSpace(NewTagName))
        {
            StatusMessage = "请先选择文件并输入标签";
            return;
        }

        var config = _gitHelper.ReadGitConfig();
        if (string.IsNullOrWhiteSpace(config.RepositoryDirectory)) return;

        SetBusy("添加标签...");
        var result = await _kbService.AddTagToFileAsync(
            config.RepositoryDirectory, SelectedFile.FileName, NewTagName.Trim());

        if (result.Success)
        {
            NewTagName = string.Empty;
            await RefreshSelectedFileTagsAsync(SelectedFile.FileName);
            await LoadFilesAsync(); // refresh the list
        }

        ClearBusy(result.Message);
    }

    /// <summary>
    /// Remove a specific tag from the selected file.
    /// </summary>
    [RelayCommand]
    private async Task RemoveTagAsync(string tagName)
    {
        if (SelectedFile == null || string.IsNullOrWhiteSpace(tagName)) return;

        var config = _gitHelper.ReadGitConfig();
        if (string.IsNullOrWhiteSpace(config.RepositoryDirectory)) return;

        SetBusy("移除标签...");
        var result = await _kbService.RemoveTagFromFileAsync(
            config.RepositoryDirectory, SelectedFile.FileName, tagName);

        if (result.Success)
        {
            await RefreshSelectedFileTagsAsync(SelectedFile.FileName);
            await LoadFilesAsync();
        }

        ClearBusy(result.Message);
    }

    /// <summary>
    /// Sync files from disk to database.
    /// </summary>
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

    /// <summary>
    /// Remove selected file from database.
    /// </summary>
    [RelayCommand]
    private async Task RemoveFileAsync()
    {
        if (SelectedFile == null) return;

        var confirmed = await _dialogService.ConfirmAsync(
            "确认删除",
            $"确定要从数据库中移除 \"{SelectedFile.FileName}\" 吗？\n\n⚠ 这只会删除数据库记录，不会删除实际文件。");

        if (!confirmed) return;

        var config = _gitHelper.ReadGitConfig();
        if (string.IsNullOrWhiteSpace(config.RepositoryDirectory)) return;

        SetBusy("删除中...");
        var result = await _kbService.DeleteFileAsync(config.RepositoryDirectory, SelectedFile.FileName);
        await LoadFilesAsync();
        SelectedFile = null;
        ClearBusy(result.Message);
    }

    /// <summary>
    /// Open the selected file with the system default editor.
    /// </summary>
    [RelayCommand]
    private async Task OpenFileAsync()
    {
        if (SelectedFile == null) return;

        var config = _gitHelper.ReadGitConfig();
        if (string.IsNullOrWhiteSpace(config.RepositoryDirectory)) return;

        var fullPath = System.IO.Path.Combine(config.RepositoryDirectory, SelectedFile.FileName);
        await _fileOpener.OpenFileAsync(fullPath);
        StatusMessage = $"已打开: {SelectedFile.FileName}";
    }

    /// <summary>
    /// Open file with built-in editor (placeholder for future).
    /// </summary>
    [RelayCommand]
    private async Task OpenWithEditorAsync()
    {
        if (SelectedFile == null) return;

        var config = _gitHelper.ReadGitConfig();
        if (string.IsNullOrWhiteSpace(config.RepositoryDirectory)) return;

        var fullPath = System.IO.Path.Combine(config.RepositoryDirectory, SelectedFile.FileName);
        await _fileOpener.OpenWithBuiltInEditorAsync(fullPath);
    }
}
