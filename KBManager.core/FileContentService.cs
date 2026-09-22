using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace KBManager.core
{
    /// <summary>
    /// Pure business-logic file reader/writer for knowledge-base Markdown files.
    /// No UI dependencies; feedback is returned through <see cref="ServiceResult"/>.
    ///
    /// All paths are repository-relative and validated, so a malformed index entry
    /// or a crafted path can never read or write outside the repository root.
    /// </summary>
    public class FileContentService : IFileContentService
    {
        private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
        private static readonly UTF8Encoding Utf8Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        private static StringComparison PathComparison =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        public ServiceResult<string> NormalizeRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return ServiceResult<string>.Fail("File path cannot be empty.");

            var normalized = relativePath.Replace('\\', '/').Trim();

            if (Path.IsPathRooted(normalized))
                return ServiceResult<string>.Fail("File path must be relative to the repository.");

            // Reject Windows-style drive paths ("C:/...") and UNC shares ("//host/share")
            // even when running on Unix, so behaviour does not depend on the host OS.
            if (normalized.Length >= 2 && normalized[1] == ':' && char.IsLetter(normalized[0]))
                return ServiceResult<string>.Fail("File path must be relative to the repository.");

            if (normalized.StartsWith("//", StringComparison.Ordinal))
                return ServiceResult<string>.Fail("File path must be relative to the repository.");

            // Collapse "." and ".." segments without touching the file system.
            var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var stack = new System.Collections.Generic.List<string>();
            foreach (var part in parts)
            {
                if (part == ".") continue;
                if (part == "..")
                {
                    if (stack.Count == 0)
                        return ServiceResult<string>.Fail("File path must stay inside the repository.");
                    stack.RemoveAt(stack.Count - 1);
                    continue;
                }
                stack.Add(part);
            }

            if (stack.Count == 0)
                return ServiceResult<string>.Fail("File path must name a file.");

            return ServiceResult<string>.Ok(string.Join('/', stack));
        }

        public ServiceResult<string> ResolveFullPath(string repositoryDirectory, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(repositoryDirectory))
                return ServiceResult<string>.Fail("Repository directory is required.");

            var normalized = NormalizeRelativePath(relativePath);
            if (!normalized.Success || normalized.Data == null)
                return ServiceResult<string>.Fail(normalized.Message);

            string repoFull;
            try
            {
                repoFull = Path.GetFullPath(repositoryDirectory);
            }
            catch (Exception ex)
            {
                return ServiceResult<string>.Fail($"Invalid repository directory: {ex.Message}");
            }

            var combined = Path.GetFullPath(
                Path.Combine(repoFull, normalized.Data.Replace('/', Path.DirectorySeparatorChar)));

            var prefix = repoFull.EndsWith(Path.DirectorySeparatorChar)
                ? repoFull
                : repoFull + Path.DirectorySeparatorChar;

            if (!combined.StartsWith(prefix, PathComparison))
                return ServiceResult<string>.Fail("File path must stay inside the repository.");

            return ServiceResult<string>.Ok(combined);
        }

        public bool Exists(string repositoryDirectory, string relativePath)
        {
            var resolved = ResolveFullPath(repositoryDirectory, relativePath);
            return resolved.Success && resolved.Data != null && File.Exists(resolved.Data);
        }

        public async Task<ServiceResult<FileReadResultDto>> ReadTextAsync(string repositoryDirectory, string relativePath)
        {
            var resolved = ResolveFullPath(repositoryDirectory, relativePath);
            if (!resolved.Success || resolved.Data == null)
                return ServiceResult<FileReadResultDto>.Fail(resolved.Message);

            try
            {
                if (!File.Exists(resolved.Data))
                {
                    AppLog.Warn($"读取失败：文件不存在 relative={relativePath} resolved={resolved.Data}");
                    return ServiceResult<FileReadResultDto>.Fail(
                        $"文件不存在：{relativePath}（解析为 {resolved.Data}）");
                }

                var bytes = await File.ReadAllBytesAsync(resolved.Data);

                bool hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
                int offset = hasBom ? 3 : 0;

                string text;
                bool decodingIssues = false;
                try
                {
                    text = Utf8Strict.GetString(bytes, offset, bytes.Length - offset);
                }
                catch (DecoderFallbackException)
                {
                    // Not valid UTF-8 (e.g. a legacy GBK note). Decode lossily so the
                    // file can still be viewed, and flag it so the caller can warn.
                    text = Utf8NoBom.GetString(bytes, offset, bytes.Length - offset);
                    decodingIssues = true;
                }

                var dto = new FileReadResultDto
                {
                    Content = text,
                    LineEnding = DetectLineEnding(text),
                    HasUtf8Bom = hasBom,
                    SizeBytes = bytes.LongLength,
                    HadDecodingIssues = decodingIssues
                };

                return ServiceResult<FileReadResultDto>.Ok(
                    dto,
                    decodingIssues
                        ? "File is not valid UTF-8; characters may have been replaced."
                        : $"Loaded {relativePath} ({bytes.Length} bytes).");
            }
            catch (Exception ex)
            {
                AppLog.Error($"读取失败 relative={relativePath} resolved={resolved.Data}", ex);
                return ServiceResult<FileReadResultDto>.Fail(
                    $"读取文件出错：{ex.GetType().Name} - {ex.Message}");
            }
        }

        public async Task<ServiceResult> WriteTextAsync(
            string repositoryDirectory,
            string relativePath,
            string content,
            string? lineEnding = null,
            bool hasUtf8Bom = false)
        {
            var resolved = ResolveFullPath(repositoryDirectory, relativePath);
            if (!resolved.Success || resolved.Data == null)
                return ServiceResult.Fail(resolved.Message);

            try
            {
                var directory = Path.GetDirectoryName(resolved.Data);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var targetEol = lineEnding == "\r\n" ? "\r\n" : "\n";
                var normalized = ApplyLineEnding(content ?? string.Empty, targetEol);

                var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: hasUtf8Bom);

                // Write to a sibling temp file then move, so a crash or full disk
                // cannot leave a half-written note behind.
                var tempPath = resolved.Data + ".kbtmp";
                await File.WriteAllTextAsync(tempPath, normalized, encoding);
                File.Move(tempPath, resolved.Data, overwrite: true);

                return ServiceResult.Ok($"Saved {relativePath}.");
            }
            catch (Exception ex)
            {
                AppLog.Error($"保存失败 relative={relativePath} resolved={resolved.Data}", ex);
                return ServiceResult.Fail($"保存文件出错：{ex.GetType().Name} - {ex.Message}");
            }
        }

        public async Task<ServiceResult<FileReadResultDto>> CreateFileAsync(
            string repositoryDirectory,
            string relativePath,
            string? initialContent = null,
            bool overwrite = false)
        {
            var resolved = ResolveFullPath(repositoryDirectory, relativePath);
            if (!resolved.Success || resolved.Data == null)
                return ServiceResult<FileReadResultDto>.Fail(resolved.Message);

            try
            {
                if (File.Exists(resolved.Data) && !overwrite)
                    return ServiceResult<FileReadResultDto>.Fail($"File already exists: {relativePath}");

                var directory = Path.GetDirectoryName(resolved.Data);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var eol = Environment.NewLine == "\r\n" ? "\r\n" : "\n";
                var text = ApplyLineEnding(initialContent ?? string.Empty, eol);
                await File.WriteAllTextAsync(resolved.Data, text, new UTF8Encoding(false));

                var dto = new FileReadResultDto
                {
                    Content = initialContent ?? string.Empty,
                    LineEnding = eol,
                    HasUtf8Bom = false,
                    SizeBytes = text.Length
                };
                return ServiceResult<FileReadResultDto>.Ok(dto, $"Created {relativePath}.");
            }
            catch (Exception ex)
            {
                AppLog.Error($"新建失败 relative={relativePath} resolved={resolved.Data}", ex);
                return ServiceResult<FileReadResultDto>.Fail($"新建文件出错：{ex.GetType().Name} - {ex.Message}");
            }
        }

        public Task<ServiceResult> DeleteFileAsync(string repositoryDirectory, string relativePath)
        {
            var resolved = ResolveFullPath(repositoryDirectory, relativePath);
            if (!resolved.Success || resolved.Data == null)
                return Task.FromResult(ServiceResult.Fail(resolved.Message));

            try
            {
                if (!File.Exists(resolved.Data))
                    return Task.FromResult(ServiceResult.Fail($"File not found: {relativePath}"));

                File.Delete(resolved.Data);
                return Task.FromResult(ServiceResult.Ok($"Deleted {relativePath}."));
            }
            catch (Exception ex)
            {
                AppLog.Error($"删除失败 relative={relativePath} resolved={resolved.Data}", ex);
                return Task.FromResult(ServiceResult.Fail($"删除文件出错：{ex.GetType().Name} - {ex.Message}"));
            }
        }

        public Task<ServiceResult<string>> RenameFileAsync(
            string repositoryDirectory,
            string oldRelativePath,
            string newRelativePath,
            bool overwrite = false)
        {
            var oldResolved = ResolveFullPath(repositoryDirectory, oldRelativePath);
            if (!oldResolved.Success || oldResolved.Data == null)
                return Task.FromResult(ServiceResult<string>.Fail(oldResolved.Message));

            var newResolved = ResolveFullPath(repositoryDirectory, newRelativePath);
            if (!newResolved.Success || newResolved.Data == null)
                return Task.FromResult(ServiceResult<string>.Fail(newResolved.Message));

            var newNormalized = NormalizeRelativePath(newRelativePath);

            try
            {
                if (!File.Exists(oldResolved.Data))
                    return Task.FromResult(ServiceResult<string>.Fail($"File not found: {oldRelativePath}"));

                if (string.Equals(oldResolved.Data, newResolved.Data, PathComparison))
                    return Task.FromResult(ServiceResult<string>.Ok(newNormalized.Data!, "Path unchanged."));

                if (File.Exists(newResolved.Data) && !overwrite)
                    return Task.FromResult(ServiceResult<string>.Fail($"Target already exists: {newRelativePath}"));

                var directory = Path.GetDirectoryName(newResolved.Data);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.Move(oldResolved.Data, newResolved.Data, overwrite);
                return Task.FromResult(ServiceResult<string>.Ok(newNormalized.Data!, $"Renamed to {newNormalized.Data}."));
            }
            catch (Exception ex)
            {
                AppLog.Error($"重命名失败 {oldRelativePath} -> {newRelativePath}", ex);
                return Task.FromResult(ServiceResult<string>.Fail($"重命名出错：{ex.GetType().Name} - {ex.Message}"));
            }
        }

        // ---- helpers ----------------------------------------------------------

        /// <summary>Return the dominant line ending of a text block.</summary>
        private static string DetectLineEnding(string text)
        {
            int crlf = 0, lf = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '\n') continue;
                if (i > 0 && text[i - 1] == '\r') crlf++;
                else lf++;
            }
            return crlf > lf ? "\r\n" : "\n";
        }

        /// <summary>Rewrite every line ending to <paramref name="target"/>.</summary>
        private static string ApplyLineEnding(string content, string target)
        {
            var unified = content.Replace("\r\n", "\n").Replace('\r', '\n');
            return target == "\r\n" ? unified.Replace("\n", "\r\n") : unified;
        }
    }
}
