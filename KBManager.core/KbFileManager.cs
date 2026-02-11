using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace KBManager.core
{
    public class KbFileManager
    {
        private readonly List<string> _excludedDirectories = new List<string>
        {
            ".git",
            ".kbdatabase"
        };

        public List<string> ScanRepositoryFiles(string repositoryDirectory)
        {
            if (string.IsNullOrEmpty(repositoryDirectory))
            {
                Console.WriteLine("Error: Repository directory cannot be empty");
                return new List<string>();
            }

            if (!Directory.Exists(repositoryDirectory))
            {
                Console.WriteLine($"Error: Repository directory not found - {repositoryDirectory}");
                return new List<string>();
            }

            var validFilePaths = new List<string>();

            try
            {
                var allFiles = Directory.EnumerateFiles(
                    repositoryDirectory,
                    "*.*",
                    SearchOption.AllDirectories
                );

                foreach (var fullFilePath in allFiles)
                {
                    if (IsInExcludedDirectory(fullFilePath, repositoryDirectory))
                    {
                        continue;
                    }

                    if (IsHiddenFile(fullFilePath))
                    {
                        continue;
                    }

                    string relativePath = GetStandardizedRelativePath(repositoryDirectory, fullFilePath);

                    if (relativePath.Length > 500)
                    {
                        Console.WriteLine($"Warning: File path too long (>{500} chars), skipped - {relativePath}");
                        continue;
                    }

                    validFilePaths.Add(relativePath);
                }

                Console.WriteLine($"Scan completed: Found {validFilePaths.Count} valid files (excluded hidden/system files)");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Error: No permission to access directory - {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning files: {ex.Message}");
            }

            return validFilePaths;
        }

        public async Task<bool> BatchAddFilesToDatabaseAsync(string repositoryDirectory)
        {
            if (string.IsNullOrEmpty(repositoryDirectory))
            {
                Console.WriteLine("Error: Repository directory cannot be empty");
                return false;
            }

            var filePaths = ScanRepositoryFiles(repositoryDirectory);
            if (!filePaths.Any())
            {
                Console.WriteLine("No valid files to add to database");
                return true;
            }

            using (var context = new FileTagDbContext(repositoryDirectory))
            {
                if (!context.CheckDatabaseExists())
                {
                    Console.WriteLine("Database not found, creating new database first...");
                    await context.CreateDatabaseAsync();
                }

                try
                {
                    using (var transaction = await context.Database.BeginTransactionAsync())
                    {
                        var existingFileNames = await context.Files
                            .Select(f => f.FileName)
                            .ToListAsync();

                        var newFiles = filePaths
                            .Where(path => !existingFileNames.Contains(path))
                            .Select(path => new FileInfo { FileName = path })
                            .ToList();

                        if (!newFiles.Any())
                        {
                            Console.WriteLine("All scanned files already exist in database");
                            await transaction.CommitAsync();
                            return true;
                        }

                        await context.Files.AddRangeAsync(newFiles);
                        await context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        Console.WriteLine($"Successfully added {newFiles.Count} new files to database");
                        Console.WriteLine($"Skipped {filePaths.Count - newFiles.Count} duplicate files");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error adding files to database: {ex.Message}");
                    return false;
                }
            }
        }


        private string GetStandardizedRelativePath(string rootDir, string fullFilePath)
        {
            string fullRootDir = Path.GetFullPath(rootDir);
            string fullFile = Path.GetFullPath(fullFilePath);

            string relativePath = Path.GetRelativePath(fullRootDir, fullFile);

            return relativePath.Replace(Path.DirectorySeparatorChar, '/').Trim();
        }

        private bool IsInExcludedDirectory(string filePath, string rootDir)
        {
            string fileDirectory = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? string.Empty;
            string relativeDir = Path.GetRelativePath(Path.GetFullPath(rootDir), fileDirectory);

            var dirSegments = relativeDir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return dirSegments.Any(segment => _excludedDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase));
        }

        private bool IsHiddenFile(string filePath)
        {
            try
            {
                var fileInfo = new System.IO.FileInfo(filePath);

                if (Environment.OSVersion.Platform.ToString().Contains("Win"))
                {
                    return fileInfo.Attributes.HasFlag(FileAttributes.Hidden);
                }

                return !string.IsNullOrEmpty(fileInfo.Name) && fileInfo.Name.StartsWith(".");
            }
            catch (Exception)
            {
                return true;
            }
        }

        public async Task<bool> ScanAndAddFilesFromGitConfigAsync()
        {
            var gitHelper = new GitHelper();
            var gitConfig = gitHelper.ReadGitConfig();

            if (!gitConfig.ValidateCoreConfig())
            {
                Console.WriteLine("Git config validation failed");
                return false;
            }

            return await BatchAddFilesToDatabaseAsync(gitConfig.RepositoryDirectory);
        }
    }
}