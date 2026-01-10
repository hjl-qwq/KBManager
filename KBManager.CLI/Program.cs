using System;
using KBManager.core;

namespace KBManager.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            // 1. Initialize Git configuration model (centralized management of all parameters)
            var gitConfig = new GitConfigModel
            {
                UserName = "Your Git Username",       // Replace with your actual username
                UserEmail = "your.email@example.com", // Replace with your actual email
                RemoteAddress = "https://github.com/your-username/your-repo.git", // Replace with remote address
                RepositoryDirectory = @"C:\Your\Local\Repo\Path", // Replace with local repository path
                CommitMessage = "Update: Add new features" // Custom commit message
            };

            // 2. Initialize GitHelper
            var gitHelper = new GitHelper();

            // 3. Example 1: Independent call Git Add operation
            Console.WriteLine("=== Execute Git Add Operation Independently ===");
            bool addResult = gitHelper.ExecuteGitAdd(gitConfig);
            if (addResult)
            {
                Console.WriteLine("Git Add operation completed successfully");
            }
            else
            {
                Console.WriteLine("Git Add operation failed");
            }

            // 4. Example 2: Independent call Git Commit operation (can be called separately at any time)
            Console.WriteLine("\n=== Execute Git Commit Operation Independently ===");
            bool commitResult = gitHelper.ExecuteGitCommit(gitConfig);
            if (commitResult)
            {
                Console.WriteLine("Git Commit operation completed successfully");
            }
            else
            {
                Console.WriteLine("Git Commit operation failed");
            }

            // Optional: Test Clone operation (use GitConfigModel)
            //Console.WriteLine("\n=== Execute Git Clone Operation ===");
            //bool cloneResult = gitHelper.CloneRepository(gitConfig);
            //if (cloneResult)
            //{
            //    Console.WriteLine("Git Clone operation completed successfully");
            //}
            //else
            //{
            //    Console.WriteLine("Git Clone operation failed");
            //}

            // Keep console window open
            Console.WriteLine($"\nAll specified operations completed.");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        // Keep the original test methods (compatible with old logic, optional to delete)
        static void TestGitClone()
        {
            try
            {
                var gitHelper = new GitHelper();
                string repoUrl = "Your remote repository (only support http yet)";
                string localPath = @"Repository directory on your desktop";

                Console.WriteLine("Start executing Git Clone operation...");
                bool isSuccess = gitHelper.CloneRepository(repoUrl, localPath);
                Console.WriteLine($"Git Clone operation completed, success status: {isSuccess}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Git Clone execution exception: {ex.Message}");
                Console.WriteLine($"Exception details: {ex.ToString()}");
            }
        }

        static void TestGitAddAndCommit()
        {
            try
            {
                var gitHelper = new GitHelper();
                string localRepoPath = @"Repository directory on your desktop";
                string tempUserName = "Your user name";
                string tempUserEmail = "Your email";
                string commitMessage = "Your commit message";

                Console.WriteLine("\nStart executing Git Add . operation...");
                bool addSuccess = gitHelper.AddAllFiles(localRepoPath);
                if (!addSuccess)
                {
                    Console.WriteLine("Git Add . operation failed, terminate subsequent Commit process");
                    return;
                }
                Console.WriteLine("Git Add . operation succeeded");

                Console.WriteLine("\nStart executing Git Commit operation...");
                bool commitSuccess = gitHelper.CommitChanges(localRepoPath, commitMessage, tempUserName, tempUserEmail);

                if (commitSuccess)
                {
                    Console.WriteLine($"Git Commit operation succeeded!");
                    Console.WriteLine($"Commit message: {commitMessage}");
                    Console.WriteLine($"本次提交使用的用户信息：{tempUserName} <{tempUserEmail}>");
                }
                else
                {
                    Console.WriteLine("Git Commit operation failed (no changes to commit possibly)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred while executing Git Add/Commit: {ex.Message}");
                Console.WriteLine($"Exception details: {ex.ToString()}");
            }
        }
    }
}