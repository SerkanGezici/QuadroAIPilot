using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuadroAIPilot.Interfaces
{
    /// <summary>
    /// Interface for file search operations
    /// </summary>
    public interface IFileSearchService
    {
        // File search operations
        Task<List<string>> FindFilesAsync(string fileName, string searchPath = "");
        Task<List<string>> SearchInDirectoryAsync(string directory, string searchPattern);
        
        // Advanced search
        Task<List<string>> FindFilesByExtensionAsync(string extension, string searchPath = "");
        Task<List<string>> FindFilesByContentAsync(string content, string searchPath = "");
        
        // Directory operations
        Task<List<string>> GetDirectoriesAsync(string path);
        Task<bool> DirectoryExistsAsync(string path);
        Task<bool> FileExistsAsync(string filePath);
        
        // Quick access paths
        List<string> GetCommonDirectories();
        string GetDesktopPath();
        string GetDocumentsPath();
        string GetDownloadsPath();
    }
}