using System;
using System.Runtime.InteropServices;

namespace QuadroAIPilot.Services.MAPI
{
    /// <summary>
    /// MAPI native structures ve data types
    /// </summary>
    
    [StructLayout(LayoutKind.Sequential)]
    public struct MAPIINIT_0
    {
        public uint ulVersion;
        public uint ulFlags;
    }
    
    // SPropValue, SPropValueUnion, SBinary are now defined in MAPIInterfaces.cs
    
    [StructLayout(LayoutKind.Sequential)]
    public struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
        
        public DateTime ToDateTime()
        {
            long fileTime = ((long)dwHighDateTime << 32) | dwLowDateTime;
            return DateTime.FromFileTime(fileTime);
        }
        
        public static FILETIME FromDateTime(DateTime dateTime)
        {
            long fileTime = dateTime.ToFileTime();
            return new FILETIME
            {
                dwLowDateTime = (uint)(fileTime & 0xFFFFFFFF),
                dwHighDateTime = (uint)(fileTime >> 32)
            };
        }
    }
    
    // SRowSet, SRow are now defined in MAPIInterfaces.cs
    
    [StructLayout(LayoutKind.Sequential)]
    public struct SPropTagArray
    {
        public uint cValues;
        public IntPtr aulPropTag; // uint array
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct ENTRYID
    {
        public uint cb;
        public IntPtr lpb;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct MAPIERROR
    {
        public uint ulVersion;
        public IntPtr lpszError;
        public IntPtr lpszComponent;
        public uint ulLowLevelError;
        public uint ulContext;
    }
    
    /// <summary>
    /// MAPI Profile information structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MAPIProfile
    {
        public string ProfileName;
        public bool IsDefault;
        public uint ProfileFlags;
        public string DisplayName;
    }
    
    /// <summary>
    /// MAPI Session information
    /// </summary>
    public class MAPISession
    {
        public IntPtr SessionPtr { get; set; }
        public string ProfileName { get; set; } = "";
        public bool IsConnected { get; set; }
        public DateTime ConnectedTime { get; set; }
        public uint SessionFlags { get; set; }
    }
    
    /// <summary>
    /// MAPI Store information
    /// </summary>
    public class MAPIStore
    {
        public IntPtr StorePtr { get; set; }
        public string StoreName { get; set; } = "";
        public string ProviderName { get; set; } = "";
        public bool IsDefault { get; set; }
        public uint StoreFlags { get; set; }
        public ENTRYID EntryId { get; set; }
    }
    
    /// <summary>
    /// MAPI Folder information
    /// </summary>
    public class MAPIFolder
    {
        public IntPtr FolderPtr { get; set; }
        public string FolderName { get; set; } = "";
        public MAPIFolderType FolderType { get; set; }
        public uint MessageCount { get; set; }
        public uint UnreadCount { get; set; }
        public ENTRYID EntryId { get; set; }
        public ENTRYID ParentEntryId { get; set; }
    }
    
    /// <summary>
    /// MAPI Table interface wrapper
    /// </summary>
    public class MAPITable
    {
        public IntPtr TablePtr { get; set; }
        public uint RowCount { get; set; }
        public bool IsInitialized { get; set; }
        public SPropTagArray Columns { get; set; }
    }
    
    /// <summary>
    /// MAPI Property wrapper
    /// </summary>
    public class MAPIProperty
    {
        public uint PropertyTag { get; set; }
        public object? Value { get; set; }
        public uint PropertyType => PropertyTag & 0xFFFF;
        public uint PropertyId => PropertyTag >> 16;
        
        public T GetValue<T>()
        {
            if (Value is T directValue)
                return directValue;
                
            if (Value != null)
            {
                try
                {
                    return (T)Convert.ChangeType(Value, typeof(T));
                }
                catch
                {
                    return default(T)!;
                }
            }
            
            return default(T)!;
        }
        
        public string GetStringValue()
        {
            return Value?.ToString() ?? string.Empty;
        }
        
        public DateTime GetDateTimeValue()
        {
            if (Value is FILETIME ft)
                return ft.ToDateTime();
            if (Value is DateTime dt)
                return dt;
            return DateTime.MinValue;
        }
        
        public bool GetBoolValue()
        {
            if (Value is bool b)
                return b;
            if (Value is int i)
                return i != 0;
            if (Value is uint ui)
                return ui != 0;
            return false;
        }
        
        public int GetIntValue()
        {
            if (Value is int i)
                return i;
            if (Value is uint ui)
                return (int)ui;
            if (Value is short s)
                return s;
            if (Value is long l)
                return (int)l;
            return 0;
        }
    }
    
    /// <summary>
    /// MAPI Result wrapper
    /// </summary>
    public class MAPIResult<T>
    {
        public bool Success { get; set; }
        public uint ErrorCode { get; set; }
        public string ErrorMessage { get; set; } = "";
        public T? Data { get; set; }
        public Exception? Exception { get; set; }
        
        public static MAPIResult<T> Ok(T data)
        {
            return new MAPIResult<T>
            {
                Success = true,
                Data = data,
                ErrorCode = MAPIConstants.S_OK
            };
        }
        
        public static MAPIResult<T> Fail(uint errorCode, string message, Exception? ex = null)
        {
            return new MAPIResult<T>
            {
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = message,
                Exception = ex
            };
        }
        
        public static MAPIResult<T> Fail(string message, Exception? ex = null)
        {
            return Fail(MAPIConstants.MAPI_E_FAILURE, message, ex);
        }
    }
    
    /// <summary>
    /// MAPI Error information
    /// </summary>
    public class MAPIErrorInfo
    {
        public uint ErrorCode { get; set; }
        public string ErrorMessage { get; set; } = "";
        public string Component { get; set; } = "";
        public uint LowLevelError { get; set; }
        public uint Context { get; set; }
        
        public static MAPIErrorInfo FromErrorCode(uint errorCode)
        {
            return new MAPIErrorInfo
            {
                ErrorCode = errorCode,
                ErrorMessage = GetErrorMessage(errorCode),
                Component = "MAPI"
            };
        }
        
        private static string GetErrorMessage(uint errorCode)
        {
            return errorCode switch
            {
                MAPIConstants.S_OK => "Success",
                MAPIConstants.MAPI_E_FAILURE => "General failure",
                MAPIConstants.MAPI_E_INSUFFICIENT_MEMORY => "Insufficient memory",
                MAPIConstants.MAPI_E_ACCESS_DENIED => "Access denied",
                MAPIConstants.MAPI_E_USER_CANCEL => "User cancelled operation",
                MAPIConstants.MAPI_E_UNKNOWN_FLAGS => "Unknown flags specified",
                MAPIConstants.MAPI_E_INVALID_PARAMETER => "Invalid parameter",
                MAPIConstants.MAPI_E_INTERFACE_NOT_SUPPORTED => "Interface not supported",
                MAPIConstants.MAPI_E_NO_SUPPORT => "Operation not supported",
                _ => $"Unknown MAPI error: 0x{errorCode:X8}"
            };
        }
    }
}