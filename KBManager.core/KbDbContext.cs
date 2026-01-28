using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.IO;
using System;
using System.Threading.Tasks;

namespace KBManager.core
{
    public class FileInfo
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string FileName { get; set; }

        public ICollection<Tag> Tags { get; set; } = new List<Tag>();
    }

    public class Tag
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string TagName { get; set; }

        public ICollection<FileInfo> Files { get; set; } = new List<FileInfo>();
    }

    public class FileTagDbContext : DbContext
    {
        private readonly string _dbDirectory;
        private const string DbSubDirectoryName = ".kbdatabase";
        private const string DbName = "KbInfo.db";

        public DbSet<FileInfo> Files { get; set; }
        public DbSet<Tag> Tags { get; set; }

        public string RepositoryToDbDirectory(string repositoryDirectory)
        {
            return Path.Combine(repositoryDirectory, DbSubDirectoryName);
        }

        public FileTagDbContext(string repositoryDirectory)
        {
            _dbDirectory = RepositoryToDbDirectory(repositoryDirectory);

            if (!Directory.Exists(_dbDirectory))
            {
                Directory.CreateDirectory(_dbDirectory);
            }
        }

        public bool CheckDatabseExists()
        {
            return File.Exists(Path.Combine(_dbDirectory, DbName));
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string dbPath = Path.Combine(_dbDirectory, DbName);
            optionsBuilder.UseSqlite($"Data Source={dbPath};Cache=Private;Pooling=False");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FileInfo>().HasIndex(f => f.FileName).IsUnique();
            modelBuilder.Entity<Tag>().HasIndex(t => t.TagName).IsUnique();

            modelBuilder.Entity<FileInfo>()
                .HasMany(f => f.Tags)
                .WithMany(t => t.Files)
                .UsingEntity(j => j.ToTable("FileTagRelations"));
        }

        public async Task<bool> CreateDatabaseAsync()
        {
            string dbPath = Path.Combine(_dbDirectory, DbName);
            bool isDbFileExists = File.Exists(dbPath);

            Database.CloseConnection();

            if (!isDbFileExists)
            {
                await Database.EnsureDeletedAsync();
                await Database.EnsureCreatedAsync();
                return true;
            }

            return false;
        }
    }
}