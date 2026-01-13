using System;

namespace KBManager.core
{
    /// <summary>
    /// Model for storing Git configuration information
    /// </summary>
    public class GitConfigModel
    {
        /// <summary>
        /// Git username for commit
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Git email for commit
        /// </summary>
        public string UserEmail { get; set; }

        /// <summary>
        /// Remote repository address (e.g. https://github.com/username/repo.git)
        /// </summary>
        public string RemoteAddress { get; set; }

        /// <summary>
        /// Remote repository address (e.g. https://github.com/username/repo.git)
        /// </summary>
        public string RemoteAddressHttps { get; set; }

        /// <summary>
        /// Remote repository address (e.g. git@github.com:username/repo.git)
        /// </summary>
        public string RemoteAddressSsh { get; set; }

        /// <summary>
        /// Local repository directory path
        /// </summary>
        public string RepositoryDirectory { get; set; }

        /// <summary>
        /// Commit message (customizable for each commit)
        /// </summary>
        public string CommitMessage { get; set; }

        /// <summary>
        /// Validate whether the core configuration is complete
        /// </summary>
        /// <returns>Whether validation passes</returns>
        public bool ValidateCoreConfig()
        {
            if (string.IsNullOrEmpty(RepositoryDirectory))
            {
                Console.WriteLine("Error: RepositoryDirectory cannot be empty");
                return false;
            }

            if (string.IsNullOrEmpty(UserName) || string.IsNullOrEmpty(UserEmail))
            {
                Console.WriteLine("Error: UserName and UserEmail cannot be empty");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validate whether clone-related configuration is complete
        /// </summary>
        /// <returns>Whether validation passes</returns>
        public bool ValidateCloneConfig()
        {
            if (string.IsNullOrEmpty(RemoteAddressHttps) && string.IsNullOrEmpty(RemoteAddressSsh))
            {
                Console.WriteLine("Error: RemoteAddress cannot be both empty");
                return false;
            }

            if (string.IsNullOrEmpty(RepositoryDirectory))
            {
                Console.WriteLine("Error: RepositoryDirectory cannot be empty");
                return false;
            }

            return true;
        }
    }
}