using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace KBManager.core
{
    public class DbHelper
    {
        public async Task CreateDb()
        {
            Console.WriteLine("Start create database");
            var gitHelper = new GitHelper();
            GitConfigModel gitConfig = gitHelper.ReadGitConfig();

            try
            {
                using (var context = new FileTagDbContext(gitConfig.RepositoryDirectory))
                {
                    bool isCreated = await context.CreateDatabaseAsync();

                    if (isCreated)
                    {
                        Console.WriteLine("Database created successfully");
                    }
                    else
                    {
                        Console.WriteLine("Database already exist");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create database: {ex.Message}");
            }
        }

        public async Task<bool> AddFileAsync(string fileName)
        {
            Console.WriteLine("Start add FileInfo");
            var gitHelper = new GitHelper();
            GitConfigModel gitConfig = gitHelper.ReadGitConfig();

            if (string.IsNullOrWhiteSpace(fileName))
            {
                Console.WriteLine("File name cannot be empty or whitespace");
                return false;
            }

            try
            {
                using (var context = new FileTagDbContext(gitConfig.RepositoryDirectory))
                {
                    if (!context.CheckDatabseExists())
                    {
                        Console.WriteLine("There's no database, create one first");
                        return false;
                    }

                    bool isFileExists = await context.Files.AnyAsync(f => f.FileName == fileName);
                    if (isFileExists)
                    {
                        Console.WriteLine($"File '{fileName}' already exists in database");
                        return false;
                    }

                    var newFile = new FileInfo
                    {
                        FileName = fileName,
                    };

                    context.Files.Add(newFile);
                    await context.SaveChangesAsync();

                    Console.WriteLine($"File '{fileName}' added successfully");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to add file: {ex.Message}");
                return false;
            }
        }

        public async Task ListAllFilesWithTagsAsync()
        {
            Console.WriteLine("Start listing all files with tags");
            var gitHelper = new GitHelper();
            GitConfigModel gitConfig = gitHelper.ReadGitConfig();

            try
            {
                using (var context = new FileTagDbContext(gitConfig.RepositoryDirectory))
                {
                    if (!context.CheckDatabseExists())
                    {
                        Console.WriteLine("There's no database, create one first");
                        return;
                    }

                    var filesWithTags = await context.Files
                        .Include(f => f.Tags)
                        .ToListAsync();

                    if (filesWithTags.Count == 0)
                    {
                        Console.WriteLine("No files found in database");
                        return;
                    }

                    Console.WriteLine("=====================================");
                    foreach (var file in filesWithTags)
                    {
                        Console.WriteLine($"File Name: {file.FileName}");

                        if (file.Tags == null || file.Tags.Count == 0)
                        {
                            Console.WriteLine("Tags: None");
                        }
                        else
                        {
                            Console.WriteLine($"Tags: {string.Join(", ", file.Tags.Select(t => t.TagName))}");
                        }
                        Console.WriteLine("-------------------------------------");
                    }
                    Console.WriteLine("=====================================");
                    Console.WriteLine($"Total files listed: {filesWithTags.Count}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to list files with tags: {ex.Message}");
            }
        }
    }
}