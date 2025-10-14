using System;
using System.Runtime.InteropServices;

namespace QuadroAIPilot.Services.MAPI
{
    /// <summary>
    /// MAPI COM Interface definitions for real property reading
    /// Sprint 3: Real MAPI Implementation
    /// </summary>
    
    #region Core MAPI Interfaces
    
    [ComImport]
    [Guid("00020300-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMAPISession
    {
        [PreserveSig]
        uint GetLastError(uint hResult, uint ulFlags, out IntPtr lppMAPIError);
        
        [PreserveSig]
        uint GetMsgStoresTable(uint ulFlags, out IntPtr lppTable);
        
        [PreserveSig]
        uint OpenMsgStore(
            IntPtr ulUIParam,
            uint cbEntryID,
            IntPtr lpEntryID,
            IntPtr lpInterface,
            uint ulFlags,
            out IntPtr lppMDB);
            
        [PreserveSig]
        uint OpenAddressBook(
            IntPtr ulUIParam,
            IntPtr lpInterface,
            uint ulFlags,
            out IntPtr lppAdrBook);
            
        [PreserveSig]
        uint OpenProfileSection(
            IntPtr lpUID,
            IntPtr lpInterface,
            uint ulFlags,
            out IntPtr lppProfSect);
            
        [PreserveSig]
        uint GetStatusTable(uint ulFlags, out IntPtr lppTable);
        
        [PreserveSig]
        uint OpenEntry(
            uint cbEntryID,
            IntPtr lpEntryID,
            IntPtr lpInterface,
            uint ulFlags,
            out uint lpulObjType,
            out IntPtr lppUnk);
            
        [PreserveSig]
        uint CompareEntryIDs(
            uint cbEntryID1,
            IntPtr lpEntryID1,
            uint cbEntryID2,
            IntPtr lpEntryID2,
            uint ulFlags,
            out uint lpulResult);
            
        [PreserveSig]
        uint Advise(
            uint cbEntryID,
            IntPtr lpEntryID,
            uint ulEventMask,
            IntPtr lpAdviseSink,
            out uint lpulConnection);
            
        [PreserveSig]
        uint Unadvise(uint ulConnection);
        
        [PreserveSig]
        uint MessageOptions(
            IntPtr ulUIParam,
            uint ulFlags,
            [MarshalAs(UnmanagedType.LPTStr)] string lpszAdrType,
            IntPtr lpMessage);
            
        [PreserveSig]
        uint QueryDefaultMessageOpt(
            [MarshalAs(UnmanagedType.LPTStr)] string lpszAdrType,
            uint ulFlags,
            out uint lpcValues,
            out IntPtr lppOptions);
            
        [PreserveSig]
        uint EnumAdrTypes(
            uint ulFlags,
            out uint lpcAdrTypes,
            out IntPtr lpppszAdrTypes);
            
        [PreserveSig]
        uint QueryIdentity(
            out uint lpcbEntryID,
            out IntPtr lppEntryID);
            
        [PreserveSig]
        uint Logoff(
            IntPtr ulUIParam,
            uint ulFlags,
            uint ulReserved);
            
        [PreserveSig]
        uint SetDefaultStore(
            uint ulFlags,
            uint cbEntryID,
            IntPtr lpEntryID);
            
        [PreserveSig]
        uint AdminServices(
            uint ulFlags,
            out IntPtr lppServiceAdmin);
            
        [PreserveSig]
        uint ShowForm(
            IntPtr ulUIParam,
            IntPtr lpMsgStore,
            IntPtr lpParentFolder,
            IntPtr lpInterface,
            uint ulMessageToken,
            IntPtr lpMessageSent,
            uint ulFlags,
            uint ulMessageStatus,
            uint ulMessageFlags,
            uint ulAccess,
            [MarshalAs(UnmanagedType.LPTStr)] string lpszMessageClass);
            
        [PreserveSig]
        uint PrepareForm(
            IntPtr lpInterface,
            IntPtr lpMessage,
            out uint lpulMessageToken);
    }
    
    [ComImport]
    [Guid("00020306-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMsgStore
    {
        [PreserveSig]
        uint GetLastError(uint hResult, uint ulFlags, out IntPtr lppMAPIError);
        
        [PreserveSig]
        uint SaveChanges(uint ulFlags);
        
        [PreserveSig]
        uint GetProps(
            IntPtr lpPropTagArray,
            uint ulFlags,
            out uint lpcValues,
            out IntPtr lppPropArray);
            
        [PreserveSig]
        uint GetPropList(uint ulFlags, out IntPtr lppPropTagArray);
        
        [PreserveSig]
        uint OpenProperty(
            uint ulPropTag,
            IntPtr lpiid,
            uint ulInterfaceOptions,
            uint ulFlags,
            out IntPtr lppUnk);
            
        [PreserveSig]
        uint SetProps(
            uint cValues,
            IntPtr lpPropArray,
            out IntPtr lppProblems);
            
        [PreserveSig]
        uint DeleteProps(
            IntPtr lpPropTagArray,
            out IntPtr lppProblems);
            
        [PreserveSig]
        uint CopyTo(
            uint ciidExclude,
            IntPtr rgiidExclude,
            IntPtr lpExcludeProps,
            IntPtr ulUIParam,
            IntPtr lpProgress,
            IntPtr lpInterface,
            IntPtr lpDestObj,
            uint ulFlags,
            out IntPtr lppProblems);
            
        [PreserveSig]
        uint CopyProps(
            IntPtr lpIncludeProps,
            IntPtr ulUIParam,
            IntPtr lpProgress,
            IntPtr lpInterface,
            IntPtr lpDestObj,
            uint ulFlags,
            out IntPtr lppProblems);
            
        [PreserveSig]
        uint GetNamesFromIDs(
            IntPtr lppPropTags,
            IntPtr lpPropSetGuid,
            uint ulFlags,
            out uint lpcPropNames,
            out IntPtr lpppPropNames);
            
        [PreserveSig]
        uint GetIDsFromNames(
            uint cPropNames,
            IntPtr lppPropNames,
            uint ulFlags,
            out IntPtr lppPropTags);
            
        [PreserveSig]
        uint Advise(
            uint cbEntryID,
            IntPtr lpEntryID,
            uint ulEventMask,
            IntPtr lpAdviseSink,
            out uint lpulConnection);
            
        [PreserveSig]
        uint Unadvise(uint ulConnection);
        
        [PreserveSig]
        uint CompareEntryIDs(
            uint cbEntryID1,
            IntPtr lpEntryID1,
            uint cbEntryID2,
            IntPtr lpEntryID2,
            uint ulFlags,
            out uint lpulResult);
            
        [PreserveSig]
        uint OpenEntry(
            uint cbEntryID,
            IntPtr lpEntryID,
            IntPtr lpInterface,
            uint ulFlags,
            out uint lpulObjType,
            out IntPtr lppUnk);
            
        [PreserveSig]
        uint SetReceiveFolder(
            [MarshalAs(UnmanagedType.LPTStr)] string lpszMessageClass,
            uint ulFlags,
            uint cbEntryID,
            IntPtr lpEntryID);
            
        [PreserveSig]
        uint GetReceiveFolder(
            [MarshalAs(UnmanagedType.LPTStr)] string lpszMessageClass,
            uint ulFlags,
            out uint lpcbEntryID,
            out IntPtr lppEntryID,
            out IntPtr lppszExplicitClass);
            
        [PreserveSig]
        uint GetReceiveFolderTable(uint ulFlags, out IntPtr lppTable);
        
        [PreserveSig]
        uint StoreLogoff(out uint lpulFlags);
        
        [PreserveSig]
        uint AbortSubmit(
            uint cbEntryID,
            IntPtr lpEntryID,
            uint ulFlags);
            
        [PreserveSig]
        uint GetOutgoingQueue(uint ulFlags, out IntPtr lppTable);
        
        [PreserveSig]
        uint SetLockState(
            IntPtr lpMessage,
            uint ulLockState);
            
        [PreserveSig]
        uint FinishedMsg(
            uint ulFlags,
            uint cbEntryID,
            IntPtr lpEntryID);
            
        [PreserveSig]
        uint NotifyNewMail(IntPtr lpNotification);
    }
    
    [ComImport]
    [Guid("0002030C-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMAPIFolder
    {
        [PreserveSig]
        uint GetLastError(uint hResult, uint ulFlags, out IntPtr lppMAPIError);
        
        [PreserveSig]
        uint SaveChanges(uint ulFlags);
        
        [PreserveSig]
        uint GetProps(
            IntPtr lpPropTagArray,
            uint ulFlags,
            out uint lpcValues,
            out IntPtr lppPropArray);
            
        [PreserveSig]
        uint GetPropList(uint ulFlags, out IntPtr lppPropTagArray);
        
        [PreserveSig]
        uint OpenProperty(
            uint ulPropTag,
            IntPtr lpiid,
            uint ulInterfaceOptions,
            uint ulFlags,
            out IntPtr lppUnk);
            
        [PreserveSig]
        uint SetProps(
            uint cValues,
            IntPtr lpPropArray,
            out IntPtr lppProblems);
            
        [PreserveSig]
        uint DeleteProps(
            IntPtr lpPropTagArray,
            out IntPtr lppProblems);
            
        [PreserveSig]
        uint CopyTo(
            uint ciidExclude,
            IntPtr rgiidExclude,
            IntPtr lpExcludeProps,
            IntPtr ulUIParam,
            IntPtr lpProgress,
            IntPtr lpInterface,
            IntPtr lpDestObj,
            uint ulFlags,
            out IntPtr lppProblems);
            
        [PreserveSig]
        uint CopyProps(
            IntPtr lpIncludeProps,
            IntPtr ulUIParam,
            IntPtr lpProgress,
            IntPtr lpInterface,
            IntPtr lpDestObj,
            uint ulFlags,
            out IntPtr lppProblems);
            
        [PreserveSig]
        uint GetNamesFromIDs(
            IntPtr lppPropTags,
            IntPtr lpPropSetGuid,
            uint ulFlags,
            out uint lpcPropNames,
            out IntPtr lpppPropNames);
            
        [PreserveSig]
        uint GetIDsFromNames(
            uint cPropNames,
            IntPtr lppPropNames,
            uint ulFlags,
            out IntPtr lppPropTags);
            
        [PreserveSig]
        uint CreateMessage(
            IntPtr lpInterface,
            uint ulFlags,
            out IntPtr lppMessage);
            
        [PreserveSig]
        uint CopyMessages(
            IntPtr lpMsgList,
            IntPtr lpInterface,
            IntPtr lpDestFolder,
            IntPtr ulUIParam,
            IntPtr lpProgress,
            uint ulFlags);
            
        [PreserveSig]
        uint DeleteMessages(
            IntPtr lpMsgList,
            IntPtr ulUIParam,
            IntPtr lpProgress,
            uint ulFlags);
            
        [PreserveSig]
        uint CreateFolder(
            uint ulFolderType,
            [MarshalAs(UnmanagedType.LPTStr)] string lpszFolderName,
            [MarshalAs(UnmanagedType.LPTStr)] string lpszFolderComment,
            IntPtr lpInterface,
            uint ulFlags,
            out IntPtr lppFolder);
            
        [PreserveSig]
        uint CopyFolder(
            uint cbEntryID,
            IntPtr lpEntryID,
            IntPtr lpInterface,
            IntPtr lpDestFolder,
            [MarshalAs(UnmanagedType.LPTStr)] string lpszNewFolderName,
            IntPtr ulUIParam,
            IntPtr lpProgress,
            uint ulFlags);
            
        [PreserveSig]
        uint DeleteFolder(
            uint cbEntryID,
            IntPtr lpEntryID,
            IntPtr ulUIParam,
            IntPtr lpProgress,
            uint ulFlags);
            
        [PreserveSig]
        uint SetReadFlags(
            IntPtr lpMsgList,
            IntPtr ulUIParam,
            IntPtr lpProgress,
            uint ulFlags);
            
        [PreserveSig]
        uint GetMessageStatus(
            uint cbEntryID,
            IntPtr lpEntryID,
            uint ulFlags,
            out uint lpulMessageStatus);
            
        [PreserveSig]
        uint SetMessageStatus(
            uint cbEntryID,
            IntPtr lpEntryID,
            uint ulNewStatus,
            uint ulNewStatusMask,
            out uint lpulOldStatus);
            
        [PreserveSig]
        uint SaveContentsSort(
            IntPtr lpSortCriteria,
            uint ulFlags);
            
        [PreserveSig]
        uint EmptyFolder(
            IntPtr ulUIParam,
            IntPtr lpProgress,
            uint ulFlags);
            
        [PreserveSig]
        uint GetContentsTable(uint ulFlags, out IntPtr lppTable);
        
        [PreserveSig]
        uint GetHierarchyTable(uint ulFlags, out IntPtr lppTable);
        
        [PreserveSig]
        uint OpenEntry(
            uint cbEntryID,
            IntPtr lpEntryID,
            IntPtr lpInterface,
            uint ulFlags,
            out uint lpulObjType,
            out IntPtr lppUnk);
            
        [PreserveSig]
        uint SetSearchCriteria(
            IntPtr lpRestriction,
            IntPtr lpContainerList,
            uint ulSearchFlags);
            
        [PreserveSig]
        uint GetSearchCriteria(
            uint ulFlags,
            out IntPtr lppRestriction,
            out IntPtr lppContainerList,
            out uint lpulSearchState);
    }
    
    [ComImport]
    [Guid("00020307-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMessage
    {
        [PreserveSig]
        uint GetLastError(uint hResult, uint ulFlags, out IntPtr lppMAPIError);
        
        [PreserveSig]
        uint SaveChanges(uint ulFlags);
        
        [PreserveSig]
        uint GetProps(
            IntPtr lpPropTagArray,
            uint ulFlags,
            out uint lpcValues,
            out IntPtr lppPropArray);
            
        [PreserveSig]
        uint GetPropList(uint ulFlags, out IntPtr lppPropTagArray);
        
        [PreserveSig]
        uint OpenProperty(
            uint ulPropTag,
            IntPtr lpiid,
            uint ulInterfaceOptions,
            uint ulFlags,
            out IntPtr lppUnk);
            
        [PreserveSig]
        uint SetProps(
            uint cValues,
            IntPtr lpPropArray,
            out IntPtr lppProblems);
            
        [PreserveSig]
        uint DeleteProps(
            IntPtr lpPropTagArray,
            out IntPtr lppProblems);
            
        [PreserveSig]
        uint CopyTo(
            uint ciidExclude,
            IntPtr rgiidExclude,
            IntPtr lpExcludeProps,
            IntPtr ulUIParam,
            IntPtr lpProgress,
            IntPtr lpInterface,
            IntPtr lpDestObj,
            uint ulFlags,
            out IntPtr lppProblems);
            
        [PreserveSig]
        uint CopyProps(
            IntPtr lpIncludeProps,
            IntPtr ulUIParam,
            IntPtr lpProgress,
            IntPtr lpInterface,
            IntPtr lpDestObj,
            uint ulFlags,
            out IntPtr lppProblems);
            
        [PreserveSig]
        uint GetNamesFromIDs(
            IntPtr lppPropTags,
            IntPtr lpPropSetGuid,
            uint ulFlags,
            out uint lpcPropNames,
            out IntPtr lpppPropNames);
            
        [PreserveSig]
        uint GetIDsFromNames(
            uint cPropNames,
            IntPtr lppPropNames,
            uint ulFlags,
            out IntPtr lppPropTags);
            
        [PreserveSig]
        uint GetAttachmentTable(uint ulFlags, out IntPtr lppTable);
        
        [PreserveSig]
        uint OpenAttach(
            uint ulAttachmentNum,
            IntPtr lpInterface,
            uint ulFlags,
            out IntPtr lppAttach);
            
        [PreserveSig]
        uint CreateAttach(
            IntPtr lpInterface,
            uint ulFlags,
            out uint lpulAttachmentNum,
            out IntPtr lppAttach);
            
        [PreserveSig]
        uint DeleteAttach(
            uint ulAttachmentNum,
            IntPtr ulUIParam,
            IntPtr lpProgress,
            uint ulFlags);
            
        [PreserveSig]
        uint GetRecipientTable(uint ulFlags, out IntPtr lppTable);
        
        [PreserveSig]
        uint ModifyRecipients(uint ulFlags, IntPtr lpMods);
        
        [PreserveSig]
        uint SubmitMessage(uint ulFlags);
        
        [PreserveSig]
        uint SetReadFlag(uint ulFlags);
    }
    
    [ComImport]
    [Guid("0002030B-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMAPITable
    {
        [PreserveSig]
        uint GetLastError(uint hResult, uint ulFlags, out IntPtr lppMAPIError);
        
        [PreserveSig]
        uint Advise(uint ulEventMask, IntPtr lpAdviseSink, out uint lpulConnection);
        
        [PreserveSig]
        uint Unadvise(uint ulConnection);
        
        [PreserveSig]
        uint GetStatus(out uint lpulTableStatus, out uint lpulTableType);
        
        [PreserveSig]
        uint SetColumns(IntPtr lpPropTagArray, uint ulFlags);
        
        [PreserveSig]
        uint QueryColumns(uint ulFlags, out IntPtr lpPropTagArray);
        
        [PreserveSig]
        uint GetRowCount(uint ulFlags, out uint lpulCount);
        
        [PreserveSig]
        uint SeekRow(uint bkOrigin, int lRowCount, out int lplRowsSought);
        
        [PreserveSig]
        uint SeekRowApprox(uint ulNumerator, uint ulDenominator);
        
        [PreserveSig]
        uint QueryPosition(out uint lpulRow, out uint lpulNumerator, out uint lpulDenominator);
        
        [PreserveSig]
        uint FindRow(IntPtr lpRestriction, uint bkOrigin, uint ulFlags);
        
        [PreserveSig]
        uint Restrict(IntPtr lpRestriction, uint ulFlags);
        
        [PreserveSig]
        uint CreateBookmark(out uint lpbkPosition);
        
        [PreserveSig]
        uint FreeBookmark(uint bkPosition);
        
        [PreserveSig]
        uint SortTable(IntPtr lpSortCriteria, uint ulFlags);
        
        [PreserveSig]
        uint QuerySortOrder(out IntPtr lppSortCriteria);
        
        [PreserveSig]
        uint QueryRows(int lRowCount, uint ulFlags, out IntPtr lppRows);
        
        [PreserveSig]
        uint Abort();
        
        [PreserveSig]
        uint ExpandRow(
            uint cbInstanceKey,
            IntPtr pbInstanceKey,
            uint ulRowCount,
            uint ulFlags,
            out IntPtr lppRows,
            out uint lpulMoreRows);
            
        [PreserveSig]
        uint CollapseRow(
            uint cbInstanceKey,
            IntPtr pbInstanceKey,
            uint ulFlags,
            out uint lpulRowCount);
            
        [PreserveSig]
        uint WaitForCompletion(uint ulFlags, uint ulTimeout, out uint lpulTableStatus);
        
        [PreserveSig]
        uint GetCollapseState(
            uint ulFlags,
            uint cbInstanceKey,
            IntPtr lpbInstanceKey,
            out uint lpcbCollapseState,
            out IntPtr lppbCollapseState);
            
        [PreserveSig]
        uint SetCollapseState(
            uint ulFlags,
            uint cbCollapseState,
            IntPtr pbCollapseState,
            out uint lpbkLocation);
    }
    
    #endregion
    
    #region MAPI Property Tags
    
    public static class MAPIPropertyTags
    {
        // Basic message properties
        public const uint PR_SUBJECT = 0x0037001E;
        public const uint PR_SENDER_NAME = 0x0C1A001E;
        public const uint PR_SENDER_EMAIL_ADDRESS = 0x0C1F001E;
        public const uint PR_MESSAGE_DELIVERY_TIME = 0x0E060040;
        public const uint PR_CLIENT_SUBMIT_TIME = 0x00390040;
        public const uint PR_MESSAGE_FLAGS = 0x0E070003;
        public const uint PR_MESSAGE_SIZE = 0x0E080003;
        public const uint PR_BODY = 0x1000001E;
        public const uint PR_BODY_HTML = 0x1013001E;
        public const uint PR_IMPORTANCE = 0x00170003;
        public const uint PR_PRIORITY = 0x00260003;
        public const uint PR_HASATTACH = 0x0E1B000B;
        
        // Folder properties
        public const uint PR_DISPLAY_NAME = 0x3001001E;
        public const uint PR_FOLDER_TYPE = 0x36010003;
        public const uint PR_CONTENT_COUNT = 0x36020003;
        public const uint PR_CONTENT_UNREAD = 0x36030003;
        public const uint PR_ENTRYID = 0x0FFF0102;
        
        // Calendar properties
        public const uint PR_START_DATE = 0x00600040;
        public const uint PR_END_DATE = 0x00610040;
        public const uint PR_LOCATION = 0x8208001E;
        public const uint PR_RECURRING = 0x8223000B;
        public const uint PR_ALL_DAY_EVENT = 0x8215000B;
        
        // Contact properties
        public const uint PR_GIVEN_NAME = 0x3A06001E;
        public const uint PR_SURNAME = 0x3A11001E;
        public const uint PR_COMPANY_NAME = 0x3A16001E;
        public const uint PR_TITLE = 0x3A17001E;
        public const uint PR_PRIMARY_TELEPHONE_NUMBER = 0x3A1A001E;
        public const uint PR_EMAIL_ADDRESS = 0x3003001E;
        
        // Task properties
        public const uint PR_TASK_DUE_DATE = 0x8105001E;
        public const uint PR_TASK_COMPLETE = 0x811C000B;
        public const uint PR_TASK_STATUS = 0x81010003;
        
        // Message flags
        public const uint MSGFLAG_READ = 0x00000001;
        public const uint MSGFLAG_UNMODIFIED = 0x00000002;
        public const uint MSGFLAG_SUBMIT = 0x00000004;
        public const uint MSGFLAG_UNSENT = 0x00000008;
        public const uint MSGFLAG_HASATTACH = 0x00000010;
        
        // Store properties
        public const uint PR_STORE_ENTRYID = 0x0FFB0102;
        public const uint PR_STORE_RECORD_KEY = 0x0FFA0102;
        public const uint PR_STORE_SUPPORT_MASK = 0x340D0003;
        public const uint PR_MDB_PROVIDER = 0x34140102;
        
        // Property types
        public const uint PT_UNSPECIFIED = 0x0000;
        public const uint PT_NULL = 0x0001;
        public const uint PT_I2 = 0x0002;
        public const uint PT_LONG = 0x0003;
        public const uint PT_R4 = 0x0004;
        public const uint PT_DOUBLE = 0x0005;
        public const uint PT_CURRENCY = 0x0006;
        public const uint PT_APPTIME = 0x0007;
        public const uint PT_ERROR = 0x000A;
        public const uint PT_BOOLEAN = 0x000B;
        public const uint PT_OBJECT = 0x000D;
        public const uint PT_I8 = 0x0014;
        public const uint PT_STRING8 = 0x001E;
        public const uint PT_UNICODE = 0x001F;
        public const uint PT_SYSTIME = 0x0040;
        public const uint PT_CLSID = 0x0048;
        public const uint PT_BINARY = 0x0102;
        public const uint PT_MV_I2 = 0x1002;
        public const uint PT_MV_LONG = 0x1003;
        public const uint PT_MV_R4 = 0x1004;
        public const uint PT_MV_DOUBLE = 0x1005;
        public const uint PT_MV_CURRENCY = 0x1006;
        public const uint PT_MV_APPTIME = 0x1007;
        public const uint PT_MV_SYSTIME = 0x1040;
        public const uint PT_MV_STRING8 = 0x101E;
        public const uint PT_MV_BINARY = 0x1102;
        public const uint PT_MV_UNICODE = 0x101F;
        public const uint PT_MV_CLSID = 0x1048;
    }
    
    #endregion
    
    #region MAPI Structures
    
    [StructLayout(LayoutKind.Sequential)]
    public struct SPropValue
    {
        public uint ulPropTag;
        public uint dwAlignPad;
        public SPropValueUnion Value;
    }
    
    [StructLayout(LayoutKind.Explicit)]
    public struct SPropValueUnion
    {
        [FieldOffset(0)] public short i;
        [FieldOffset(0)] public int l;
        [FieldOffset(0)] public float flt;
        [FieldOffset(0)] public double dbl;
        [FieldOffset(0)] public short b;
        [FieldOffset(0)] public long ft;
        [FieldOffset(0)] public IntPtr lpszA;
        [FieldOffset(0)] public IntPtr bin;
        [FieldOffset(0)] public IntPtr lpszW;
        [FieldOffset(0)] public IntPtr lpguid;
        [FieldOffset(0)] public long li;
        [FieldOffset(0)] public uint err;
        [FieldOffset(0)] public IntPtr MVi;
        [FieldOffset(0)] public IntPtr MVl;
        [FieldOffset(0)] public IntPtr MVflt;
        [FieldOffset(0)] public IntPtr MVdbl;
        [FieldOffset(0)] public IntPtr MVcur;
        [FieldOffset(0)] public IntPtr MVat;
        [FieldOffset(0)] public IntPtr MVft;
        [FieldOffset(0)] public IntPtr MVlpszA;
        [FieldOffset(0)] public IntPtr MVbin;
        [FieldOffset(0)] public IntPtr MVlpszW;
        [FieldOffset(0)] public IntPtr MVguid;
        [FieldOffset(0)] public IntPtr MVli;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct SBinary
    {
        public uint cb;
        public IntPtr lpb;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct SRowSet
    {
        public uint cRows;
        public IntPtr aRow; // SRow array
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct SRow
    {
        public uint ulAdrEntryPad;
        public uint cValues;
        public IntPtr lpProps; // SPropValue array
    }
    
    #endregion
}