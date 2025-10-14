using System;
using System.Threading.Tasks;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Interfaces
{
    /// <summary>
    /// Interface for Windows API operations
    /// </summary>
    public interface IWindowsApiService
    {
        // Window operations
        IntPtr GetActiveWindow();
        string GetActiveWindowTitle();
        bool SetForegroundWindow(IntPtr hWnd);
        bool CloseActiveApplication();
        bool IsWindowVisible(IntPtr hWnd);

        // Volume operations
        bool SendVolumeCommand(string command);
        Task<bool> SendVolumeCommandAsync(string command);

        // Input operations
        void SendText(string text);
        void SendKeyStrokes(string keys);

        // System operations
        bool MinimizeWindow(IntPtr hWnd);
        bool MaximizeWindow(IntPtr hWnd);
        bool RestoreWindow(IntPtr hWnd);

        // Process operations
        bool KillProcessByName(string processName);
        bool IsProcessRunning(string processName);
        bool IsApplicationRunning(string processName);
        bool BringWindowToFront(IntPtr hWnd);
        bool BringWindowToFront(string processName, string windowTitle, bool maximize = false);
        
    }
}