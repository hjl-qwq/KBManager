using System;
using KBManager.core;

namespace KBManager.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("===== Git Operation CLI Tool =====");
            Console.WriteLine("Please select the Git operation to execute:");
            Console.WriteLine("1. Clone remote repository to local path");
            Console.WriteLine("2. Execute Git Add (stage all changed files)");
            Console.WriteLine("3. Execute Git Commit (commit staged changes)");
            Console.WriteLine("4. Execute Git Push (push commits to remote)");
            Console.WriteLine("5. Exit");
            Console.WriteLine("===================================\n");

            while (true)
            {
                Console.Write("Please enter operation number (1-5): ");
                var input = Console.ReadLine();
                if (!int.TryParse(input, out int choice) || choice < 1 || choice > 5)
                {
                    Console.WriteLine("Invalid input, please enter a number between 1 and 5!\n");
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
                        Console.WriteLine("Exiting program...");
                        return;
                }

                Console.WriteLine("\n-------------------------\n");
            }
        }

        private static void ExecuteCloneOperation()
        {
            Console.WriteLine("\n===== Executing Git Clone Operation =====");
            var gitConfig = new GitConfigModel();

            try
            {
                Console.Write("Please enter remote repository URL (e.g. https://github.com/username/repo.git): ");
                gitConfig.RemoteAddress = Console.ReadLine()?.Trim();

                Console.Write("Please enter local clone path (e.g. C:\\Git\\MyRepo): ");
                gitConfig.RepositoryDirectory = Console.ReadLine()?.Trim();
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Validation Error: {ex.Message}");
                return;
            }

            var gitHelper = new GitHelper();
            bool result = gitHelper.CloneRepository(gitConfig);

            Console.WriteLine(result
                ? "Clone operation executed successfully!"
                : "Clone operation executed failed!");
        }

        private static void ExecuteAddOperation()
        {
            Console.WriteLine("\n===== Executing Git Add Operation =====");
            var gitConfig = new GitConfigModel();

            try
            {
                Console.Write("Please enter local repository directory path (e.g. C:\\Git\\MyRepo): ");
                gitConfig.RepositoryDirectory = Console.ReadLine()?.Trim();
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Validation Error: {ex.Message}");
                return;
            }

            var gitHelper = new GitHelper();
            bool result = gitHelper.ExecuteGitAdd(gitConfig);

            Console.WriteLine(result
                ? "Add operation executed successfully!"
                : "Add operation executed failed!");
        }

        private static void ExecuteCommitOperation()
        {
            Console.WriteLine("\n===== Executing Git Commit Operation =====");
            var gitConfig = new GitConfigModel();

            try
            {
                Console.Write("Please enter local repository directory path (e.g. C:\\Git\\MyRepo): ");
                gitConfig.RepositoryDirectory = Console.ReadLine()?.Trim();

                Console.Write("Please enter Git username: ");
                gitConfig.UserName = Console.ReadLine()?.Trim();

                Console.Write("Please enter Git email address: ");
                gitConfig.UserEmail = Console.ReadLine()?.Trim();

                Console.Write("Please enter commit message: ");
                gitConfig.CommitMessage = Console.ReadLine()?.Trim();
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Validation Error: {ex.Message}");
                return;
            }

            var gitHelper = new GitHelper();
            bool result = gitHelper.ExecuteGitCommit(gitConfig);

            Console.WriteLine(result
                ? "Commit operation executed successfully!"
                : "Commit operation executed failed!");
        }

        private static void ExecutePushOperation()
        {
            Console.WriteLine("\n===== Executing Git Push Operation =====");
            var gitConfig = new GitConfigModel();

            try
            {
                Console.Write("Please enter local repository directory path (e.g. C:\\Git\\MyRepo): ");
                gitConfig.RepositoryDirectory = Console.ReadLine()?.Trim();

                Console.Write("Please enter remote repository SSH URL (e.g. git@gitee.com:username/repo.git): ");
                gitConfig.RemoteAddress = Console.ReadLine()?.Trim();
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Validation Error: {ex.Message}");
                return;
            }

            var gitHelper = new GitHelper();
            bool result = gitHelper.ExecuteGitPush(gitConfig);

            Console.WriteLine(result
                ? "Push operation executed successfully!"
                : "Push operation executed failed!");
        }
    }
}
