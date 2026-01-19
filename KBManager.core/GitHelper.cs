using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using LibGit2Sharp;

namespace KBManager.core
{
    public class GitHelper
    {
        public string GitUserName { get; set; }
        public string GitUserEmail { get; set; }

        /// <summary>
        /// Check if directory empty
        /// </summary>
        /// <param name="directoryPath">target directory</param>
        /// <returns>true = Directory exist and not empty</returns>
        private bool IsDirectoryExistsAndNotEmpty(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                return false;
            }

            var dirInfo = new DirectoryInfo(directoryPath);
            return dirInfo.GetFiles().Length > 0 || dirInfo.GetDirectories().Length > 0;
        }

        /// <summary>
        /// Directory copy function (Fit Linux and Windows)
        /// </summary>
        /// <param name="sourceDir">Source directory</param>
        /// <param name="destDir">Target directory</param>
        /// <param name="overwrite">Overwrite or not</param>
        private static void CopyDirectoryCrossPlatform(string sourceDir, string destDir, bool overwrite)
        {
            if (!Directory.Exists(sourceDir))
            {
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
            }

            Directory.CreateDirectory(destDir);

            string[] files = Directory.GetFiles(sourceDir);
            string[] subDirs = Directory.GetDirectories(sourceDir);

            foreach (string file in files)
            {
                string destFilePath = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFilePath, overwrite);
            }

            foreach (string subDir in subDirs)
            {
                string destSubDirPath = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectoryCrossPlatform(subDir, destSubDirPath, overwrite);
            }
        }

        // Auto-detect SSH key path (prioritize ED25519 over RSA)
        private string GetSshKeyPath()
        {
            string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string sshDir = Path.Combine(userHome, ".ssh");

            // Check ED25519 key first (your key type)
            string ed25519Key = Path.Combine(sshDir, "id_ed25519");
            if (File.Exists(ed25519Key))
            {
                Console.WriteLine($"Detected ED25519 SSH key: {ed25519Key}");
                return ed25519Key;
            }

            // Fallback to RSA key (default)
            string rsaKey = Path.Combine(sshDir, "id_rsa");
            if (File.Exists(rsaKey))
            {
                Console.WriteLine($"Detected RSA SSH key: {rsaKey}");
                return rsaKey;
            }

            // If no auto-detected key, prompt user to input path
            Console.WriteLine("No default SSH key found (id_ed25519/id_rsa)");
            Console.Write("Enter full path to your SSH private key: ");
            string customKeyPath = Console.ReadLine()?.Trim();

            return string.IsNullOrEmpty(customKeyPath) ? string.Empty : customKeyPath;
        }

        public bool CloneRepository(GitConfigModel config)
        {
            if (config == null)
            {
                Console.WriteLine("Config object cannot be null.");
                return false;
            }

            if (!config.ValidateCloneConfig()) return false;

            if (IsDirectoryExistsAndNotEmpty(config.RepositoryDirectory))
            {
                Console.WriteLine($"Target directory is not empty: {config.RepositoryDirectory}");
                return false;
            }

            string parentDir = Path.GetDirectoryName(config.RepositoryDirectory);

            string tempDirectory = config.RepositoryDirectory + ".tmp_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            if (string.IsNullOrEmpty(config.RemoteAddressSsh))
            {
                Console.WriteLine("Ssh remote address is empty, use https");
                goto CloneViaHttps;
            }
            try
            {
                Repository.Clone(config.RemoteAddressSsh, tempDirectory);
                Console.WriteLine($"Repository cloned successfully from {config.RemoteAddressSsh} to: {tempDirectory}");
                CopyDirectoryCrossPlatform(
                    sourceDir: tempDirectory,
                    destDir: config.RepositoryDirectory,
                    overwrite: false
                );
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Clone failed from ssh address {config.RemoteAddressSsh}, try https");
                Console.WriteLine($"Clone failed: {ex.Message}");
                Console.WriteLine($"Full error details:\n{ex.ToString()}");
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.EnumerateFiles(tempDirectory, "*", SearchOption.AllDirectories).ToList().ForEach(file => File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly));
                    Directory.Delete(tempDirectory, true);
                }
            }

