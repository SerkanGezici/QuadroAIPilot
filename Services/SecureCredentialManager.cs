using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using QuadroAIPilot.Services;

namespace QuadroAIPilot.Services
{
    /// <summary>
    /// SECURITY: Secure credential storage using Windows Credential Manager
    /// Implements encryption at rest using Windows DPAPI (Data Protection API)
    /// OWASP A02: Cryptographic Failures - MITIGATED
    /// </summary>
    public static class SecureCredentialManager
    {
        #region P/Invoke Declarations

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public uint Flags;
            public CRED_TYPE Type;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string TargetName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public CRED_PERSIST Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string TargetAlias;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string UserName;
        }

        private enum CRED_TYPE : uint
        {
            GENERIC = 1,
            DOMAIN_PASSWORD = 2,
            DOMAIN_CERTIFICATE = 3,
            DOMAIN_VISIBLE_PASSWORD = 4
        }

        private enum CRED_PERSIST : uint
        {
            SESSION = 1,
            LOCAL_MACHINE = 2,
            ENTERPRISE = 3
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] uint flags);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredRead(string target, CRED_TYPE type, int reservedFlag, out IntPtr credentialPtr);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredDelete(string target, CRED_TYPE type, int reservedFlag);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern void CredFree([In] IntPtr cred);

        #endregion

        private const string TARGET_PREFIX = "QuadroAIPilot:";

        /// <summary>
        /// SECURITY: Saves a credential securely to Windows Credential Manager
        /// Credentials are encrypted at rest using Windows DPAPI
        /// </summary>
        /// <param name="targetName">Credential identifier (e.g., "Email:user@example.com")</param>
        /// <param name="username">Username or email address</param>
        /// <param name="password">Password (will be securely stored)</param>
        /// <returns>True if saved successfully</returns>
        public static bool SaveCredential(string targetName, string username, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetName) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    LoggingService.LogWarning("[SecureCredentialManager] Invalid parameters for SaveCredential");
                    return false;
                }

                string fullTargetName = TARGET_PREFIX + targetName;

                // Convert password to byte array
                byte[] passwordBytes = Encoding.Unicode.GetBytes(password);

                // Allocate unmanaged memory for password
                IntPtr passwordPtr = Marshal.AllocHGlobal(passwordBytes.Length);
                try
                {
                    Marshal.Copy(passwordBytes, 0, passwordPtr, passwordBytes.Length);

                    var credential = new CREDENTIAL
                    {
                        Type = CRED_TYPE.GENERIC,
                        TargetName = fullTargetName,
                        UserName = username,
                        CredentialBlob = passwordPtr,
                        CredentialBlobSize = (uint)passwordBytes.Length,
                        Persist = CRED_PERSIST.LOCAL_MACHINE,
                        Comment = "QuadroAIPilot - Securely stored credential"
                    };

                    bool result = CredWrite(ref credential, 0);

                    if (result)
                    {
                        LoggingService.LogVerbose($"[SECURITY] Credential saved securely: {targetName}");
                    }
                    else
                    {
                        int error = Marshal.GetLastWin32Error();
                        LoggingService.LogWarning($"[SECURITY] Failed to save credential: {targetName}, Error: {error}");
                    }

                    return result;
                }
                finally
                {
                    // SECURITY: Zero out memory before freeing
                    if (passwordPtr != IntPtr.Zero)
                    {
                        Marshal.Copy(new byte[passwordBytes.Length], 0, passwordPtr, passwordBytes.Length);
                        Marshal.FreeHGlobal(passwordPtr);
                    }

                    // SECURITY: Zero out password bytes
                    Array.Clear(passwordBytes, 0, passwordBytes.Length);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[SECURITY] Error saving credential: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// SECURITY: Retrieves a credential securely from Windows Credential Manager
        /// Returns SecureString to minimize plaintext exposure in memory
        /// </summary>
        /// <param name="targetName">Credential identifier</param>
        /// <param name="username">Expected username (for validation)</param>
        /// <returns>SecureString containing password, or null if not found</returns>
        public static SecureString GetCredential(string targetName, string username)
        {
            IntPtr credPtr = IntPtr.Zero;

            try
            {
                if (string.IsNullOrWhiteSpace(targetName) || string.IsNullOrWhiteSpace(username))
                {
                    LoggingService.LogWarning("[SecureCredentialManager] Invalid parameters for GetCredential");
                    return null;
                }

                string fullTargetName = TARGET_PREFIX + targetName;

                bool success = CredRead(fullTargetName, CRED_TYPE.GENERIC, 0, out credPtr);

                if (!success)
                {
                    LoggingService.LogVerbose($"[SECURITY] Credential not found: {targetName}");
                    return null;
                }

                var credential = Marshal.PtrToStructure<CREDENTIAL>(credPtr);

                // Validate username matches
                if (!string.Equals(credential.UserName, username, StringComparison.OrdinalIgnoreCase))
                {
                    LoggingService.LogWarning($"[SECURITY] Username mismatch for credential: {targetName}");
                    return null;
                }

                // Extract password from unmanaged memory
                if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
                {
                    LoggingService.LogWarning($"[SECURITY] Empty credential blob: {targetName}");
                    return null;
                }

                byte[] passwordBytes = new byte[credential.CredentialBlobSize];
                Marshal.Copy(credential.CredentialBlob, passwordBytes, 0, (int)credential.CredentialBlobSize);

                try
                {
                    // Convert to SecureString (encrypted in memory)
                    string passwordString = Encoding.Unicode.GetString(passwordBytes);
                    SecureString securePassword = new SecureString();

                    foreach (char c in passwordString)
                    {
                        securePassword.AppendChar(c);
                    }

                    securePassword.MakeReadOnly();

                    LoggingService.LogVerbose($"[SECURITY] Credential retrieved securely: {targetName}");

                    return securePassword;
                }
                finally
                {
                    // SECURITY: Zero out password bytes
                    Array.Clear(passwordBytes, 0, passwordBytes.Length);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[SECURITY] Error retrieving credential: {ex.Message}", ex);
                return null;
            }
            finally
            {
                if (credPtr != IntPtr.Zero)
                {
                    CredFree(credPtr);
                }
            }
        }

        /// <summary>
        /// SECURITY: Retrieves password as plain string (use sparingly!)
        /// Prefer GetCredential() which returns SecureString
        /// </summary>
        public static string GetPasswordString(string targetName, string username)
        {
            SecureString securePassword = GetCredential(targetName, username);
            if (securePassword == null)
                return null;

            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.SecureStringToBSTR(securePassword);
                return Marshal.PtrToStringBSTR(ptr);
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(ptr); // Zero out memory
                }
            }
        }

        /// <summary>
        /// SECURITY: Deletes a credential from Windows Credential Manager
        /// </summary>
        public static bool DeleteCredential(string targetName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetName))
                {
                    LoggingService.LogWarning("[SecureCredentialManager] Invalid targetName for DeleteCredential");
                    return false;
                }

                string fullTargetName = TARGET_PREFIX + targetName;

                bool result = CredDelete(fullTargetName, CRED_TYPE.GENERIC, 0);

                if (result)
                {
                    LoggingService.LogVerbose($"[SECURITY] Credential deleted: {targetName}");
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    LoggingService.LogWarning($"[SECURITY] Failed to delete credential: {targetName}, Error: {error}");
                }

                return result;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[SECURITY] Error deleting credential: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// SECURITY: Converts SecureString to plain string (use sparingly!)
        /// </summary>
        public static string SecureStringToString(SecureString secureString)
        {
            if (secureString == null || secureString.Length == 0)
                return null;

            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.SecureStringToBSTR(secureString);
                return Marshal.PtrToStringBSTR(ptr);
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(ptr); // Zero out memory
                }
            }
        }

        /// <summary>
        /// SECURITY: Converts plain string to SecureString
        /// </summary>
        public static SecureString StringToSecureString(string plainString)
        {
            if (string.IsNullOrEmpty(plainString))
                return null;

            SecureString secureString = new SecureString();
            foreach (char c in plainString)
            {
                secureString.AppendChar(c);
            }
            secureString.MakeReadOnly();

            return secureString;
        }
    }
}
