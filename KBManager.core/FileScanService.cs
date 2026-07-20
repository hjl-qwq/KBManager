using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KBManager.core
{
    /// <summary>
    /// Pure business-logic file scanner — no Console I/O.
    /// </summary>
    public class FileScanService : IFileScanService
    {
        private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".kbdatabase"
        };

        public ServiceResult<List<string>> ScanRepositoryFiles(string repositoryDirectory)
        {
            if (string.IsNullOrWhiteSpace(repositoryDirectory))
                return ServiceResult<List<string>>.Fail("Repository directory is required.");
            if (!Directory.Exists(repositoryDirectory))
                return ServiceResult<List<string>>.Fail($"Directory not found: {repositoryDirectory}");

            var validFiles = new List<string>();
            try
            {
                var allFiles = Directory.EnumerateFiles(repositoryDirectory, "*.*", SearchOption.AllDirectories);
                foreach (var fullPath in allFiles)
                {
                    if (IsExcluded(fullPath, repositoryDirectory)) continue;
                    if (IsHidden(fullPath)) continue;

                    var relative = GetRelativePath(repositoryDirectory, fullPath);
                    if (relative.Length > 500) continue;

                    validFiles.Add(relative);
                }
                return ServiceResult<List<string>>.Ok(validFiles, $"Scanned {validFiles.Count} file(s).");
            }
            catch (Exception ex)
            {
                return ServiceResult<List<string>>.Fail($"Scan failed: {ex.Message}");
            }
        }

        public async Task<ServiceResult> BatchAddFilesToDatabaseAsync(string repositoryDirectory)
        {
            var scanResult = ScanRepositoryFiles(repositoryDirectory);
            if (!scanResult.Success) return ServiceResult.Fail(scanResult.Message);

            var filePaths = scanResult.Data!;
            if (filePaths.Count == 0)
                return ServiceResult.Ok("No valid files found to add.");

            try
            {
                using var context = new FileTagDbContext(repositoryDirectory);
                if (!context.CheckDatabaseExists())
                    await context.CreateDatabaseAsync();

                using var transaction = await context.Database.BeginTransactionAsync();
                var existing = await context.Files.Select(f => f.FileName).ToListAsync();
                var newFiles = filePaths
                    .Where(p => !existing.Contains(p))
                    .Select(p => new FileInfo { FileName = p })
                    .ToList();

                if (newFiles.Count == 0)
                {
                    await transaction.CommitAsync();
                    return ServiceResult.Ok("All files already exist in database.");
                }

                await context.Files.AddRangeAsync(newFiles);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ServiceResult.Ok($"Added {newFiles.Count} new file(s), skipped {filePaths.Count - newFiles.Count} duplicate(s).");
            }
            catch (Exception ex)
            {
                return ServiceResult.Fail($"Batch add failed: {ex.Message}");
            }
        }

        // ---- helpers ----------------------------------------------------------

        private static string GetRelativePath(string root, string fullPath)
        {
            var rel = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(fullPath));
            return rel.Replace(Path.DirectorySeparatorChar, '/').Trim();
        }

        private static bool IsExcluded(string filePath, string rootDir)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? string.Empty;
            var rel = Path.GetRelativePath(Path.GetFullPath(rootDir), dir);
            return rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(s => ExcludedDirectories.Contains(s));
        }

        private static bool IsHidden(string filePath)
        {
            try
            {
                var fi = new System.IO.FileInfo(filePath);
                if (Environment.OSVersion.Platform.ToString().Contains("Win"))
                    return fi.Attributes.HasFlag(FileAttributes.Hidden);
                return !string.IsNullOrEmpty(fi.Name) && fi.Name.StartsWith(".");
            }
            catch
            {
                return true;
            }
        }
    }
}