CloneViaHttps:

            try
            {
                Repository.Clone(config.RemoteAddressHttps, tempDirectory);
                Console.WriteLine($"Repository cloned successfully from {config.RemoteAddressHttps} to: {tempDirectory}");
                CopyDirectoryCrossPlatform(
                    sourceDir: tempDirectory,
                    destDir: config.RepositoryDirectory,
                    overwrite: false
                );
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Clone failed from https address {config.RemoteAddressHttps}");
                Console.WriteLine($"Clone failed: {ex.Message}");
                Console.WriteLine($"Full error details:\n{ex.ToString()}");
                return false;
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.EnumerateFiles(tempDirectory, "*", SearchOption.AllDirectories).ToList().ForEach(file => File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly));
                    Directory.Delete(tempDirectory, true);
                }
            }

        }

        public bool ExecuteGitAdd(GitConfigModel config)
        {
            if (string.IsNullOrEmpty(config.RepositoryDirectory))
            {
                Console.WriteLine("Error: RepositoryDirectory cannot be empty");
                return false;
            }

            try
            {
                using (var repo = new Repository(config.RepositoryDirectory))
                {
                    Commands.Stage(repo, "*");
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

        public bool ExecuteGitCommit(GitConfigModel gitConfig, GitCommitModel gitCommit)
        {
            if (!gitConfig.ValidateCoreConfig()) return false;
            if (string.IsNullOrEmpty(gitCommit.CommitMessage))
            {
                Console.WriteLine("Error: CommitMessage cannot be empty");
                return false;
            }

            try
            {
                using (var repo = new Repository(gitConfig.RepositoryDirectory))
                {
                    var status = repo.RetrieveStatus();
                    if (!status.IsDirty)
                    {
                        Console.WriteLine("No changes to commit (working directory clean)");
                        return true;
                    }

                    string finalUserName = !string.IsNullOrEmpty(gitConfig.UserName) ? gitConfig.UserName : "Temp CLI User";
                    string finalUserEmail = !string.IsNullOrEmpty(gitConfig.UserEmail) ? gitConfig.UserEmail : "temp-cli-user@example.com";

                    var author = new Signature(finalUserName, finalUserEmail, DateTimeOffset.Now);
                    var commit = repo.Commit(gitCommit.CommitMessage, author, author);

                    Console.WriteLine($"Commit successful! Commit ID: {commit.Sha.Substring(0, 7)}");
                    Console.WriteLine($"Commit message: {gitCommit.CommitMessage}");
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
        /// SSH Push with ED25519 key support (your key type)
        /// </summary>
        public bool ExecuteGitPush(GitConfigModel config)
        {
            if (string.IsNullOrEmpty(config.RepositoryDirectory))
            {
                Console.WriteLine("Error: RepositoryDirectory cannot be empty");
                return false;
            }

            if (string.IsNullOrEmpty(config.RemoteAddressSsh))
            {
                Console.WriteLine("Error: SSH remote address cannot be both empty");
                return false;
            }

            try
            {
                using (var repo = new Repository(config.RepositoryDirectory))
                {
                    // Reconfigure remote origin for SSH
                    if (repo.Network.Remotes["origin"] != null)
                    {
                        repo.Network.Remotes.Remove("origin");
                    }
                    var remote = repo.Network.Remotes.Add("origin", config.RemoteAddressSsh);
                    Console.WriteLine($"Configured remote origin (SSH): {config.RemoteAddressSsh}");

                    // Get your ED25519 SSH key path
                    string sshKeyPath = GetSshKeyPath();
                    if (string.IsNullOrEmpty(sshKeyPath) || !File.Exists(sshKeyPath))
                    {
                        Console.WriteLine($"Error: SSH key file not found at {sshKeyPath}");
                        return false;
                    }

                    // SSH push configuration (ED25519 compatible)
                    var pushOptions = new PushOptions
                    {
                        CredentialsProvider = (url, usernameFromUrl, types) =>
                        {
                            // Get passphrase for your ED25519 key (if set)
                            string passphrase = string.Empty;
                            Console.Write("Enter ED25519 SSH key passphrase (leave empty if none): ");

                            ConsoleKeyInfo key;
                            do
                            {
                                key = Console.ReadKey(true);
                                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                                {
                                    passphrase += key.KeyChar;
                                }
                                else if (key.Key == ConsoleKey.Backspace && passphrase.Length > 0)
                                {
                                    passphrase = passphrase.Substring(0, passphrase.Length - 1);
                                }
                            } while (key.Key != ConsoleKey.Enter);

                            Console.WriteLine();

                            // ED25519 key authentication (compatible with all LibGit2Sharp versions)
                            return new UsernamePasswordCredentials
                            {
                                Username = "git", // Fixed SSH username for Gitee
                                Password = string.IsNullOrEmpty(passphrase) ?
                                    File.ReadAllText(sshKeyPath) : passphrase
                            };
                        }
                    };

                    var branch = repo.Head;
                    if (branch == null)
                    {
                        Console.WriteLine("Error: No active branch found in repository");
                        return false;
                    }

                    // Execute SSH push with ED25519 key
                    repo.Network.Push(remote, $"refs/heads/{branch.FriendlyName}", pushOptions);

                    Console.WriteLine("Push operation completed successfully via SSH (ED25519 key)!");
                    Console.WriteLine($"Pushed branch: {branch.FriendlyName} to remote: {remote.Name}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to push changes via SSH: {ex.Message}");
                Console.WriteLine("\nTroubleshooting steps for ED25519 key:");
                Console.WriteLine("1. Verify ED25519 public key is added to Gitee: https://gitee.com/profile/sshkeys");
                Console.WriteLine("2. Test SSH connection: ssh -T git@gitee.com (should return 'Hi username!')");
                Console.WriteLine("3. Check ED25519 key permissions (chmod 600 ~/.ssh/id_ed25519 on Linux/Mac)");
                Console.WriteLine($"Full error details:\n{ex.ToString()}");
                return false;
            }
        }

        public bool SaveGitConfig(GitConfigModel gitConfig)
        {
            if (gitConfig == null)
            {
                throw new ArgumentNullException(nameof(gitConfig), "gitConfig cannot be null");
            }

            var configManager = new CrossPlatformConfig<GitConfigModel>("KBManager");

            if (!gitConfig.ValidateCoreConfig() || !gitConfig.ValidateCloneConfig())
            {
                Console.WriteLine("Cannot pass git config validation");
                return false;
            }

            try
            {
                configManager.WriteConfig(gitConfig);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Save GitConfig failed: {ex.Message}");
                Console.WriteLine($"Full error details:\n{ex.ToString()}");
                return false;
            }
        }

        public GitConfigModel ReadGitConfig()
        {
            var configManager = new CrossPlatformConfig<GitConfigModel>("KBManager");
            try
            {
                GitConfigModel gitConfig = configManager.ReadConfig();
                return gitConfig;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Read GitConfig failed: {ex.Message}");
                Console.WriteLine($"Full error details:\n{ex.ToString()}");
                return new GitConfigModel();
            }
        }
    }
}