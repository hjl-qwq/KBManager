using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KBManager.core
{
    /// <summary>
    /// Pure business-logic implementation of IKnowledgeBaseService.
    /// No Console.WriteLine / Console.ReadLine — all feedback is returned
    /// via ServiceResult so the same code works for CLI and GUI.
    /// </summary>
    public class KnowledgeBaseService : IKnowledgeBaseService
    {
        public async Task<ServiceResult> CreateDatabaseAsync(string repositoryDirectory)
        {
            if (string.IsNullOrWhiteSpace(repositoryDirectory))
                return ServiceResult.Fail("Repository directory is required.");

            try
            {
                using var context = new FileTagDbContext(repositoryDirectory);
                bool created = await context.CreateDatabaseAsync();
                return created
                    ? ServiceResult.Ok("Database created successfully.")
                    : ServiceResult.Ok("Database already exists.");
            }
            catch (Exception ex)
            {
                return ServiceResult.Fail($"Failed to create database: {ex.Message}");
            }
        }

        public async Task<ServiceResult> AddFileAsync(string repositoryDirectory, string fileName)
        {
            if (string.IsNullOrWhiteSpace(repositoryDirectory))
                return ServiceResult.Fail("Repository directory is required.");
            if (string.IsNullOrWhiteSpace(fileName))
                return ServiceResult.Fail("File name cannot be empty.");

            try
            {
                using var context = new FileTagDbContext(repositoryDirectory);
                if (!context.CheckDatabaseExists())
                    return ServiceResult.Fail("Database does not exist. Create one first.");

                bool exists = await context.Files.AnyAsync(f => f.FileName == fileName);
                if (exists)
                    return ServiceResult.Fail($"File '{fileName}' already exists in database.");

                context.Files.Add(new FileInfo { FileName = fileName });
                await context.SaveChangesAsync();
                return ServiceResult.Ok($"File '{fileName}' added successfully.");
            }
            catch (Exception ex)
            {
                return ServiceResult.Fail($"Failed to add file: {ex.Message}");
            }
        }

        public async Task<ServiceResult<List<FileEntryDto>>> ListFilesWithTagsAsync(string repositoryDirectory)
        {
            if (string.IsNullOrWhiteSpace(repositoryDirectory))
                return ServiceResult<List<FileEntryDto>>.Fail("Repository directory is required.");

            try
            {
                using var context = new FileTagDbContext(repositoryDirectory);
                if (!context.CheckDatabaseExists())
                    return ServiceResult<List<FileEntryDto>>.Fail("Database does not exist. Create one first.");

                var files = await context.Files
                    .Include(f => f.Tags)
                    .OrderBy(f => f.FileName)
                    .ToListAsync();

                var dtos = files.Select(f => new FileEntryDto
                {
                    FileName = f.FileName,
                    Tags = f.Tags?.Select(t => t.TagName).OrderBy(t => t).ToList() ?? new List<string>()
                }).ToList();

                return ServiceResult<List<FileEntryDto>>.Ok(dtos, $"Found {dtos.Count} file(s).");
            }
            catch (Exception ex)
            {
                return ServiceResult<List<FileEntryDto>>.Fail($"Failed to list files: {ex.Message}");
            }
        }

        public async Task<ServiceResult<FileEntryDto?>> GetFileWithTagsAsync(string repositoryDirectory, string fileName)
        {
            if (string.IsNullOrWhiteSpace(repositoryDirectory))
                return ServiceResult<FileEntryDto?>.Fail("Repository directory is required.");
            if (string.IsNullOrWhiteSpace(fileName))
                return ServiceResult<FileEntryDto?>.Fail("File name cannot be empty.");

            try
            {
                using var context = new FileTagDbContext(repositoryDirectory);
                if (!context.CheckDatabaseExists())
                    return ServiceResult<FileEntryDto?>.Fail("Database does not exist.");

                var file = await context.Files
                    .Include(f => f.Tags)
                    .FirstOrDefaultAsync(f => f.FileName == fileName);

                if (file == null)
                    return ServiceResult<FileEntryDto?>.Fail($"File '{fileName}' not found in database.");

                var dto = new FileEntryDto
                {
                    FileName = file.FileName,
                    Tags = file.Tags?.Select(t => t.TagName).OrderBy(t => t).ToList() ?? new List<string>()
                };
                return ServiceResult<FileEntryDto?>.Ok(dto);
            }
            catch (Exception ex)
            {
                return ServiceResult<FileEntryDto?>.Fail($"Failed to get file: {ex.Message}");
            }
        }

        public async Task<ServiceResult> AddTagToFileAsync(string repositoryDirectory, string fileName, string tagName)
        {
            if (string.IsNullOrWhiteSpace(repositoryDirectory))
                return ServiceResult.Fail("Repository directory is required.");
            if (string.IsNullOrWhiteSpace(fileName))
                return ServiceResult.Fail("File name cannot be empty.");
            if (string.IsNullOrWhiteSpace(tagName))
                return ServiceResult.Fail("Tag name cannot be empty.");

            try
            {
                using var context = new FileTagDbContext(repositoryDirectory);
                if (!context.CheckDatabaseExists())
                    return ServiceResult.Fail("Database does not exist. Create one first.");

                var file = await context.Files.Include(f => f.Tags)
                    .FirstOrDefaultAsync(f => f.FileName == fileName);
                if (file == null)
                    return ServiceResult.Fail($"File '{fileName}' not found in database.");

                if (file.Tags.Any(t => t.TagName == tagName))
                    return ServiceResult.Fail($"Tag '{tagName}' already exists on file '{fileName}'.");

                var tag = await context.Tags.FirstOrDefaultAsync(t => t.TagName == tagName)
                          ?? new Tag { TagName = tagName };

                if (tag.Id == 0) context.Tags.Add(tag);
                file.Tags.Add(tag);
                await context.SaveChangesAsync();

                return ServiceResult.Ok($"Tag '{tagName}' added to '{fileName}'.");
            }
            catch (Exception ex)
            {
                return ServiceResult.Fail($"Failed to add tag: {ex.Message}");
            }
        }

        public async Task<ServiceResult<List<FileEntryDto>>> SearchFilesByTagAsync(string repositoryDirectory, string tagName)
        {
            if (string.IsNullOrWhiteSpace(repositoryDirectory))
                return ServiceResult<List<FileEntryDto>>.Fail("Repository directory is required.");
            if (string.IsNullOrWhiteSpace(tagName))
                return ServiceResult<List<FileEntryDto>>.Fail("Tag name cannot be empty.");

            try
            {
                using var context = new FileTagDbContext(repositoryDirectory);
                if (!context.CheckDatabaseExists())
                    return ServiceResult<List<FileEntryDto>>.Fail("Database does not exist. Create one first.");

                var tag = await context.Tags
                    .Include(t => t.Files)
                    .FirstOrDefaultAsync(t => t.TagName == tagName);

                if (tag == null || tag.Files.Count == 0)
                    return ServiceResult<List<FileEntryDto>>.Ok(new List<FileEntryDto>(),
                        $"No files found with tag '{tagName}'.");

                var dtos = tag.Files.Select(f => new FileEntryDto
                {
                    FileName = f.FileName,
                    Tags = new List<string> { tagName }
                }).OrderBy(f => f.FileName).ToList();

                return ServiceResult<List<FileEntryDto>>.Ok(dtos,
                    $"Found {dtos.Count} file(s) with tag '{tagName}'.");
            }
            catch (Exception ex)
            {
                return ServiceResult<List<FileEntryDto>>.Fail($"Failed to search: {ex.Message}");
            }
        }

        public async Task<ServiceResult> RemoveTagFromFileAsync(string repositoryDirectory, string fileName, string tagName)
        {
            if (string.IsNullOrWhiteSpace(repositoryDirectory))
                return ServiceResult.Fail("Repository directory is required.");
            if (string.IsNullOrWhiteSpace(fileName))
                return ServiceResult.Fail("File name cannot be empty.");
            if (string.IsNullOrWhiteSpace(tagName))
                return ServiceResult.Fail("Tag name cannot be empty.");

            try
            {
                using var context = new FileTagDbContext(repositoryDirectory);
                if (!context.CheckDatabaseExists())
                    return ServiceResult.Fail("Database does not exist.");

                var file = await context.Files.Include(f => f.Tags)
                    .FirstOrDefaultAsync(f => f.FileName == fileName);
                if (file == null)
                    return ServiceResult.Fail($"File '{fileName}' not found in database.");

                var tagToRemove = file.Tags.FirstOrDefault(t => t.TagName == tagName);
                if (tagToRemove == null)
                    return ServiceResult.Fail($"Tag '{tagName}' not found on file '{fileName}'.");

                file.Tags.Remove(tagToRemove);
                await context.SaveChangesAsync();

                // Clean up orphaned tags
                var tagInDb = await context.Tags.Include(t => t.Files)
                    .FirstOrDefaultAsync(t => t.Id == tagToRemove.Id);
                if (tagInDb != null && tagInDb.Files.Count == 0)
                {
                    context.Tags.Remove(tagInDb);
                    await context.SaveChangesAsync();
                }

                return ServiceResult.Ok($"Tag '{tagName}' removed from '{fileName}'.");
            }
            catch (Exception ex)
            {
                return ServiceResult.Fail($"Failed to remove tag: {ex.Message}");
            }
        }

        public async Task<ServiceResult> DeleteFileAsync(string repositoryDirectory, string fileName)
        {
            if (string.IsNullOrWhiteSpace(repositoryDirectory))
                return ServiceResult.Fail("Repository directory is required.");
            if (string.IsNullOrWhiteSpace(fileName))
                return ServiceResult.Fail("File name cannot be empty.");

            try
            {
                using var context = new FileTagDbContext(repositoryDirectory);
                if (!context.CheckDatabaseExists())
                    return ServiceResult.Fail("Database does not exist.");

                var file = await context.Files.Include(f => f.Tags)
                    .FirstOrDefaultAsync(f => f.FileName == fileName);
                if (file == null)
                    return ServiceResult.Fail($"File '{fileName}' not found in database.");

                var associatedTags = file.Tags.ToList();
                context.Files.Remove(file);
                await context.SaveChangesAsync();

                // Clean up orphaned tags
                foreach (var tag in associatedTags)
                {
                    var tagInDb = await context.Tags.Include(t => t.Files)
                        .FirstOrDefaultAsync(t => t.Id == tag.Id);
                    if (tagInDb != null && tagInDb.Files.Count == 0)
                    {
                        context.Tags.Remove(tagInDb);
                    }
                }
                await context.SaveChangesAsync();

                return ServiceResult.Ok($"File '{fileName}' removed from database.");
            }
            catch (Exception ex)
            {
                return ServiceResult.Fail($"Failed to delete file: {ex.Message}");
            }
        }

        public async Task<ServiceResult<List<TagEntryDto>>> ListAllTagsAsync(string repositoryDirectory)
        {
            if (string.IsNullOrWhiteSpace(repositoryDirectory))
                return ServiceResult<List<TagEntryDto>>.Fail("Repository directory is required.");

            try
            {
                using var context = new FileTagDbContext(repositoryDirectory);
                if (!context.CheckDatabaseExists())
                    return ServiceResult<List<TagEntryDto>>.Fail("Database does not exist.");

                var tags = await context.Tags.OrderBy(t => t.TagName).ToListAsync();
                var dtos = tags.Select(t => new TagEntryDto { TagName = t.TagName }).ToList();
                return ServiceResult<List<TagEntryDto>>.Ok(dtos, $"Found {dtos.Count} tag(s).");
            }
            catch (Exception ex)
            {
                return ServiceResult<List<TagEntryDto>>.Fail($"Failed to list tags: {ex.Message}");
            }
        }
    }
}
