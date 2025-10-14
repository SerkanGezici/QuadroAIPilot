using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace QuadroAIPilot.Configuration
{
    /// <summary>
    /// Email integration configuration settings
    /// </summary>
    public class EmailConfiguration
    {
        /// <summary>
        /// Outlook integration settings
        /// </summary>
        public OutlookConfiguration Outlook { get; set; } = new();

        /// <summary>
        /// MAPI service settings
        /// </summary>
        public MAPIConfiguration MAPI { get; set; } = new();

        /// <summary>
        /// Email account management settings
        /// </summary>
        public AccountManagementConfiguration AccountManagement { get; set; } = new();

        /// <summary>
        /// Email search and filtering settings
        /// </summary>
        public SearchConfiguration Search { get; set; } = new();
    }

    /// <summary>
    /// Outlook integration configuration
    /// </summary>
    public class OutlookConfiguration
    {
        /// <summary>
        /// Enable Outlook integration
        /// </summary>
        public bool EnableOutlookIntegration { get; set; } = true;

        /// <summary>
        /// Outlook application path
        /// </summary>
        public string OutlookPath { get; set; } = string.Empty;

        /// <summary>
        /// Default Outlook profile name
        /// </summary>
        public string DefaultProfile { get; set; } = string.Empty;

        /// <summary>
        /// Auto-detect Outlook installation
        /// </summary>
        public bool AutoDetectInstallation { get; set; } = true;

        /// <summary>
        /// Outlook startup timeout in milliseconds
        /// </summary>
        public int StartupTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// Enable Outlook automation
        /// </summary>
        public bool EnableAutomation { get; set; } = true;

        /// <summary>
        /// Use Outlook security prompts
        /// </summary>
        public bool UseSecurityPrompts { get; set; } = false;

        /// <summary>
        /// Close Outlook after operations
        /// </summary>
        public bool CloseAfterOperations { get; set; } = false;
    }

    /// <summary>
    /// MAPI service configuration
    /// </summary>
    public class MAPIConfiguration
    {
        /// <summary>
        /// Enable MAPI service
        /// </summary>
        public bool EnableMAPIService { get; set; } = true;

        /// <summary>
        /// MAPI session timeout in milliseconds
        /// </summary>
        public int SessionTimeoutMs { get; set; } = 60000;

        /// <summary>
        /// Maximum MAPI retry attempts
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// MAPI operation timeout in milliseconds
        /// </summary>
        public int OperationTimeoutMs { get; set; } = 15000;

        /// <summary>
        /// Enable MAPI logging
        /// </summary>
        public bool EnableLogging { get; set; } = true;

        /// <summary>
        /// MAPI log level (Debug, Info, Warning, Error)
        /// </summary>
        public string LogLevel { get; set; } = "Info";

        /// <summary>
        /// Use extended MAPI
        /// </summary>
        public bool UseExtendedMAPI { get; set; } = true;
    }

    /// <summary>
    /// Email account management configuration
    /// </summary>
    public class AccountManagementConfiguration
    {
        /// <summary>
        /// Auto-discover email accounts
        /// </summary>
        public bool AutoDiscoverAccounts { get; set; } = true;

        /// <summary>
        /// Load accounts on startup
        /// </summary>
        public bool LoadAccountsOnStartup { get; set; } = true;

        /// <summary>
        /// Account refresh interval in minutes
        /// </summary>
        public int RefreshIntervalMinutes { get; set; } = 30;

        /// <summary>
        /// Maximum accounts to load
        /// </summary>
        public int MaxAccountsToLoad { get; set; } = 10;

        /// <summary>
        /// Cache account information
        /// </summary>
        public bool CacheAccountInfo { get; set; } = true;

        /// <summary>
        /// Account cache duration in hours
        /// </summary>
        public int CacheDurationHours { get; set; } = 24;

        /// <summary>
        /// Validate account credentials
        /// </summary>
        public bool ValidateCredentials { get; set; } = true;

        /// <summary>
        /// Preferred email accounts (in priority order)
        /// </summary>
        public List<string> PreferredAccounts { get; set; } = new();
    }

    /// <summary>
    /// Email search and filtering configuration
    /// </summary>
    public class SearchConfiguration
    {
        /// <summary>
        /// Default search scope (Inbox, AllFolders, etc.)
        /// </summary>
        public string DefaultSearchScope { get; set; } = "Inbox";

        /// <summary>
        /// Maximum search results
        /// </summary>
        public int MaxSearchResults { get; set; } = 100;

        /// <summary>
        /// Search timeout in milliseconds
        /// </summary>
        public int SearchTimeoutMs { get; set; } = 10000;

        /// <summary>
        /// Enable full-text search
        /// </summary>
        public bool EnableFullTextSearch { get; set; } = true;

        /// <summary>
        /// Search in attachments
        /// </summary>
        public bool SearchInAttachments { get; set; } = false;

        /// <summary>
        /// Default search date range in days
        /// </summary>
        public int DefaultDateRangeDays { get; set; } = 30;

        /// <summary>
        /// Enable search indexing
        /// </summary>
        public bool EnableSearchIndexing { get; set; } = true;

        /// <summary>
        /// Search result sorting (Date, Subject, Sender, Relevance)
        /// </summary>
        public string DefaultSorting { get; set; } = "Date";

        /// <summary>
        /// Include sent items in searches
        /// </summary>
        public bool IncludeSentItems { get; set; } = true;

        /// <summary>
        /// Include deleted items in searches
        /// </summary>
        public bool IncludeDeletedItems { get; set; } = false;

        /// <summary>
        /// Folder exclusions for search
        /// </summary>
        public List<string> ExcludedFolders { get; set; } = new()
        {
            "Junk Email",
            "Spam",
            "Trash",
            "Deleted Items"
        };
    }
}