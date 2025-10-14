using System.Text.Json.Serialization;

namespace QuadroAIPilot.Configuration
{
    /// <summary>
    /// Performance and logging configuration settings
    /// </summary>
    public class PerformanceConfiguration
    {
        /// <summary>
        /// Memory management settings
        /// </summary>
        public MemoryConfiguration Memory { get; set; } = new();

        /// <summary>
        /// Logging configuration settings
        /// </summary>
        public LoggingConfiguration Logging { get; set; } = new();

        /// <summary>
        /// Threading and concurrency settings
        /// </summary>
        public ThreadingConfiguration Threading { get; set; } = new();

        /// <summary>
        /// Cache and storage optimization settings
        /// </summary>
        public CacheConfiguration Cache { get; set; } = new();

        /// <summary>
        /// Monitoring and diagnostics settings
        /// </summary>
        public MonitoringConfiguration Monitoring { get; set; } = new();
    }

    /// <summary>
    /// Memory management configuration
    /// </summary>
    public class MemoryConfiguration
    {
        /// <summary>
        /// Enable automatic garbage collection
        /// </summary>
        public bool EnableAutoGC { get; set; } = true;

        /// <summary>
        /// Garbage collection interval in minutes
        /// </summary>
        public int GCIntervalMinutes { get; set; } = 5;

        /// <summary>
        /// Maximum memory usage in MB before forcing GC
        /// </summary>
        public int MaxMemoryUsageMB { get; set; } = 500;

        /// <summary>
        /// Enable memory usage monitoring
        /// </summary>
        public bool EnableMemoryMonitoring { get; set; } = true;

        /// <summary>
        /// Memory usage warning threshold in MB
        /// </summary>
        public int MemoryWarningThresholdMB { get; set; } = 300;

        /// <summary>
        /// Enable large object heap compaction
        /// </summary>
        public bool EnableLOHCompaction { get; set; } = true;

        /// <summary>
        /// Dispose resources immediately
        /// </summary>
        public bool ImmediateDispose { get; set; } = true;
    }

    /// <summary>
    /// Logging configuration
    /// </summary>
    public class LoggingConfiguration
    {
        /// <summary>
        /// Enable application logging
        /// </summary>
        public bool EnableLogging { get; set; } = true;

        /// <summary>
        /// Logging level (Trace, Debug, Info, Warning, Error, Fatal)
        /// </summary>
        public string LogLevel { get; set; } = "Info";

        /// <summary>
        /// Enable console logging
        /// </summary>
        public bool EnableConsoleLogging { get; set; } = true;

        /// <summary>
        /// Enable file logging
        /// </summary>
        public bool EnableFileLogging { get; set; } = true;

        /// <summary>
        /// Log file path
        /// </summary>
        public string LogFilePath { get; set; } = "Logs/QuadroAIPilot.log";

        /// <summary>
        /// Maximum log file size in MB
        /// </summary>
        public int MaxLogFileSizeMB { get; set; } = 10;

        /// <summary>
        /// Maximum number of log files to keep
        /// </summary>
        public int MaxLogFiles { get; set; } = 5;

        /// <summary>
        /// Log rotation interval (Daily, Weekly, Monthly)
        /// </summary>
        public string LogRotationInterval { get; set; } = "Daily";

        /// <summary>
        /// Enable structured logging (JSON format)
        /// </summary>
        public bool EnableStructuredLogging { get; set; } = false;

        /// <summary>
        /// Include timestamp in log entries
        /// </summary>
        public bool IncludeTimestamp { get; set; } = true;

        /// <summary>
        /// Include thread ID in log entries
        /// </summary>
        public bool IncludeThreadId { get; set; } = false;

        /// <summary>
        /// Include stack trace for errors
        /// </summary>
        public bool IncludeStackTrace { get; set; } = true;
    }

    /// <summary>
    /// Threading and concurrency configuration
    /// </summary>
    public class ThreadingConfiguration
    {
        /// <summary>
        /// Maximum worker threads
        /// </summary>
        public int MaxWorkerThreads { get; set; } = 10;

