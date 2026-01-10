using System;
using KBManager.core;

namespace KBManager.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            // Initialize GitHelper from KBManager.core
            var gitHelper = new GitHelper();

            // Replace with your github repository URL
            string repoUrl = "your github repository URL";

            // Replace with your local path
            string localPath = @"your local path";

            // Execute clone operation
            bool isSuccess = gitHelper.CloneRepository(repoUrl, localPath);

            // Keep console window open
            Console.WriteLine($"\nOperation finished. Success: {isSuccess}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}