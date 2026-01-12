using System;
using System.IO;
using System.Text.RegularExpressions;

namespace KBManager.core
{
    /// <summary>
    /// Model for storing Git configuration information
    /// </summary>
    public class GitConfigModel
    {
        // Private backing fields
        private string _userName;
        private string _userEmail;
        private string _remoteAddress;
        private string _repositoryDirectory;
        private string _commitMessage;

        /// <summary>
        /// Git username for commit (validated on assignment)
        /// </summary>
        public string UserName
        {
            get => _userName;
            set
            {
                if (!IsValidUserName(value, out string errorMsg))
                {
                    throw new ArgumentException(errorMsg, nameof(UserName));
                }
                _userName = value;
            }
        }

        /// <summary>
        /// Git email for commit (validated on assignment)
        /// </summary>
        public string UserEmail
        {
            get => _userEmail;
            set
            {
                if (!IsValidEmail(value, out string errorMsg))
                {
                    throw new ArgumentException(errorMsg, nameof(UserEmail));
                }
                _userEmail = value;
            }
        }

        /// <summary>
        /// Remote repository address (validated on assignment)
        /// </summary>
        public string RemoteAddress
        {
            get => _remoteAddress;
            set
            {
                if (!IsValidRemoteAddress(value, out string errorMsg))
                {
                    throw new ArgumentException(errorMsg, nameof(RemoteAddress));
                }
                _remoteAddress = value;
            }
        }

        /// <summary>
        /// Local repository directory path (validated on assignment)
        /// </summary>
        public string RepositoryDirectory
        {
            get => _repositoryDirectory;
            set
            {
                if (!IsValidDirectoryPath(value, out string errorMsg))
                {
                    throw new ArgumentException(errorMsg, nameof(RepositoryDirectory));
                }
                _repositoryDirectory = value;
            }
        }

        /// <summary>
        /// Commit message (validated on assignment)
        /// </summary>
        public string CommitMessage
        {
            get => _commitMessage;
            set
            {
                if (!IsValidCommitMessage(value, out string errorMsg))
                {
                    throw new ArgumentException(errorMsg, nameof(CommitMessage));
                }
                _commitMessage = value;
            }
        }

        // Validation helper methods
        private bool IsValidUserName(string value, out string errorMsg)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errorMsg = "Username cannot be empty or whitespace";
                return false;
            }
            if (value.Length > 100)
            {
                errorMsg = "Username cannot exceed 100 characters";
                return false;
            }
            errorMsg = string.Empty;
            return true;
        }

        private bool IsValidEmail(string value, out string errorMsg)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errorMsg = "Email cannot be empty or whitespace";
                return false;
            }
            try
            {
                var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                if (!regex.IsMatch(value))
                {
                    errorMsg = "Email format is invalid (e.g., example@domain.com)";
                    return false;
                }
            }
            catch
            {
                errorMsg = "Email validation failed";
                return false;
            }
            errorMsg = string.Empty;
            return true;
        }

        private bool IsValidRemoteAddress(string value, out string errorMsg)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errorMsg = "Remote address cannot be empty or whitespace";
                return false;
            }
            if (!value.StartsWith("https://") && !value.StartsWith("git@"))
            {
                errorMsg = "Remote address must be HTTPS or SSH format (e.g., git@gitee.com:user/repo.git)";
                return false;
            }
            errorMsg = string.Empty;
            return true;
        }

        private bool IsValidDirectoryPath(string value, out string errorMsg)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errorMsg = "Repository directory cannot be empty or whitespace";
                return false;
            }
            try
            {
                // Validate path format (even if directory doesn't exist yet)
                string fullPath = Path.GetFullPath(value);
            }
            catch
            {
                errorMsg = "Repository directory path is invalid (e.g., C:\\Git\\MyRepo or /home/user/repo)";
                return false;
            }
            errorMsg = string.Empty;
            return true;
        }

        private bool IsValidCommitMessage(string value, out string errorMsg)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errorMsg = "Commit message cannot be empty or whitespace";
                return false;
            }
            if (value.Length > 500)
            {
                errorMsg = "Commit message cannot exceed 500 characters";
                return false;
            }
            errorMsg = string.Empty;
            return true;
        }

        // Simplified validation methods (now redundant but kept for backward compatibility)
        public bool ValidateCoreConfig()
        {
            try
            {
                // Validation is already enforced via property setters, so just verify non-null
                return !string.IsNullOrEmpty(_userName) && 
                       !string.IsNullOrEmpty(_userEmail) && 
                       !string.IsNullOrEmpty(_repositoryDirectory);
            }
            catch
            {
                return false;
            }
        }

        public bool ValidateCloneConfig()
        {
            try
            {
                return !string.IsNullOrEmpty(_remoteAddress) && 
                       !string.IsNullOrEmpty(_repositoryDirectory);
            }
            catch
            {
                return false;
            }
        }
    }
}
