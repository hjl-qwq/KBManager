using System;
using System.Net; // 引入网络配置命名空间
using LibGit2Sharp;

namespace KBManager.core
{
    public class GitHelper
    {
        /// <summary>
        /// Clone github repository to local path
        /// </summary>
        /// <param name="repositoryUrl">Github repository url (e.g. https://github.com/username/repo.git)</param>
        /// <param name="localPath">Local path to save repository</param>
        /// <returns>Whether the clone operation is successful</returns>
        public bool CloneRepository(string repositoryUrl, string localPath)
        {
            try
            {
                // 核心修复：仅启用 TLS 1.2（兼容所有支持 .NET Standard 2.0 的运行时）
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                // 可选：临时禁用证书验证（仅测试环境用，生产环境删除！）
                // ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

                // Core clone operation
                Repository.Clone(repositoryUrl, localPath);
                Console.WriteLine($"Repository cloned successfully to: {localPath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Clone failed: {ex.Message}");
                // 打印完整异常信息，方便定位问题
                Console.WriteLine($"Full error details:\n{ex.ToString()}");
                return false;
            }
        }
    }
}