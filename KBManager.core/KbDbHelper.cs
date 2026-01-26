using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    }
}