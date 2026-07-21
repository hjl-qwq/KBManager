using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KBManager.core;

namespace KBManager.CLI
{
    /// <summary>
    /// Main CLI application orchestrator.
    /// Wires up services, manages menu flows, and dispatches commands.
    /// All Console I/O is done through ConsoleUI / InteractiveMenu.
    /// </summary>
    public class CliApp
    {
        private readonly GitHelper _gitHelper;
        private readonly IKnowledgeBaseService _kbService;
        private readonly IFileScanService _fileScanService;
        private GitConfigModel _cachedConfig = new();

        public CliApp()
        {
            _gitHelper = new GitHelper();
            _kbService = new KnowledgeBaseService();
            _fileScanService = new FileScanService();
        }

        public async Task RunAsync()
        {
            // Ensure config is loaded early; create if missing on first run.
            _cachedConfig = _gitHelper.ReadGitConfig();
            if (!_cachedConfig.ValidateCoreConfig())
            {
                ConsoleUI.WriteHeader("Welcome to KBManager");
                ConsoleUI.WriteWarning("  No configuration found. Let's set up your environment first.");
                ConsoleUI.PressAnyKey();
                await RunSettingsAsync();
            }

            while (true)
            {
                ConsoleUI.WriteHeader("KBManager — Knowledge Base CLI");
                var menu = new InteractiveMenu("Main Menu", new List<MenuItem>
                {
                    new("search",    "🔍  Search Files by Tag",        "Find articles by tag"),
                    new("change",    "🏷   Change Files & Tags",       "Manage tags and sync"),
                    new("settings",  "⚙   Settings",                  "Configure user & repo"),
                    new("repo",      "📦  Repository Operations",     "Git clone, add, commit, push"),
                    new("exit",      "✕   Exit",                      null, ConsoleColor.DarkGray),
                });

                var choice = menu.Show();
                switch (choice)
                {
                    case "search":   await RunSearchAsync();   break;
                    case "change":   await RunChangeAsync();   break;
                    case "settings": await RunSettingsAsync(); break;
                    case "repo":     await RunRepoAsync();     break;
                    case "exit":
                    case "__back__":
                        ConsoleUI.WriteHeader("Goodbye!");
                        ConsoleUI.WriteInfo("  KBManager terminated.");
                        return;
                }
            }
        }

        // ========================================================================
        //  SEARCH
        // ========================================================================

        private async Task RunSearchAsync()
        {
            ConsoleUI.WriteHeader("Search Files by Tag");
            var tag = ConsoleUI.ReadLine("Enter tag to search for");
            if (string.IsNullOrWhiteSpace(tag))
            {
                ConsoleUI.WriteWarning("  Tag cannot be empty.");
                ConsoleUI.PressAnyKey();
                return;
            }

            var dir = await ResolveDirectoryAsync();
            if (dir == null) return;

            var result = await _kbService.SearchFilesByTagAsync(dir, tag);
            if (result.Success && result.Data != null && result.Data.Count > 0)
            {
                ConsoleUI.WriteSubHeader($"Results for tag: {tag}");
                ConsoleUI.WriteFileTable(result.Data, showIndex: false);
            }
            ConsoleUI.ShowServiceResult(result);
            ConsoleUI.PressAnyKey();
        }

        // ========================================================================
        //  CHANGE  (file / tag management)
        // ========================================================================

        private async Task RunChangeAsync()
        {
            var dir = await ResolveDirectoryAsync();
            if (dir == null) return;

            while (true)
            {
                ConsoleUI.WriteHeader("Change Files & Tags");

                // Always show current file list first (as spec requires)
                var listResult = await _kbService.ListFilesWithTagsAsync(dir);
                if (listResult.Success && listResult.Data != null)
                {
                    ConsoleUI.WriteFileTable(listResult.Data);
                }
                else
                {
                    ConsoleUI.ShowServiceResult(listResult);
                }

                Console.WriteLine();
                var menu = new InteractiveMenu("Operations", new List<MenuItem>
                {
                    new("select",   "📝  Select File to Edit Tags",  "Add or remove tags on a file"),
                    new("sync",     "🔄  Sync Files to Database",    "Scan repo and add new files"),
                    new("remove",   "🗑   Remove File from Database", "Not recommended — removes metadata only"),
                    MenuItem.Back,
                });

                var choice = menu.Show();
                switch (choice)
                {
                    case "select": await RunTagEditAsync(dir, listResult.Data); break;
                    case "sync":   await RunSyncAsync(dir);                     break;
                    case "remove": await RunRemoveFileAsync(dir, listResult.Data); break;
                    case "__back__": return;
                }
            }
        }

