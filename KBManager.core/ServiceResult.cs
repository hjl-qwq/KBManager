using System.Collections.Generic;

namespace KBManager.core
{
    /// <summary>
    /// Unified return type for all service operations.
    /// GUI/CLI layers consume this without depending on Console output.
    /// </summary>
    public class ServiceResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        public static ServiceResult Ok(string message = "") => new() { Success = true, Message = message };
        public static ServiceResult Fail(string message) => new() { Success = false, Message = message };
    }

    public class ServiceResult<T> : ServiceResult
    {
        public T? Data { get; set; }

        public static ServiceResult<T> Ok(T data, string message = "") =>
            new() { Success = true, Data = data, Message = message };

        public new static ServiceResult<T> Fail(string message) =>
            new() { Success = false, Message = message };
    }

    /// <summary>
    /// Lightweight DTO for file entries returned to presentation layer.
    /// </summary>
    public class FileEntryDto
    {
        public string FileName { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
    }

    /// <summary>
    /// Lightweight DTO for tag entries returned to presentation layer.
    /// </summary>
    public class TagEntryDto
    {
        public string TagName { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for tag with file count, used for autocomplete / suggestion lists.
    /// </summary>
    public class TagWithCountDto
    {
        public string TagName { get; set; } = string.Empty;
        public int FileCount { get; set; }
    }
}
