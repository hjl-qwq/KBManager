using System.Collections.Generic;
using System.Threading.Tasks;

namespace KBManager.core
{
    /// <summary>
    /// File-system scanning for the knowledge-base repository.
    /// </summary>
    public interface IFileScanService
    {
        ServiceResult<List<string>> ScanRepositoryFiles(string repositoryDirectory);
        Task<ServiceResult> BatchAddFilesToDatabaseAsync(string repositoryDirectory);
    }
}