        private async Task RunTagEditAsync(string dir, List<FileEntryDto>? files)
        {
            var file = await PickFileAsync(dir, files, "Select a file to edit its tags");
            if (file == null) return;

            while (true)
            {
                ConsoleUI.WriteHeader($"Editing: {file.FileName}");
                ConsoleUI.WriteColor("  Tags: ", ConsoleColor.White);
                if (file.Tags.Count > 0)
                    ConsoleUI.WriteLineColor(string.Join(", ", file.Tags), ConsoleColor.Green);
                else
                    ConsoleUI.WriteDim("(none)");
                Console.WriteLine();

                var menu = new InteractiveMenu("Tag Operations", new List<MenuItem>
                {
                    new("add",    "➕  Add Tag",       $"Add a tag to '{Truncate(file.FileName, 30)}'"),
                    new("remove", "➖  Remove Tag",    $"Remove a tag from '{Truncate(file.FileName, 30)}'"),
                    MenuItem.Back,
                });

                var choice = menu.Show();
                switch (choice)
                {
                    case "add":
                        var newTag = ConsoleUI.ReadLine("Enter tag to add");
                        if (!string.IsNullOrWhiteSpace(newTag))
                        {
                            var r = await _kbService.AddTagToFileAsync(dir, file.FileName, newTag);
                            ConsoleUI.ShowServiceResult(r);
                        }
                        break;

                    case "remove":
                        if (file.Tags.Count == 0)
                        {
                            ConsoleUI.WriteWarning("  No tags to remove.");
                            break;
                        }
                        var rmTag = ConsoleUI.ReadLine("Enter tag to remove");
                        if (!string.IsNullOrWhiteSpace(rmTag))
                        {
                            var r = await _kbService.RemoveTagFromFileAsync(dir, file.FileName, rmTag);
                            ConsoleUI.ShowServiceResult(r);
                        }
                        break;

                    case "__back__":
                        return;
                }

                // Refresh file state
                var refresh = await _kbService.GetFileWithTagsAsync(dir, file.FileName);
                if (refresh.Success && refresh.Data != null) file = refresh.Data;
                ConsoleUI.PressAnyKey();
            }
        }

        private async Task RunSyncAsync(string dir)
        {
            ConsoleUI.WriteHeader("Sync Files to Database");
            ConsoleUI.WriteInfo("  Scanning repository for new files...");
            var result = await _fileScanService.BatchAddFilesToDatabaseAsync(dir);
            ConsoleUI.ShowServiceResult(result);
            ConsoleUI.PressAnyKey();
        }

        private async Task RunRemoveFileAsync(string dir, List<FileEntryDto>? files)
        {
            var file = await PickFileAsync(dir, files, "Select a file to remove from database");
            if (file == null) return;

            ConsoleUI.WriteHeader($"Remove File: {file.FileName}");
            ConsoleUI.WriteWarning("  ⚠  This only removes the database entry, NOT the actual file.");
            ConsoleUI.WriteWarning("  ⚠  This operation is not recommended unless the file no longer exists.");

            if (!ConsoleUI.Confirm("Are you sure you want to remove this entry?"))
            {
                ConsoleUI.WriteDim("  Cancelled.");
                ConsoleUI.PressAnyKey();
                return;
            }

            var result = await _kbService.DeleteFileAsync(dir, file.FileName);
            ConsoleUI.ShowServiceResult(result);
            ConsoleUI.PressAnyKey();
        }

        // ========================================================================
        //  SETTINGS
        // ========================================================================

        private Task RunSettingsAsync()
        {
            ConsoleUI.WriteHeader("Settings");
            ConsoleUI.WriteInfo("  Update all configuration fields. Leave blank to keep current value.");
            Console.WriteLine();

            var gcm = _gitHelper.ReadGitConfig();
            ShowCurrentConfig(gcm);

            Console.WriteLine();
            ConsoleUI.WriteDim("  Enter new values below:");
            Console.WriteLine();

            gcm.UserName           = ConsoleUI.ReadLine("User name")            ?? gcm.UserName;
            gcm.UserEmail          = ConsoleUI.ReadLine("User email")           ?? gcm.UserEmail;
            gcm.RemoteAddressHttps = ConsoleUI.ReadLine("HTTPS remote address") ?? gcm.RemoteAddressHttps;
            gcm.RemoteAddressSsh   = ConsoleUI.ReadLine("SSH remote address")   ?? gcm.RemoteAddressSsh;
            gcm.RepositoryDirectory = ConsoleUI.ReadLine("Local repo directory") ?? gcm.RepositoryDirectory;

            Console.WriteLine();
            if (_gitHelper.SaveGitConfig(gcm))
            {
                ConsoleUI.WriteSuccess("  ✓ Configuration saved successfully.");
                _cachedConfig = gcm;
            }
            else
            {
                ConsoleUI.WriteError("  ✗ Failed to save configuration.");
            }

            ConsoleUI.PressAnyKey();
            return Task.CompletedTask;
        }

