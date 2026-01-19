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
    }
}