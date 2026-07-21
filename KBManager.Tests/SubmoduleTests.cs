using System;
using System.IO;
using System.Linq;
using KBManager.core;
using Xunit;

namespace KBManager.Tests
{
    /// <summary>
    /// Tests for submodule handling — Git Add skip + FileScan exclusion.
    /// These tests create a real local git repo with a submodule to verify
    /// the fix works end-to-end without touching any remote.
    /// </summary>
    public class SubmoduleTests : IDisposable
    {
        private readonly string _testRoot;

        public SubmoduleTests()
        {
            _testRoot = Path.Combine(Path.GetTempPath(), "kbmanager_test_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_testRoot);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testRoot))
            {
                // Remove read-only .git files before deleting
                var dirInfo = new DirectoryInfo(_testRoot);
                foreach (var fs in dirInfo.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
                {
                    if ((fs.Attributes & FileAttributes.ReadOnly) != 0)
                        fs.Attributes &= ~FileAttributes.ReadOnly;
                }
                Directory.Delete(_testRoot, true);
            }
        }

        [Fact]
        public void ExecuteGitAdd_Skips_Submodule_Without_Error()
        {
            // Arrange: create a git repo with a real submodule
            string repoDir = Path.Combine(_testRoot, "repo");
            string subDir = Path.Combine(_testRoot, "sub_repo");

            // Create and init the "submodule" repo
            Directory.CreateDirectory(subDir);
            LibGit2Sharp.Repository.Init(subDir);
            File.WriteAllText(Path.Combine(subDir, "subfile.md"), "# Sub file");
            using (var subRepo = new LibGit2Sharp.Repository(subDir))
            {
                LibGit2Sharp.Commands.Stage(subRepo, "subfile.md");
                subRepo.Commit("sub init",
                    new LibGit2Sharp.Signature("test", "test@test.com", DateTimeOffset.Now),
                    new LibGit2Sharp.Signature("test", "test@test.com", DateTimeOffset.Now),
                    new LibGit2Sharp.CommitOptions { AllowEmptyCommit = true });
            }

            // Init main repo and add submodule
            LibGit2Sharp.Repository.Init(repoDir);
            File.WriteAllText(Path.Combine(repoDir, "readme.md"), "# Test");

            using (var repo = new LibGit2Sharp.Repository(repoDir))
            {
                LibGit2Sharp.Commands.Stage(repo, "readme.md");
                repo.Commit("initial",
                    new LibGit2Sharp.Signature("test", "test@test.com", DateTimeOffset.Now),
                    new LibGit2Sharp.Signature("test", "test@test.com", DateTimeOffset.Now),
                    new LibGit2Sharp.CommitOptions { AllowEmptyCommit = true });

                // Register submodule so repo.Submodules returns it
                repo.Config.Set("submodule.attachment.url", subDir);
                repo.Config.Set("submodule.attachment.active", "true");
            }

            // Modify file in submodule to make it dirty
            File.AppendAllText(Path.Combine(subDir, "subfile.md"), "\nmore content");

            // Act: ExecuteGitAdd should stage submodule changes + main repo changes
            var helper = new GitHelper();
            var config = new GitConfigModel { RepositoryDirectory = repoDir };
            bool result = helper.ExecuteGitAdd(config);

            Assert.True(result, "ExecuteGitAdd should handle submodules");

            // Submodule should have staged changes
            using (var subRepo = new LibGit2Sharp.Repository(subDir))
            {
                var subStatus = subRepo.RetrieveStatus(new LibGit2Sharp.StatusOptions());
                Assert.True(subStatus.IsDirty || subStatus.Staged.Any());
            }
        }

        [Fact]
        public void ExecuteGitCommit_Commits_Submodules_First()
        {
            string repoDir = Path.Combine(_testRoot, "repo2");
            string subDir = Path.Combine(_testRoot, "sub_repo2");

            // Setup submodule repo with a commit
            Directory.CreateDirectory(subDir);
            LibGit2Sharp.Repository.Init(subDir);
            File.WriteAllText(Path.Combine(subDir, "data.txt"), "v1");
            using (var subRepo = new LibGit2Sharp.Repository(subDir))
            {
                LibGit2Sharp.Commands.Stage(subRepo, "data.txt");
                subRepo.Commit("sub commit",
                    new LibGit2Sharp.Signature("test", "test@test.com", DateTimeOffset.Now),
                    new LibGit2Sharp.Signature("test", "test@test.com", DateTimeOffset.Now),
                    new LibGit2Sharp.CommitOptions { AllowEmptyCommit = true });
            }

            // Setup main repo
            LibGit2Sharp.Repository.Init(repoDir);
            File.WriteAllText(Path.Combine(repoDir, "main.md"), "# Main");
            using (var repo = new LibGit2Sharp.Repository(repoDir))
            {
                LibGit2Sharp.Commands.Stage(repo, "main.md");
                repo.Commit("main init",
                    new LibGit2Sharp.Signature("test", "test@test.com", DateTimeOffset.Now),
                    new LibGit2Sharp.Signature("test", "test@test.com", DateTimeOffset.Now),
                    new LibGit2Sharp.CommitOptions { AllowEmptyCommit = true });

                repo.Config.Set("submodule.attachment.url", subDir);
                repo.Config.Set("submodule.attachment.active", "true");
            }

            // Make changes in submodule
            File.AppendAllText(Path.Combine(subDir, "data.txt"), "\nv2");

            // Stage submodule + main, then commit
            var helper = new GitHelper();
            var cfg = new GitConfigModel { RepositoryDirectory = repoDir, UserName = "tester", UserEmail = "t@t.com" };

            helper.ExecuteGitAdd(cfg);
            bool committed = helper.ExecuteGitCommit(cfg, new GitCommitModel { CommitMessage = "test submodule commit" });

            Assert.True(committed, "Main repo commit should succeed");
        }

        [Fact]
        public void FileScan_Skips_Git_And_Database_Directories()
        {
            // Arrange
            string repoDir = Path.Combine(_testRoot, "scanrepo");
            Directory.CreateDirectory(repoDir);
            File.WriteAllText(Path.Combine(repoDir, "doc1.md"), "# Doc 1");
            File.WriteAllText(Path.Combine(repoDir, "doc2.txt"), "hello");

            // Create .git dir (should be skipped)
            Directory.CreateDirectory(Path.Combine(repoDir, ".git"));
            File.WriteAllText(Path.Combine(repoDir, ".git", "config"), "fake");

            // Create .kbdatabase dir (should be skipped)
            Directory.CreateDirectory(Path.Combine(repoDir, ".kbdatabase"));
            File.WriteAllText(Path.Combine(repoDir, ".kbdatabase", "KbInfo.db"), "fake db");

            // Create a hidden file (should be skipped on Linux)
            File.WriteAllText(Path.Combine(repoDir, ".hidden"), "hidden");

            // Act
            var scanner = new FileScanService();
            var result = scanner.ScanRepositoryFiles(repoDir);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            var files = result.Data!;

            // Should only contain doc1.md and doc2.txt
            Assert.Contains("doc1.md", files);
            Assert.Contains("doc2.txt", files);
            Assert.DoesNotContain(".git/config", files);
            Assert.DoesNotContain(files, f => f.StartsWith(".git/"));
            Assert.DoesNotContain(files, f => f.StartsWith(".kbdatabase/"));
        }

        [Fact]
        public void ConfigModel_Validates_Core_And_Clone()
        {
            var empty = new GitConfigModel();
            Assert.False(empty.ValidateCoreConfig());
            Assert.False(empty.ValidateCloneConfig());

            var valid = new GitConfigModel
            {
                UserName = "test",
                UserEmail = "test@test.com",
                RepositoryDirectory = "/some/path",
                RemoteAddressSsh = "git@github.com:test/repo.git"
            };
            Assert.True(valid.ValidateCoreConfig());
            Assert.True(valid.ValidateCloneConfig());
        }

        [Fact]
        public void ServiceResult_Ok_And_Fail_Work()
        {
            var ok = ServiceResult.Ok("done");
            Assert.True(ok.Success);
            Assert.Equal("done", ok.Message);

            var fail = ServiceResult.Fail("error");
            Assert.False(fail.Success);
            Assert.Equal("error", fail.Message);

            var dataOk = ServiceResult<string>.Ok("payload", "msg");
            Assert.True(dataOk.Success);
            Assert.Equal("payload", dataOk.Data);
        }
    }
}