        // ========================================================================
        //  REPOSITORY  (Git operations)
        // ========================================================================

        private Task RunRepoAsync()
        {
            while (true)
            {
                ConsoleUI.WriteHeader("Repository Operations");
                var menu = new InteractiveMenu("Git Operations", new List<MenuItem>
                {
                    new("clone",     "📥  Clone Repository",           "Clone remote repo + submodules"),
                    new("sub_add",   "📌  Submodule Add",              "Stage changes in all submodules"),
                    new("sub_commit","💾  Submodule Commit",           "Commit staged changes in submodules"),
                    new("add",       "📌  Main Repo Add",              "Stage changes in main repository"),
                    new("commit",    "💾  Main Repo Commit",           "Commit staged changes in main repo"),
                    new("push",      "🚀  Push All",                   "Push submodules then main repo (SSH)"),
                    new("status",    "📋  Show Config",                "View current Git config"),
                    MenuItem.Back,
                });

                var choice = menu.Show();
                switch (choice)
                {
                    case "clone":       RunClone();        break;
                    case "sub_add":     RunSubmoduleAdd(); break;
                    case "sub_commit":  RunSubmoduleCommit(); break;
                    case "add":         RunAdd();          break;
                    case "commit":      RunCommit();       break;
                    case "push":        RunPush();         break;
                    case "status":      ShowCurrentConfig(_gitHelper.ReadGitConfig()); ConsoleUI.PressAnyKey(); break;
                    case "__back__":    return Task.CompletedTask;
                }
            }
        }

        private void RunClone()
        {
            ConsoleUI.WriteHeader("Clone Repository");
            var config = _gitHelper.ReadGitConfig();
            ShowCurrentConfig(config);
            Console.WriteLine();

            if (!ConsoleUI.Confirm("Proceed with clone?")) return;

            ConsoleUI.WriteInfo("  Cloning...");
            bool ok = _gitHelper.CloneRepository(config);
            if (ok) ConsoleUI.WriteSuccess("  ✓ Clone successful.");
            else ConsoleUI.WriteError("  ✗ Clone failed. Check settings and network.");
            ConsoleUI.PressAnyKey();
        }

        private void RunAdd()
        {
            ConsoleUI.WriteHeader("Main Repo Add");
            var config = _gitHelper.ReadGitConfig();
            bool ok = _gitHelper.ExecuteGitAdd(config);
            if (ok) ConsoleUI.WriteSuccess("  ✓ Main repo files staged.");
            else ConsoleUI.WriteError("  ✗ Add failed.");
            ConsoleUI.PressAnyKey();
        }

        private void RunSubmoduleAdd()
        {
            ConsoleUI.WriteHeader("Submodule Add");
            var config = _gitHelper.ReadGitConfig();
            bool ok = _gitHelper.ExecuteSubmoduleAdd(config);
            if (ok) ConsoleUI.WriteSuccess("  ✓ Submodule changes staged.");
            else ConsoleUI.WriteError("  ✗ Submodule add failed.");
            ConsoleUI.PressAnyKey();
        }

        private void RunSubmoduleCommit()
        {
            ConsoleUI.WriteHeader("Submodule Commit");
            var config = _gitHelper.ReadGitConfig();
            var msg = ConsoleUI.ReadLine("Commit message");
            if (string.IsNullOrWhiteSpace(msg))
            {
                ConsoleUI.WriteError("  Commit message cannot be empty.");
                ConsoleUI.PressAnyKey();
                return;
            }

            var commitModel = new GitCommitModel { CommitMessage = msg };
            bool ok = _gitHelper.ExecuteSubmoduleCommit(config, commitModel);
            if (ok) ConsoleUI.WriteSuccess("  ✓ Submodules committed.");
            else ConsoleUI.WriteError("  ✗ Submodule commit failed.");
            ConsoleUI.PressAnyKey();
        }

