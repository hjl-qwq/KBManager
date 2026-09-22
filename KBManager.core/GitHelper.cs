using System;
using System.Collections.Generic;
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
        /// Optional secondary sink for Git progress output. The GUI attaches one so
        /// the Git panel can show live logs, instead of redirecting Console.Out for
        /// the whole process (which also captured unrelated writers).
        /// </summary>
        public TextWriter? LogSink { get; set; }

        /// <summary>
        /// When false, no interactive console prompt is ever issued. The GUI sets
        /// this so a missing SSH key fails fast instead of blocking on stdin.
        /// </summary>
        public bool Interactive { get; set; } = true;

        /// <summary>Write one line of progress to the console and the optional sink.</summary>
        private void Log(string message = "")
        {
            Console.WriteLine(message);
            LogSink?.WriteLine(message);
        }

        /// <summary>Write text with no trailing newline (used for prompts).</summary>
        private void LogRaw(string message)
        {
            Console.Write(message);
            LogSink?.Write(message);
        }

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
                Log($"Detected ED25519 SSH key: {ed25519Key}");
                return ed25519Key;
            }

            // Fallback to RSA key (default)
            string rsaKey = Path.Combine(sshDir, "id_rsa");
            if (File.Exists(rsaKey))
            {
                Log($"Detected RSA SSH key: {rsaKey}");
                return rsaKey;
            }

            // If no auto-detected key, prompt user to input path
            Log("No default SSH key found (id_ed25519/id_rsa)");
            if (!Interactive)
            {
                Log("No interactive console available; SSH key must be configured on disk.");
                return string.Empty;
            }

            LogRaw("Enter full path to your SSH private key: ");
            string customKeyPath = Console.ReadLine()?.Trim();

            return string.IsNullOrEmpty(customKeyPath) ? string.Empty : customKeyPath;
        }

        public bool CloneRepository(GitConfigModel config)
        {
            if (config == null)
            {
                Log("Config object cannot be null.");
                return false;
            }

            if (!config.ValidateCloneConfig()) return false;

            if (IsDirectoryExistsAndNotEmpty(config.RepositoryDirectory))
            {
                Log($"Target directory is not empty: {config.RepositoryDirectory}");
                return false;
            }

            string tempDirectory = config.RepositoryDirectory + ".tmp_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            if (string.IsNullOrEmpty(config.RemoteAddressSsh))
            {
                Log("Ssh remote address is empty, use https");
                goto CloneViaHttps;
            }
            try
            {
                CloneAndInitSubmodules(config.RemoteAddressSsh, tempDirectory);
                Log($"Repository cloned successfully from {config.RemoteAddressSsh} to: {tempDirectory}");
                CopyDirectoryCrossPlatform(
                    sourceDir: tempDirectory,
                    destDir: config.RepositoryDirectory,
                    overwrite: false
                );
                return true;
            }
            catch (Exception ex)
            {
                Log($"Clone failed from ssh address {config.RemoteAddressSsh}, try https");
                Log($"Clone failed: {ex.Message}");
                Log($"Full error details:\n{ex.ToString()}");
            }
            finally
            {
                CleanupTempDirectory(tempDirectory);
            }

CloneViaHttps:

            try
            {
                CloneAndInitSubmodules(config.RemoteAddressHttps, tempDirectory);
                Log($"Repository cloned successfully from {config.RemoteAddressHttps} to: {tempDirectory}");
                CopyDirectoryCrossPlatform(
                    sourceDir: tempDirectory,
                    destDir: config.RepositoryDirectory,
                    overwrite: false
                );
                return true;
            }
            catch (Exception ex)
            {
                Log($"Clone failed from https address {config.RemoteAddressHttps}");
                Log($"Clone failed: {ex.Message}");
                Log($"Full error details:\n{ex.ToString()}");
                return false;
            }
            finally
            {
                CleanupTempDirectory(tempDirectory);
            }

        }

        /// <summary>
        /// Clone a repository and initialize/update all submodules.
        /// Uses the same SSH key detection as ExecuteGitPush.
        /// </summary>
        private void CloneAndInitSubmodules(string remoteUrl, string localPath)
        {
            string sshKeyPath = GetSshKeyPath();
            bool hasSshKey = !string.IsNullOrEmpty(sshKeyPath) && File.Exists(sshKeyPath);

            var cloneOptions = new CloneOptions
            {
                RecurseSubmodules = false, // we handle submodules manually after clone
            };

            if (hasSshKey)
            {
                cloneOptions.FetchOptions.CredentialsProvider = (url, usernameFromUrl, types) =>
                    new UsernamePasswordCredentials
                    {
                        Username = "git",
                        Password = File.ReadAllText(sshKeyPath)
                    };
            }

            Repository.Clone(remoteUrl, localPath, cloneOptions);

            // Initialize and update submodules
            using (var repo = new Repository(localPath))
            {
                foreach (var submodule in repo.Submodules)
                {
                    Log($"Initializing submodule: {submodule.Name} ({submodule.Url})");
                    try
                    {
                        var updateOptions = new SubmoduleUpdateOptions
                        {
                            Init = true,
                        };

                        if (hasSshKey)
                        {
                            updateOptions.FetchOptions.CredentialsProvider = (url, usernameFromUrl, types) =>
                                new UsernamePasswordCredentials
                                {
                                    Username = "git",
                                    Password = File.ReadAllText(sshKeyPath)
                                };
                        }

                        repo.Submodules.Update(submodule.Name, updateOptions);
                        Log($"  Submodule '{submodule.Name}' initialized successfully.");
                    }
                    catch (Exception ex)
                    {
                        Log($"  Warning: failed to initialize submodule '{submodule.Name}': {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Recursively clear read-only attributes and delete a temp directory.
        /// </summary>
        private void CleanupTempDirectory(string tempDirectory)
        {
            if (!Directory.Exists(tempDirectory)) return;

            try
            {
                // Remove read-only attributes from .git contents recursively
                var dirInfo = new DirectoryInfo(tempDirectory);
                foreach (var fsInfo in dirInfo.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
                {
                    if ((fsInfo.Attributes & FileAttributes.ReadOnly) != 0)
                        fsInfo.Attributes &= ~FileAttributes.ReadOnly;
                }
                Directory.Delete(tempDirectory, true);
            }
            catch (Exception ex)
            {
                Log($"Warning: failed to clean up temp directory: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  Main-repo-only Add / Commit  (submodules handled separately)
        // ═══════════════════════════════════════════════════════════════

        public bool ExecuteGitAdd(GitConfigModel config)
        {
            if (string.IsNullOrEmpty(config.RepositoryDirectory))
            {
                Log("Error: RepositoryDirectory cannot be empty");
                return false;
            }

            try
            {
                using (var repo = new Repository(config.RepositoryDirectory))
                {
                    int staged = 0;
                    var status = repo.RetrieveStatus();
                    var submodulePathSet = new HashSet<string>(
                        repo.Submodules.Select(s => s.Path.Replace('\\', '/').TrimEnd('/')),
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var entry in status)
                    {
                        if (entry.State == FileStatus.Unaltered ||
                            entry.State == FileStatus.Ignored ||
                            entry.State == FileStatus.Nonexistent)
                            continue;

                        // Skip files inside submodule dirs, but stage the
                        // submodule pointer itself (e.g. "attachment").
                        string normalizedPath = entry.FilePath.Replace('\\', '/').TrimEnd('/');
                        if (submodulePathSet.Any(s => normalizedPath.StartsWith(s + "/")))
                            continue;

                        try
                        {
                            Commands.Stage(repo, entry.FilePath);
                            staged++;
                        }
                        catch (Exception ex)
                        {
                            Log($"  Skipped '{entry.FilePath}': {ex.Message}");
                        }
                    }

                    Log($"Staged {staged} file(s) in main repository");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to stage files: {ex.Message}");
                return false;
            }
        }

        public bool ExecuteGitCommit(GitConfigModel gitConfig, GitCommitModel gitCommit)
        {
            if (!gitConfig.ValidateCoreConfig()) return false;
            if (string.IsNullOrEmpty(gitCommit.CommitMessage))
            {
                Log("Error: CommitMessage cannot be empty");
                return false;
            }

            string finalUserName = !string.IsNullOrEmpty(gitConfig.UserName) ? gitConfig.UserName : "Temp CLI User";
            string finalUserEmail = !string.IsNullOrEmpty(gitConfig.UserEmail) ? gitConfig.UserEmail : "temp-cli-user@example.com";
            var author = new Signature(finalUserName, finalUserEmail, DateTimeOffset.Now);

            try
            {
                using (var repo = new Repository(gitConfig.RepositoryDirectory))
                {
                    if (!repo.RetrieveStatus().IsDirty)
                    {
                        Log("No changes to commit (working directory clean)");
                        return true;
                    }

                    var commit = repo.Commit(gitCommit.CommitMessage, author, author);
                    Log($"Commit successful! Commit ID: {commit.Sha[..7]}");
                    Log($"Commit message: {gitCommit.CommitMessage}");
                    Log($"User info: {finalUserName} <{finalUserEmail}>");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to commit changes: {ex.Message}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  Submodule-only Add / Commit
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Stage changes inside ALL submodules.</summary>
        public bool ExecuteSubmoduleAdd(GitConfigModel config)
        {
            if (string.IsNullOrEmpty(config.RepositoryDirectory))
            {
                Log("Error: RepositoryDirectory cannot be empty");
                return false;
            }

            try
            {
                using (var repo = new Repository(config.RepositoryDirectory))
                {
                    int total = 0;
                    foreach (var submodule in repo.Submodules)
                    {
                        string subPath = Path.GetFullPath(Path.Combine(config.RepositoryDirectory, submodule.Path));
                        if (!Repository.IsValid(subPath)) continue;
                        total += StageChangesInRepo(subPath, submodule.Name);
                    }

                    Log($"Submodule add complete — {total} file(s) staged across all submodules");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log($"Submodule add failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>Commit staged changes inside ALL submodules.</summary>
        public bool ExecuteSubmoduleCommit(GitConfigModel gitConfig, GitCommitModel gitCommit)
        {
            if (!gitConfig.ValidateCoreConfig()) return false;
            if (string.IsNullOrEmpty(gitCommit.CommitMessage))
            {
                Log("Error: CommitMessage cannot be empty");
                return false;
            }

            string finalUserName = !string.IsNullOrEmpty(gitConfig.UserName) ? gitConfig.UserName : "Temp CLI User";
            string finalUserEmail = !string.IsNullOrEmpty(gitConfig.UserEmail) ? gitConfig.UserEmail : "temp-cli-user@example.com";
            var author = new Signature(finalUserName, finalUserEmail, DateTimeOffset.Now);

            try
            {
                using (var repo = new Repository(gitConfig.RepositoryDirectory))
                {
                    int committed = 0;
                    foreach (var submodule in repo.Submodules)
                    {
                        string subPath = Path.GetFullPath(Path.Combine(gitConfig.RepositoryDirectory, submodule.Path));
                        if (!Repository.IsValid(subPath)) continue;

                        if (CommitIfDirty(subPath, submodule.Name, gitCommit.CommitMessage, author))
                            committed++;
                    }

                    Log($"Submodule commit complete — {committed} submodule(s) committed");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log($"Submodule commit failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>Commit inside a submodule if it has staged changes. Returns true if committed.</summary>
        private bool CommitIfDirty(string repoPath, string label, string message, Signature author)
        {
            try
            {
                using (var repo = new Repository(repoPath))
                {
                    if (!repo.RetrieveStatus().IsDirty) return false;

                    var subMsg = $"{message} [submodule: {label}]";
                    var commit = repo.Commit(subMsg, author, author);
                    Log($"  Submodule '{label}': committed {commit.Sha[..7]}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log($"  Submodule '{label}': commit skipped — {ex.Message}");
                return false;
            }
        }

        /// <summary>Stage all changes inside a repository (used for submodules).</summary>
        private int StageChangesInRepo(string repoPath, string label)
        {
            try
            {
                using (var repo = new Repository(repoPath))
                {
                    var status = repo.RetrieveStatus();
                    int count = 0;
                    foreach (var entry in status)
                    {
                        if (entry.State == FileStatus.Unaltered ||
                            entry.State == FileStatus.Ignored ||
                            entry.State == FileStatus.Nonexistent)
                            continue;

                        try { Commands.Stage(repo, entry.FilePath); count++; }
                        catch { /* skip */ }
                    }
                    if (count > 0)
                        Log($"  Submodule '{label}': staged {count} file(s)");
                    return count;
                }
            }
            catch (Exception ex)
            {
                Log($"  Submodule '{label}': {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// SSH Push with ED25519 key support.
        /// Pushes submodules first, then the main repository.
        /// </summary>
        /// <param name="config">Git configuration</param>
        /// <param name="passphrase">SSH key passphrase (empty if none). When null, prompts via console.</param>
        public bool ExecuteGitPush(GitConfigModel config, string? passphrase = null)
        {
            if (string.IsNullOrEmpty(config.RepositoryDirectory))
            {
                Log("Error: RepositoryDirectory cannot be empty");
                return false;
            }

            if (string.IsNullOrEmpty(config.RemoteAddressSsh))
            {
                Log("Error: SSH remote address cannot be both empty");
                return false;
            }

            // Pre-fetch SSH key so we only prompt for passphrase once
            string sshKeyPath = GetSshKeyPath();
            if (string.IsNullOrEmpty(sshKeyPath) || !File.Exists(sshKeyPath))
            {
                Log($"Error: SSH key file not found at {sshKeyPath}");
                return false;
            }

            string actualPassphrase = passphrase ?? ReadPassphrase();

            try
            {
                // ── Step 1: Push each submodule first ──
                using (var repo = new Repository(config.RepositoryDirectory))
                {
                    foreach (var submodule in repo.Submodules)
                    {
                        string subPath = Path.GetFullPath(Path.Combine(config.RepositoryDirectory, submodule.Path));
                        if (!Repository.IsValid(subPath)) continue;

                        PushSubmodule(subPath, submodule.Name, sshKeyPath, actualPassphrase);
                    }
                }

                // ── Step 2: Push main repo (original logic, unchanged) ──
                using (var repo = new Repository(config.RepositoryDirectory))
                {
                    // Reconfigure remote origin for SSH
                    if (repo.Network.Remotes["origin"] != null)
                    {
                        repo.Network.Remotes.Remove("origin");
                    }
                    var remote = repo.Network.Remotes.Add("origin", config.RemoteAddressSsh);
                    Log($"Configured remote origin (SSH): {config.RemoteAddressSsh}");

                    // SSH push configuration (ED25519 compatible)
                    var pushOptions = new PushOptions
                    {
                        CredentialsProvider = (url, usernameFromUrl, types) =>
                            new UsernamePasswordCredentials
                            {
                                Username = "git",
                                Password = string.IsNullOrEmpty(actualPassphrase) ?
                                    File.ReadAllText(sshKeyPath) : actualPassphrase
                            }
                    };

                    var branch = repo.Head;
                    if (branch == null)
                    {
                        Log("Error: No active branch found in repository");
                        return false;
                    }

                    // Execute SSH push with ED25519 key
                    repo.Network.Push(remote, $"refs/heads/{branch.FriendlyName}", pushOptions);

                    Log("Push operation completed successfully via SSH (ED25519 key)!");
                    Log($"Pushed branch: {branch.FriendlyName} to remote: {remote.Name}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to push changes via SSH: {ex.Message}");
                Log("\nTroubleshooting steps for ED25519 key:");
                Log("1. Verify ED25519 public key is added to remote: https://gitee.com/profile/sshkeys");
                Log("2. Test SSH connection: ssh -T git@gitee.com (should return 'Hi username!')");
                Log("3. Check ED25519 key permissions (chmod 600 ~/.ssh/id_ed25519 on Linux/Mac)");
                Log($"Full error details:\n{ex.ToString()}");
                return false;
            }
        }

        /// <summary>Push a single submodule using its own remote.</summary>
        private void PushSubmodule(string subPath, string label, string sshKeyPath, string passphrase)
        {
            try
            {
                using (var subRepo = new Repository(subPath))
                {
                    var subBranch = subRepo.Head;
                    if (subBranch == null) return;

                    var subRemote = subRepo.Network.Remotes["origin"];
                    if (subRemote == null)
                    {
                        Log($"  Submodule '{label}': no origin remote, skipping push");
                        return;
                    }

                    var pushOpts = new PushOptions
                    {
                        CredentialsProvider = (url, usernameFromUrl, types) =>
                            new UsernamePasswordCredentials
                            {
                                Username = "git",
                                Password = string.IsNullOrEmpty(passphrase) ?
                                    File.ReadAllText(sshKeyPath) : passphrase
                            }
                    };

                    subRepo.Network.Push(subRemote, $"refs/heads/{subBranch.FriendlyName}", pushOpts);
                    Log($"  Submodule '{label}': pushed {subBranch.FriendlyName}");
                }
            }
            catch (Exception ex)
            {
                Log($"  Submodule '{label}': push skipped — {ex.Message}");
            }
        }

        /// <summary>Read SSH passphrase once, shared across all push operations.</summary>
        private string ReadPassphrase()
        {
            if (!Interactive)
            {
                // No console to prompt on (GUI): assume an unencrypted key.
                Log("No interactive console available; assuming an unencrypted SSH key.");
                return string.Empty;
            }

            LogRaw("Enter ED25519 SSH key passphrase (leave empty if none): ");
            string passphrase = string.Empty;

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
                    passphrase = passphrase[..^1];
                }
            } while (key.Key != ConsoleKey.Enter);

            Log();
            return passphrase;
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
                Log("Cannot pass git config validation");
                return false;
            }

            try
            {
                configManager.WriteConfig(gitConfig);
                return true;
            }
            catch (Exception ex)
            {
                Log($"Save GitConfig failed: {ex.Message}");
                Log($"Full error details:\n{ex.ToString()}");
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
                Log($"Read GitConfig failed: {ex.Message}");
                Log($"Full error details:\n{ex.ToString()}");
                return new GitConfigModel();
            }
        }
    }
}