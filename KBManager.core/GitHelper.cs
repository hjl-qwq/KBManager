using System;
using System.Net;
using LibGit2Sharp;

namespace KBManager.core
{
    public class GitHelper
    {
        // Reserved user info configuration fields (compatible with original logic)
        public string GitUserName { get; set; }
        public string GitUserEmail { get; set; }

        /// <summary>
        /// Clone github repository to local path
        /// </summary>
        /// <param name="config">Git configuration model</param>
        /// <returns>Whether the clone operation is successful</returns>
        public bool CloneRepository(GitConfigModel config)
        {
            if (!config.ValidateCloneConfig())
            {
                return false;
            }

            try
            {
                // Core fix: Enable only TLS 1.2 (compatible with all runtimes supporting .NET Standard 2.0)
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                // Optional: Temporarily disable certificate validation (only for test environment, remove in production!)
                // ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

                // Core clone operation
                Repository.Clone(config.RemoteAddress, config.RepositoryDirectory);
                Console.WriteLine($"Repository cloned successfully to: {config.RepositoryDirectory}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Clone failed: {ex.Message}");
                // Print full exception info for troubleshooting
                Console.WriteLine($"Full error details:\n{ex.ToString()}");
                return false;
            }
        }

        /// <summary>
        /// Execute git add . operation (add all changed files)
        /// Independent call interface, return success/failure status
        /// </summary>
        /// <param name="config">Git configuration model</param>
        /// <returns>Whether the add operation is successful</returns>
        public bool ExecuteGitAdd(GitConfigModel config)
        {
            if (string.IsNullOrEmpty(config.RepositoryDirectory))
            {
                Console.WriteLine("Error: RepositoryDirectory cannot be empty");
                return false;
            }

            try
            {
                // Open local repository
                using (var repo = new Repository(config.RepositoryDirectory))
                {
                    // Execute git add . (add all untracked/modified files)
                    Commands.Stage(repo, "*"); // "*" equivalent to git add .
                    Console.WriteLine("All files staged successfully (git add .)");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to stage files: {ex.Message}");
                Console.WriteLine($"Full error details:\n{ex.ToString()}");
                return false;
            }
        }

        /// <summary>
        /// Execute git commit operation (independent call interface)
        /// Only rely on GitConfigModel for parameter passing, no modification to any Git configuration files
        /// </summary>
        /// <param name="config">Git configuration model</param>
        /// <returns>Whether the commit operation is successful</returns>
        public bool ExecuteGitCommit(GitConfigModel config)
        {
            if (!config.ValidateCoreConfig())
            {
                return false;
            }

            if (string.IsNullOrEmpty(config.CommitMessage))
            {
                Console.WriteLine("Error: CommitMessage cannot be empty");
                return false;
            }

            try
            {
                using (var repo = new Repository(config.RepositoryDirectory))
                {
                    // Check if there are staged changes
                    var status = repo.RetrieveStatus();
                    if (!status.IsDirty)
                    {
                        Console.WriteLine("No changes to commit (working directory clean)");
                        return true; // No changes count as "success" (avoid error)
                    }

                    // Use configuration model's user info (temporary, valid only for this run)
                    string finalUserName = !string.IsNullOrEmpty(config.UserName) ? config.UserName : "Temp CLI User";
                    string finalUserEmail = !string.IsNullOrEmpty(config.UserEmail) ? config.UserEmail : "temp-cli-user@example.com";

                    // Build committer info (valid only at runtime, not written to any config files)
                    var author = new Signature(
                        finalUserName,
                        finalUserEmail,
                        DateTimeOffset.Now
                    );

                    // Execute commit
                    var commit = repo.Commit(config.CommitMessage, author, author);
                    Console.WriteLine($"Commit successful! Commit ID: {commit.Sha.Substring(0, 7)}");
                    Console.WriteLine($"Commit message: {config.CommitMessage}");
                    Console.WriteLine($"User info: {finalUserName} <{finalUserEmail}>");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to commit changes: {ex.Message}");
                Console.WriteLine($"Full error details:\n{ex.ToString()}");
                return false;
            }
        }

        /// <summary>
        /// Manually configure repository-level user.name and user.email (optional: can also configure globally)
        /// </summary>
        /// <param name="config">Git configuration model</param>
        /// <returns>Whether the configuration is successful</returns>
        public bool ConfigureGitUser(GitConfigModel config)
        {
            if (!config.ValidateCoreConfig())
            {
                return false;
            }

            try
            {
                using (var repo = new Repository(config.RepositoryDirectory))
                {
                    // Configure repository-level user info (override/set)
                    repo.Config.Set("user.name", config.UserName);
                    repo.Config.Set("user.email", config.UserEmail);

                    // Optional: Configure global-level user info (uncomment to enable)
                    // repo.Config.Set("user.name", config.UserName, true);
                    // repo.Config.Set("user.email", config.UserEmail, true);

                    // Sync config to instance fields
                    GitUserName = config.UserName;
                    GitUserEmail = config.UserEmail;

                    Console.WriteLine($"Git user configured: {config.UserName} <{config.UserEmail}>");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to configure git user: {ex.Message}");
                Console.WriteLine($"Full error details:\n{ex.ToString()}");
                return false;
            }
        }

        // Keep the original CloneRepository method (compatible with old logic)
        public bool CloneRepository(string repositoryUrl, string localPath)
        {
            var config = new GitConfigModel
            {
                RemoteAddress = repositoryUrl,
                RepositoryDirectory = localPath
            };
            return CloneRepository(config);
        }

        // Keep the original AddAllFiles method (compatible with old logic)
        public bool AddAllFiles(string localRepoPath)
        {
            var config = new GitConfigModel
            {
                RepositoryDirectory = localRepoPath
            };
            return ExecuteGitAdd(config);
        }

        // Keep the original CommitChanges method (compatible with old logic)
        public bool CommitChanges(string localRepoPath, string commitMessage, string userName = null, string userEmail = null)
        {
            var config = new GitConfigModel
            {
                RepositoryDirectory = localRepoPath,
                CommitMessage = commitMessage,
                UserName = userName,
                UserEmail = userEmail
            };
            return ExecuteGitCommit(config);
        }
    }
}