        private void RunCommit()
        {
            ConsoleUI.WriteHeader("Git Commit");
            var config = _gitHelper.ReadGitConfig();
            var msg = ConsoleUI.ReadLine("Commit message");
            if (string.IsNullOrWhiteSpace(msg))
            {
                ConsoleUI.WriteError("  Commit message cannot be empty.");
                ConsoleUI.PressAnyKey();
                return;
            }

            var commitModel = new GitCommitModel { CommitMessage = msg };
            bool ok = _gitHelper.ExecuteGitCommit(config, commitModel);
            if (ok) ConsoleUI.WriteSuccess("  ✓ Commit successful.");
            else ConsoleUI.WriteError("  ✗ Commit failed.");
            ConsoleUI.PressAnyKey();
        }

        private void RunPush()
        {
            ConsoleUI.WriteHeader("Git Push (SSH)");
            var config = _gitHelper.ReadGitConfig();

            if (!ConsoleUI.Confirm("Push to remote via SSH?")) return;

            // ========== DO NOT MODIFY PUSH LOGIC ==========
            bool ok = _gitHelper.ExecuteGitPush(config);
            // ==============================================

            if (ok) ConsoleUI.WriteSuccess("  ✓ Push successful.");
            else ConsoleUI.WriteError("  ✗ Push failed. Check SSH key and remote.");
            ConsoleUI.PressAnyKey();
        }

        // ========================================================================
        //  HELPERS
        // ========================================================================

        /// <summary>
        /// Resolve the repository directory from cached config.
        /// If invalid, redirects user to Settings.
        /// </summary>
        private async Task<string?> ResolveDirectoryAsync()
        {
            _cachedConfig = _gitHelper.ReadGitConfig();
            if (!_cachedConfig.ValidateCoreConfig())
            {
                ConsoleUI.WriteError("  Configuration is incomplete. Redirecting to Settings...");
                ConsoleUI.PressAnyKey();
                await RunSettingsAsync();
                _cachedConfig = _gitHelper.ReadGitConfig();
                if (!_cachedConfig.ValidateCoreConfig())
                {
                    ConsoleUI.WriteError("  Cannot proceed without valid configuration.");
                    ConsoleUI.PressAnyKey();
                    return null;
                }
            }
            return _cachedConfig.RepositoryDirectory;
        }

        /// <summary>
        /// Interactive file picker. Returns the selected FileEntryDto or null.
        /// </summary>
        private Task<FileEntryDto?> PickFileAsync(string dir, List<FileEntryDto>? files, string title)
        {
            if (files == null || files.Count == 0)
            {
                ConsoleUI.WriteWarning("  No files in database. Try syncing first.");
                ConsoleUI.PressAnyKey();
                return Task.FromResult<FileEntryDto?>(null);
            }

            // Build a menu from the file list
            var items = files.Select((f, i) =>
                new MenuItem(
                    $"file_{i}",
                    Truncate(f.FileName, 45),
                    f.Tags.Count > 0 ? string.Join(", ", f.Tags.Take(3)) + (f.Tags.Count > 3 ? "..." : "") : "no tags",
                    f.Tags.Count > 0 ? ConsoleColor.Green : ConsoleColor.Gray
                )).ToList();

            items.Add(MenuItem.Back);

            ConsoleUI.WriteHeader(title);
            ConsoleUI.WriteFileTable(files);
            Console.WriteLine();

            var menu = new InteractiveMenu("Choose a file", items);
            var choice = menu.Show();

            if (choice == "__back__" || !choice.StartsWith("file_")) return Task.FromResult<FileEntryDto?>(null);

            var idx = int.Parse(choice.Replace("file_", ""));
            return Task.FromResult<FileEntryDto?>(files[idx]);
        }

        private static void ShowCurrentConfig(GitConfigModel config)
        {
            ConsoleUI.WriteLineColor("  -- Current Configuration --", ConsoleColor.DarkCyan);
            Console.WriteLine($"  User Name:        {config.UserName ?? "(not set)"}");
            Console.WriteLine($"  User Email:       {config.UserEmail ?? "(not set)"}");
            Console.WriteLine($"  HTTPS Remote:     {config.RemoteAddressHttps ?? "(not set)"}");
            Console.WriteLine($"  SSH Remote:       {config.RemoteAddressSsh ?? "(not set)"}");
            Console.WriteLine($"  Local Directory:  {config.RepositoryDirectory ?? "(not set)"}");
        }

        private static string Truncate(string value, int maxLen)
            => value.Length <= maxLen ? value : "..." + value[^(maxLen - 3)..];
    }
}
