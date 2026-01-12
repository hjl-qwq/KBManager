using System;
using System.Text.RegularExpressions;

namespace KBManager.core
{
    /// <summary>
    /// Universal format and null value validation utility class
    /// </summary>
    public static class ValidationHelper
    {
        // Email regex (general email format)
        private static readonly Regex EmailRegex = new Regex(
            @"^[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // HTTPS Git address regex (matches mainstream repositories)
        private static readonly Regex HttpsGitRegex = new Regex(
            @"^https:\/\/(www\.)?[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+\/[a-zA-Z0-9-_]+\/[a-zA-Z0-9-_]+\.git$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // SSH Git address regex (git@xxx:username/repo.git)
        private static readonly Regex SshGitRegex = new Regex(
            @"^git@[a-zA-Z0-9-.]+:[a-zA-Z0-9-_]+\/[a-zA-Z0-9-_]+\.git$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Null value validation
        /// </summary>
        /// <param name="value">Value to be validated</param>
        /// <param name="fieldName">Field name (used for error prompt)</param>
        /// <param name="errorMsg">Output error message</param>
        /// <returns>Whether it is non-null and non-empty</returns>
        public static bool IsNotNullOrEmpty(string value, string fieldName, out string errorMsg)
        {
            errorMsg = string.Empty;
            if (string.IsNullOrEmpty(value))
            {
                errorMsg = $"{fieldName} cannot be empty or null";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Email format + null value validation
        /// </summary>
        /// <param name="email">Email to be validated</param>
        /// <param name="errorMsg">Output error message</param>
        /// <returns>Whether it is valid</returns>
        public static bool IsValidEmail(string email, out string errorMsg)
        {
            // First perform null value validation
            if (!IsNotNullOrEmpty(email, "UserEmail", out errorMsg))
            {
                return false;
            }
            // Then perform format validation
            if (!EmailRegex.IsMatch(email))
            {
                errorMsg = "UserEmail format is invalid (example: user@example.com)";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Git remote address (HTTPS/SSH) format + null value validation
        /// </summary>
        /// <param name="remoteAddress">Address to be validated</param>
        /// <param name="errorMsg">Output error message</param>
        /// <returns>Whether it is valid</returns>
        public static bool IsValidGitRemoteAddress(string remoteAddress, out string errorMsg)
        {
            // First perform null value validation
            if (!IsNotNullOrEmpty(remoteAddress, "RemoteAddress", out errorMsg))
            {
                return false;
            }
            // Then perform format validation
            bool isHttpsValid = HttpsGitRegex.IsMatch(remoteAddress);
            bool isSshValid = SshGitRegex.IsMatch(remoteAddress);

            if (!isHttpsValid && !isSshValid)
            {
                errorMsg = $"RemoteAddress format is invalid!\n" +
                           $"Supported HTTPS format: https://github.com/username/repo.git\n" +
                           $"Supported SSH format: git@gitee.com:username/repo.git";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Repository directory path null value validation (reuse null value validation logic)
        /// </summary>
        public static bool IsValidRepositoryDirectory(string path, out string errorMsg)
        {
            return IsNotNullOrEmpty(path, "RepositoryDirectory", out errorMsg);
        }

        /// <summary>
        /// Username null value validation (reuse null value validation logic)
        /// </summary>
        public static bool IsValidUserName(string name, out string errorMsg)
        {
            return IsNotNullOrEmpty(name, "UserName", out errorMsg);
        }

        /// <summary>
        /// Commit message null value validation (reuse null value validation logic)
        /// </summary>
        public static bool IsValidCommitMessage(string msg, out string errorMsg)
        {
            return IsNotNullOrEmpty(msg, "CommitMessage", out errorMsg);
        }
    }
}
