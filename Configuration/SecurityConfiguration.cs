using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace QuadroAIPilot.Configuration
{
    /// <summary>
    /// Security and validation configuration settings
    /// </summary>
    public class SecurityConfiguration
    {
        /// <summary>
        /// Input validation settings
        /// </summary>
        public ValidationConfiguration Validation { get; set; } = new();

        /// <summary>
        /// File system security settings
        /// </summary>
        public FileSystemSecurityConfiguration FileSystem { get; set; } = new();

        /// <summary>
        /// Process execution security settings
        /// </summary>
        public ProcessSecurityConfiguration Process { get; set; } = new();

        /// <summary>
        /// Network and communication security settings
        /// </summary>
        public NetworkSecurityConfiguration Network { get; set; } = new();

        /// <summary>
        /// Audit and logging security settings
        /// </summary>
        public AuditConfiguration Audit { get; set; } = new();
    }

    /// <summary>
    /// Input validation configuration
    /// </summary>
    public class ValidationConfiguration
    {
        /// <summary>
        /// Enable input sanitization
        /// </summary>
        public bool EnableInputSanitization { get; set; } = true;

        /// <summary>
        /// Enable script injection detection
        /// </summary>
        public bool EnableScriptInjectionDetection { get; set; } = true;

        /// <summary>
        /// Enable path traversal detection
        /// </summary>
        public bool EnablePathTraversalDetection { get; set; } = true;

        /// <summary>
        /// Enable SQL injection detection
        /// </summary>
        public bool EnableSQLInjectionDetection { get; set; } = true;

        /// <summary>
        /// Maximum input length for commands
        /// </summary>
        public int MaxCommandLength { get; set; } = 1000;

        /// <summary>
        /// Maximum input length for file paths
        /// </summary>
        public int MaxFilePathLength { get; set; } = 260;

        /// <summary>
        /// Dangerous patterns to block (regex)
        /// </summary>
        public List<string> DangerousPatterns { get; set; } = new()
        {
            @"[;&|`]",              // Command chaining
            @"\$\([^)]*\)",         // Command substitution
            @"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>", // Script tags
            @"javascript:",         // JavaScript protocol
            @"vbscript:",          // VBScript protocol
            @"\.\.[\\/]",          // Path traversal
            @"['\""]\s*;\s*",       // SQL injection patterns
            @"union\s+select",     // SQL union
            @"drop\s+table"        // SQL drop
        };

        /// <summary>
        /// Allowed characters in user input (regex)
        /// </summary>
        public string AllowedCharacterPattern { get; set; } = @"^[a-zA-Z0-9\s\.\-_çğıöşüÇĞIİÖŞÜ]*$";

        /// <summary>
        /// Enable whitelist-based validation
        /// </summary>
        public bool EnableWhitelistValidation { get; set; } = true;
    }

    /// <summary>
    /// File system security configuration
    /// </summary>
    public class FileSystemSecurityConfiguration
    {
        /// <summary>
        /// Enable file path validation
        /// </summary>
        public bool EnablePathValidation { get; set; } = true;

        /// <summary>
        /// Restrict access to system directories
        /// </summary>
        public bool RestrictSystemDirectories { get; set; } = true;

        /// <summary>
        /// Allowed base directories for file operations
        /// </summary>
        public List<string> AllowedBaseDirectories { get; set; } = new()
        {
            @"C:\Users",
            @"D:\",
            @"E:\",
            @"C:\Temp"
        };

        /// <summary>
        /// Blocked directories
        /// </summary>
        public List<string> BlockedDirectories { get; set; } = new()
        {
            @"C:\Windows\System32",
            @"C:\Program Files\Windows NT",
            @"C:\Boot",
            @"C:\Recovery"
        };

        /// <summary>
        /// Allowed file extensions for operations
        /// </summary>
        public List<string> AllowedFileExtensions { get; set; } = new()
        {
            ".txt", ".doc", ".docx", ".pdf", ".xlsx", ".pptx",
            ".jpg", ".jpeg", ".png", ".gif", ".bmp",
            ".mp3", ".mp4", ".avi", ".mkv",
            ".zip", ".rar", ".7z"
        };

        /// <summary>
        /// Blocked file extensions
        /// </summary>
        public List<string> BlockedFileExtensions { get; set; } = new()
        {
            ".exe", ".bat", ".cmd", ".com", ".scr",
            ".pif", ".vbs", ".js", ".jar", ".msi",
            ".reg", ".inf", ".sys", ".dll"
        };

        /// <summary>
        /// Maximum file size for operations (in bytes)
        /// </summary>
        public long MaxFileSize { get; set; } = 100 * 1024 * 1024; // 100 MB

        /// <summary>
        /// Enable file content scanning
        /// </summary>
        public bool EnableContentScanning { get; set; } = false;
    }

    /// <summary>
    /// Process execution security configuration
    /// </summary>
    public class ProcessSecurityConfiguration
    {
        /// <summary>
        /// Enable process validation
        /// </summary>
        public bool EnableProcessValidation { get; set; } = true;

        /// <summary>
        /// Allowed process names (whitelist)
        /// </summary>
        public List<string> AllowedProcessNames { get; set; } = new()
        {
            "notepad.exe",
            "explorer.exe",
            "calc.exe",
            "msedge.exe",
            "chrome.exe",
            "firefox.exe",
            "outlook.exe",
            "winword.exe",
            "excel.exe",
            "powerpnt.exe"
        };

        /// <summary>
        /// Blocked process names
        /// </summary>
        public List<string> BlockedProcessNames { get; set; } = new()
        {
            "cmd.exe",
            "powershell.exe",
            "wscript.exe",
            "cscript.exe",
            "regedit.exe",
            "taskmgr.exe",
            "mmc.exe"
        };

        /// <summary>
        /// Allowed process directories
        /// </summary>
        public List<string> AllowedProcessDirectories { get; set; } = new()
        {
            @"C:\Program Files",
            @"C:\Program Files (x86)",
            @"C:\Windows\System32",
            @"C:\Users"
        };

        /// <summary>
        /// Process execution timeout in milliseconds
        /// </summary>
        public int ProcessTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// Enable process argument validation
        /// </summary>
        public bool EnableArgumentValidation { get; set; } = true;

        /// <summary>
        /// Maximum process arguments length
        /// </summary>
        public int MaxArgumentsLength { get; set; } = 500;
    }

    /// <summary>
    /// Network security configuration
    /// </summary>
    public class NetworkSecurityConfiguration
    {
        /// <summary>
        /// Enable network access validation
        /// </summary>
        public bool EnableNetworkValidation { get; set; } = true;

        /// <summary>
        /// Allowed domains for web requests
        /// </summary>
        public List<string> AllowedDomains { get; set; } = new()
        {
            "microsoft.com",
            "office.com",
            "outlook.com",
            "github.com"
        };

        /// <summary>
        /// Blocked domains
        /// </summary>
        public List<string> BlockedDomains { get; set; } = new();

        /// <summary>
        /// Enable SSL/TLS validation
        /// </summary>
        public bool EnableSSLValidation { get; set; } = true;

        /// <summary>
        /// Network request timeout in milliseconds
        /// </summary>
        public int RequestTimeoutMs { get; set; } = 15000;

        /// <summary>
        /// Maximum request size in bytes
        /// </summary>
        public long MaxRequestSize { get; set; } = 10 * 1024 * 1024; // 10 MB

        /// <summary>
        /// Enable request logging
        /// </summary>
        public bool EnableRequestLogging { get; set; } = true;
    }

    /// <summary>
    /// Audit and logging configuration
    /// </summary>
    public class AuditConfiguration
    {
        /// <summary>
        /// Enable security event logging
        /// </summary>
        public bool EnableSecurityLogging { get; set; } = true;

        /// <summary>
        /// Log all command executions
        /// </summary>
        public bool LogAllCommands { get; set; } = true;

        /// <summary>
        /// Log file access operations
        /// </summary>
        public bool LogFileOperations { get; set; } = true;

        /// <summary>
        /// Log process executions
        /// </summary>
        public bool LogProcessExecutions { get; set; } = true;

        /// <summary>
        /// Log network requests
        /// </summary>
        public bool LogNetworkRequests { get; set; } = false;

        /// <summary>
        /// Log security violations
        /// </summary>
        public bool LogSecurityViolations { get; set; } = true;

        /// <summary>
        /// Security log retention period in days
        /// </summary>
        public int LogRetentionDays { get; set; } = 90;

        /// <summary>
        /// Maximum log file size in MB
        /// </summary>
        public int MaxLogFileSizeMB { get; set; } = 50;

        /// <summary>
        /// Enable log encryption
        /// </summary>
        public bool EnableLogEncryption { get; set; } = false;
    }
}