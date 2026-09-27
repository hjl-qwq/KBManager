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

        /// <summary>
        /// True when the index database file already exists for this repository.
        /// Lets callers bootstrap an empty repository without string-matching errors.
        /// </summary>
        bool DatabaseExists(string repositoryDirectory);

        Task<ServiceResult> AddFileAsync(string repositoryDirectory, string fileName);

        /// <summary>
        /// Ensure a file has an index record, creating it when missing.
        /// Returns true when a new record was inserted.
        /// </summary>
        Task<ServiceResult<bool>> EnsureFileIndexedAsync(string repositoryDirectory, string fileName);
        Task<ServiceResult<List<FileEntryDto>>> ListFilesWithTagsAsync(string repositoryDirectory);
        Task<ServiceResult> AddTagToFileAsync(string repositoryDirectory, string fileName, string tagName);
        Task<ServiceResult<List<FileEntryDto>>> SearchFilesByTagAsync(string repositoryDirectory, string tagName);
        Task<ServiceResult> RemoveTagFromFileAsync(string repositoryDirectory, string fileName, string tagName);
        Task<ServiceResult> DeleteFileAsync(string repositoryDirectory, string fileName);

        /// <summary>
        /// Move an index record to a new repository-relative path, preserving its
        /// tags. Any stale record already at the destination is folded in.
        /// </summary>
        Task<ServiceResult> RenameFileAsync(string repositoryDirectory, string oldFileName, string newFileName);

        /// <summary>
        /// Reconcile the index against the real directory: return the index records
        /// whose file is absent from <paramref name="filesOnDisk"/>.
        ///
        /// The caller supplies the on-disk file list so a single scan can serve both
        /// the tree and this comparison. Keeps the "what counts as stale" rule in one
        /// place instead of scattering it across callers.
        /// </summary>
        Task<ServiceResult<List<string>>> FindStaleRecordsAsync(
            string repositoryDirectory,
            IReadOnlyCollection<string> filesOnDisk);

        Task<ServiceResult<List<TagEntryDto>>> ListAllTagsAsync(string repositoryDirectory);
        Task<ServiceResult<List<TagWithCountDto>>> GetTagsWithFileCountAsync(string repositoryDirectory);
        Task<ServiceResult<FileEntryDto?>> GetFileWithTagsAsync(string repositoryDirectory, string fileName);
    }
}
