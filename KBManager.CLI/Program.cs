using System;
using KBManager.core;

namespace KBManager.CLI
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("===== Git Operation CLI Tool =====");
            Console.WriteLine("Please select the Git operation to execute:");
            Console.WriteLine("1. Clone remote repository to local path");
            Console.WriteLine("2. Execute Git Add (stage all changed files)");
            Console.WriteLine("3. Execute Git Commit (commit staged changes)");
            Console.WriteLine("4. Execute Git Push (push commits to remote)");
            Console.WriteLine("5. Test save git config");
            Console.WriteLine("6. Test read git config");
            Console.WriteLine("7. Create database");
            Console.WriteLine("8. Add filename to database");
            Console.WriteLine("9. List filename in database");
            Console.WriteLine("10. Add tag to filename");
            Console.WriteLine("11. Find file through tag");
            Console.WriteLine("12. Remove tag from file");
            Console.WriteLine("13. Remove file");
            Console.WriteLine("14. Show all tag");
            Console.WriteLine("15. Exit");
            Console.WriteLine("===================================\n");

            // Main loop: keep receiving user input until exit is selected
            while (true)
            {
                Console.Write("Please enter operation number (1-15): ");
                var input = Console.ReadLine();
                if (!int.TryParse(input, out int choice) || choice < 1 || choice > 15)
                {
                    Console.WriteLine("Invalid input, please enter a number between 1 and 15!\n");
                    continue;
                }

                switch (choice)
                {
                    case 1:
                        ExecuteCloneOperation();
                        break;
                    case 2:
                        ExecuteAddOperation();
                        break;
                    case 3:
                        ExecuteCommitOperation();
                        break;
                    case 4:
                        ExecutePushOperation();
                        break;
                    case 5:
                        SaveGitConfig();
                        break;
                    case 6:
                        ReadGitConfig();
                        break;
                    case 7:
                        await ExecuteCreateDatabase();
                        break;
                    case 8:
                        await ExecuteAddFileToDatabase();
                        break;
                    case 9:
                        await ExecuteListAllFilesWithTagsAsync();
                        break;
                    case 10:
                        await ExecuteAddTagToFileAsync();
                        break;
                    case 11:
                        await ExecuteFindFileThroughTagAsync();
                        break;
                    case 12:
                        await ExecuteRemoveTagFromFileAsync();
                        break;
                    case 13:
                        await ExecuteDeleteFileAsync();
                        break;
                    case 14:
                        await ExecuteListAllTagsAsync();
                        break;
                    case 15:
                        Console.WriteLine("Exiting program...");
                        return;
                }

                Console.WriteLine("\n-------------------------\n"); // Operation separator
            }
        }

        /// <summary>
        /// Execute Git Clone operation interactively
        /// </summary>
        private static void ExecuteCloneOperation()
        {
            Console.WriteLine("\n===== Executing Git Clone Operation =====");

            var gitHelper = new GitHelper();

            GitConfigModel gitConfig = gitHelper.ReadGitConfig();

            bool result = gitHelper.CloneRepository(gitConfig);

            Console.WriteLine(result
                ? "Clone operation executed successfully!"
                : "Clone operation executed failed!");
        }

        /// <summary>
        /// Execute Git Add operation interactively
        /// </summary>
        private static void ExecuteAddOperation()
        {
            Console.WriteLine("\n===== Executing Git Add Operation =====");

            var gitHelper = new GitHelper();

            GitConfigModel gitConfig = gitHelper.ReadGitConfig();

            bool result = gitHelper.ExecuteGitAdd(gitConfig);

            Console.WriteLine(result
                ? "Add operation executed successfully!"
                : "Add operation executed failed!");
        }

        /// <summary>
        /// Execute Git Commit operation interactively
        /// </summary>
        private static void ExecuteCommitOperation()
        {
            Console.WriteLine("\n===== Executing Git Commit Operation =====");

            var gitHelper = new GitHelper();

            GitConfigModel gitConfig = gitHelper.ReadGitConfig();

            var gitCommit = new GitCommitModel();

            Console.Write("Please enter commit message: ");
            gitCommit.CommitMessage = Console.ReadLine()?.Trim();

            bool result = gitHelper.ExecuteGitCommit(gitConfig, gitCommit);

            Console.WriteLine(result
                ? "Commit operation executed successfully!"
                : "Commit operation executed failed!");
        }

        /// <summary>
        /// Execute Git Push operation interactively
        /// </summary>
        private static void ExecutePushOperation()
        {
            Console.WriteLine("\n===== Executing Git Push Operation =====");

            var gitHelper = new GitHelper();

            GitConfigModel gitConfig = gitHelper.ReadGitConfig();

            bool result = gitHelper.ExecuteGitPush(gitConfig);

            Console.WriteLine(result
                ? "Push operation executed successfully!"
                : "Push operation executed failed!");
        }

        private static void SaveGitConfig()
        {
            var gitConfig = new GitConfigModel();
            var gitHelper = new GitHelper();

            Console.Write("Please enter Git username: ");
            gitConfig.UserName = Console.ReadLine()?.Trim();

            Console.Write("Please enter Git email address: ");
            gitConfig.UserEmail = Console.ReadLine()?.Trim();

            Console.Write("Please enter remote repository URL in https (e.g. https://github.com/username/repo.git): ");
            gitConfig.RemoteAddressHttps = Console.ReadLine()?.Trim();

            Console.Write("Please enter remote repository URL in ssh (e.g. git@github.com:username/repo.git): ");
            gitConfig.RemoteAddressSsh = Console.ReadLine()?.Trim();

            Console.Write("Please enter local repository directory path (e.g. C:\\Git\\MyRepo): ");
            gitConfig.RepositoryDirectory = Console.ReadLine()?.Trim();

            if (gitHelper.SaveGitConfig(gitConfig))
            {
                Console.Write("Save git config successfully");
            }
            else
            {
                Console.Write("Save git config failed");
            }
        }

        private static void ReadGitConfig()
        {
            var gitHelper = new GitHelper();
            GitConfigModel gitConfig = gitHelper.ReadGitConfig();
            Console.Write($"Current user name: {gitConfig.UserName}\n");
            Console.Write($"Current user email: {gitConfig.UserEmail}\n");
            Console.Write($"Current remote repository URL in https: {gitConfig.RemoteAddressHttps}\n");
            Console.Write($"Current remote repository URL in ssh: {gitConfig.RemoteAddressSsh}\n");
            Console.Write($"Current repository directory: {gitConfig.RepositoryDirectory}\n");
        }

        private static async Task ExecuteCreateDatabase()
        {
            var dbHelper = new DbHelper();
            await dbHelper.CreateDb();
        }

        private static async Task ExecuteAddFileToDatabase()
        {
            Console.WriteLine("\n===== Executing Add File To Database Operation =====");

            Console.Write("Please enter the filename to add to database: ");
            string? inputFileName = Console.ReadLine();
            string fileName = inputFileName?.Trim() ?? string.Empty;

            var dbHelper = new DbHelper();
            bool result = await dbHelper.AddFileAsync(fileName);

            Console.WriteLine(result
                ? "Add filename to database successfully!"
                : "Add filename to database failed!");
        }

        private static async Task ExecuteListAllFilesWithTagsAsync()
        {
            Console.WriteLine("\n===== List filename in database =====");

            var dbHelper = new DbHelper();
            await dbHelper.ListAllFilesWithTagsAsync();

        }

        private static async Task ExecuteAddTagToFileAsync()
        {
            Console.WriteLine("\n===== Add tag to filename =====");
            var dbHelper = new DbHelper();
            await dbHelper.ListAllFilesWithTagsAsync();
            string? fileName, tag;
            Console.Write("Please enter filename: ");
            fileName = Console.ReadLine()?.Trim();

            Console.Write("Please enter tag: ");
            tag = Console.ReadLine()?.Trim();
            await dbHelper.AddTagToFileAsync(fileName, tag);
        }

        private static async Task ExecuteFindFileThroughTagAsync()
        {
            Console.WriteLine("\n===== Find file through tag =====");
            var dbHelper = new DbHelper();
            string? tag;
            Console.Write("Please enter tag: ");
            tag = Console.ReadLine()?.Trim();

            await dbHelper.SearchFilesByTagAsync(tag);
        }

        private static async Task ExecuteRemoveTagFromFileAsync()
        {
            var dbHelper = new DbHelper();
            string? fileName, tag;

            Console.Write("Please enter filename: ");
            fileName = Console.ReadLine()?.Trim();

            Console.Write("Please enter tag: ");
            tag = Console.ReadLine()?.Trim();

            await dbHelper.RemoveTagFromFileAsync(fileName, tag);
        }

        private static async Task ExecuteDeleteFileAsync()
        {
            var dbHelper = new DbHelper();
            string? fileName;

            Console.Write("Please enter filename: ");
            fileName = Console.ReadLine()?.Trim();

            await dbHelper.DeleteFileAsync(fileName);
        }

        private static async Task ExecuteListAllTagsAsync()
        {
            Console.WriteLine("\n===== List tags in database =====");

            var dbHelper = new DbHelper();
            await dbHelper.ListAllTagsAsync();
        }
    }
}