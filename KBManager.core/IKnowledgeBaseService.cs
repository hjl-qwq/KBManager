using System.Collections.Generic;
using System.Threading.Tasks;

namespace KBManager.core
{
    /// <summary>
    /// Knowledge-base database operations. All methods accept an explicit
    /// repositoryDirectory so callers (CLI / GUI) control config resolution.
    /// </summary>
    public interface IKnowledgeBaseService
    {
        Task<ServiceResult> CreateDatabaseAsync(string repositoryDirectory);
        Task<ServiceResult> AddFileAsync(string repositoryDirectory, string fileName);
        Task<ServiceResult<List<FileEntryDto>>> ListFilesWithTagsAsync(string repositoryDirectory);
        Task<ServiceResult> AddTagToFileAsync(string repositoryDirectory, string fileName, string tagName);
        Task<ServiceResult<List<FileEntryDto>>> SearchFilesByTagAsync(string repositoryDirectory, string tagName);
        Task<ServiceResult> RemoveTagFromFileAsync(string repositoryDirectory, string fileName, string tagName);
        Task<ServiceResult> DeleteFileAsync(string repositoryDirectory, string fileName);
        Task<ServiceResult<List<TagEntryDto>>> ListAllTagsAsync(string repositoryDirectory);
        Task<ServiceResult<FileEntryDto?>> GetFileWithTagsAsync(string repositoryDirectory, string fileName);
    }
}