        /// <summary>
        /// Maximum completion port threads
        /// </summary>
        public int MaxCompletionPortThreads { get; set; } = 10;

        /// <summary>
        /// Thread pool minimum threads
        /// </summary>
        public int MinThreadPoolThreads { get; set; } = 2;

        /// <summary>
        /// Enable thread pool optimization
        /// </summary>
        public bool EnableThreadPoolOptimization { get; set; } = true;

        /// <summary>
        /// Use dedicated thread for UI operations
        /// </summary>
        public bool UseDedicatedUIThread { get; set; } = true;

        /// <summary>
        /// Use dedicated thread for command processing
        /// </summary>
        public bool UseDedicatedCommandThread { get; set; } = true;

        /// <summary>
        /// Task scheduler type (Default, ThreadPool, Custom)
        /// </summary>
        public string TaskSchedulerType { get; set; } = "Default";

        /// <summary>
        /// Enable task cancellation
        /// </summary>
        public bool EnableTaskCancellation { get; set; } = true;

        /// <summary>
        /// Default task timeout in milliseconds
        /// </summary>
        public int DefaultTaskTimeoutMs { get; set; } = 30000;
    }

    /// <summary>
    /// Cache and storage optimization configuration
    /// </summary>
    public class CacheConfiguration
    {
        /// <summary>
        /// Enable application caching
        /// </summary>
        public bool EnableCaching { get; set; } = true;

        /// <summary>
        /// Cache size limit in MB
        /// </summary>
        public int CacheSizeLimitMB { get; set; } = 100;

        /// <summary>
        /// Cache entry expiration time in minutes
        /// </summary>
        public int CacheExpirationMinutes { get; set; } = 30;

        /// <summary>
        /// Enable cache compression
        /// </summary>
        public bool EnableCacheCompression { get; set; } = false;

        /// <summary>
        /// Cache cleanup interval in minutes
        /// </summary>
        public int CacheCleanupIntervalMinutes { get; set; } = 10;

        /// <summary>
        /// Enable persistent cache (disk storage)
        /// </summary>
        public bool EnablePersistentCache { get; set; } = false;

        /// <summary>
        /// Cache directory path
        /// </summary>
        public string CacheDirectoryPath { get; set; } = "Cache";

        /// <summary>
        /// Enable cache statistics
        /// </summary>
        public bool EnableCacheStatistics { get; set; } = false;
    }

    /// <summary>
    /// Monitoring and diagnostics configuration
    /// </summary>
    public class MonitoringConfiguration
    {
        /// <summary>
        /// Enable performance monitoring
        /// </summary>
        public bool EnablePerformanceMonitoring { get; set; } = true;

        /// <summary>
        /// Performance monitoring interval in seconds
        /// </summary>
        public int MonitoringIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// Enable CPU usage monitoring
        /// </summary>
        public bool EnableCPUMonitoring { get; set; } = true;

        /// <summary>
        /// Enable memory usage monitoring
        /// </summary>
        public bool EnableMemoryUsageMonitoring { get; set; } = true;

        /// <summary>
        /// Enable disk usage monitoring
        /// </summary>
        public bool EnableDiskUsageMonitoring { get; set; } = false;

        /// <summary>
        /// Enable network usage monitoring
        /// </summary>
        public bool EnableNetworkUsageMonitoring { get; set; } = false;

        /// <summary>
        /// Enable application metrics collection
        /// </summary>
        public bool EnableMetricsCollection { get; set; } = true;

        /// <summary>
        /// Metrics retention period in days
        /// </summary>
        public int MetricsRetentionDays { get; set; } = 7;

        /// <summary>
        /// Enable performance alerts
        /// </summary>
        public bool EnablePerformanceAlerts { get; set; } = false;

        /// <summary>
        /// CPU usage alert threshold (percentage)
        /// </summary>
        public int CPUAlertThreshold { get; set; } = 80;

        /// <summary>
        /// Memory usage alert threshold (percentage)
        /// </summary>
        public int MemoryAlertThreshold { get; set; } = 70;
    }
}