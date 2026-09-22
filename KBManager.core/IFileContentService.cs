using System.Threading.Tasks;

namespace KBManager.core
{
    /// <summary>
    /// Result of reading a text file: the decoded content plus the physical
    /// details needed to write it back byte-compatibly (BOM / line endings).
    /// </summary>
    public class FileReadResultDto
    {
        /// <summary>Decoded file content.</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Dominant line ending of the original file: "\n" or "\r\n".
        /// Used so saving does not silently rewrite a CRLF file to LF.
        /// </summary>
        public string LineEnding { get; set; } = "\n";

        /// <summary>Whether the original file started with a UTF-8 BOM.</summary>
        public bool HasUtf8Bom { get; set; }

        /// <summary>File size in bytes.</summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// True when the bytes were not valid UTF-8 and had to be decoded
        /// lossily; the caller should warn the user before saving.
        /// </summary>
        public bool HadDecodingIssues { get; set; }
    }

    /// <summary>
    /// Reads and writes the Markdown files that make up the knowledge base.
    /// Every path is interpreted relative to the repository root and is
    /// validated so it cannot escape that root.
    /// </summary>
    public interface IFileContentService
    {
        /// <summary>
        /// Resolve a repository-relative path to an absolute path, rejecting
        /// rooted paths or anything that escapes the repository.
        /// </summary>
        ServiceResult<string> ResolveFullPath(string repositoryDirectory, string relativePath);

        /// <summary>Normalise a relative path to the canonical "a/b.md" form.</summary>
        ServiceResult<string> NormalizeRelativePath(string relativePath);

        /// <summary>True when the repository-relative file exists on disk.</summary>
        bool Exists(string repositoryDirectory, string relativePath);

        /// <summary>Read a text file, capturing encoding and line-ending details.</summary>
        Task<ServiceResult<FileReadResultDto>> ReadTextAsync(string repositoryDirectory, string relativePath);

        /// <summary>
        /// Write text back to a file. When <paramref name="lineEnding"/> is null the
        /// environment default is used; pass the value from the read result to
        /// preserve the file's original convention.
        /// </summary>
        Task<ServiceResult> WriteTextAsync(
            string repositoryDirectory,
            string relativePath,
            string content,
            string? lineEnding = null,
            bool hasUtf8Bom = false);

        /// <summary>Create a new file with optional initial content.</summary>
        Task<ServiceResult<FileReadResultDto>> CreateFileAsync(
            string repositoryDirectory,
            string relativePath,
            string? initialContent = null,
            bool overwrite = false);

        /// <summary>Delete a file from disk.</summary>
        Task<ServiceResult> DeleteFileAsync(string repositoryDirectory, string relativePath);

        /// <summary>
        /// Rename or move a file, creating the destination directory when needed.
        /// Returns the normalised new relative path.
        /// </summary>
        Task<ServiceResult<string>> RenameFileAsync(
            string repositoryDirectory,
            string oldRelativePath,
            string newRelativePath,
            bool overwrite = false);
    }
}
