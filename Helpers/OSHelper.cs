using System;
using Microsoft.UI.Xaml;

namespace QuadroAIPilot.Helpers
{
    /// <summary>
    /// Helper class for OS-specific features and compatibility
    /// </summary>
    public static class OSHelper
    {
        /// <summary>
        /// Gets whether the current OS is Windows 11 or later
        /// </summary>
        public static bool IsWindows11OrLater
        {
            get
            {
                var version = Environment.OSVersion.Version;
                // Windows 11 is version 10.0.22000 or later
                return version.Major > 10 || 
                       (version.Major == 10 && version.Build >= 22000);
            }
        }

        /// <summary>
        /// Applies appropriate backdrop based on OS support
        /// NOTE: Simplified version to avoid dependency issues
        /// </summary>
        public static bool TrySetBackdrop(Window window, string preferredBackdrop = "Mica")
        {
            try
            {
                // For now, we'll handle backdrops through XAML styles
                // This avoids runtime errors with missing types
                // The actual backdrop will be set by ThemeManager
                
                System.Diagnostics.Debug.WriteLine($"[OSHelper] Backdrop request: {preferredBackdrop} on {(IsWindows11OrLater ? "Windows 11" : "Windows 10")}");
                
                // Return true to indicate the request was processed
                // Actual backdrop application happens in ThemeManager
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OSHelper] Failed to process backdrop request: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets OS-specific feature flags
        /// </summary>
        public static class Features
        {
            public static bool SupportsSnapLayouts => IsWindows11OrLater;
            public static bool SupportsRoundedCorners => IsWindows11OrLater;
            public static bool SupportsModernContext => IsWindows11OrLater;
            public static bool SupportsHapticFeedback => IsWindows11OrLater;
            public static bool SupportsMicaBackdrop => IsWindows11OrLater;
            public static bool SupportsAcrylicBackdrop => true; // Available on both Win10 and Win11
        }
    }
}