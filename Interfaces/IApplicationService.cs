using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuadroAIPilot.Interfaces
{
    /// <summary>
    /// Interface for application management operations
    /// </summary>
    public interface IApplicationService
    {
        // Application launching
        Task<bool> LaunchApplicationAsync(string applicationName);
        Task<bool> LaunchApplicationByPathAsync(string applicationPath);

        // Application discovery
        string? FindApplication(string appName);
        List<string> GetInstalledApplications();
        
        // Application state
        bool IsApplicationRunning(string applicationName);
        Task<bool> CloseApplicationAsync(string applicationName);

        // Registry operations
        string? GetApplicationPathFromRegistry(string appName);
        List<string> SearchInRegistryApps(string searchTerm);
    }
}