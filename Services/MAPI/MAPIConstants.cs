using System;

namespace QuadroAIPilot.Services.MAPI
{
    /// <summary>
    /// MAPI constants ve enumerations
    /// </summary>
    public static class MAPIConstants
    {
        // MAPI Initialization flags
        public const uint MAPI_INIT_VERSION = 0x00000000;
        public const uint MAPI_MULTITHREAD_NOTIFICATIONS = 0x00000001;
        
        // MAPI Logon flags
        public const uint MAPI_LOGON_UI = 0x00000001;
        public const uint MAPI_NEW_SESSION = 0x00000002;
        public const uint MAPI_EXTENDED = 0x00000020;
        public const uint MAPI_USE_DEFAULT = 0x00000040;
        public const uint MAPI_EXPLICIT_PROFILE = 0x00000010;
        
        // MAPI Error codes
        public const uint S_OK = 0x00000000;
        public const uint MAPI_E_SUCCESS = 0x00000000;
        public const uint MAPI_E_FAILURE = 0x80004005;
        public const uint MAPI_E_INSUFFICIENT_MEMORY = 0x8007000E;
        public const uint MAPI_E_ACCESS_DENIED = 0x80070005;
        public const uint MAPI_E_USER_CANCEL = 0x80040113;
        public const uint MAPI_E_UNKNOWN_FLAGS = 0x80040106;
        public const uint MAPI_E_INVALID_PARAMETER = 0x80070057;
        public const uint MAPI_E_INTERFACE_NOT_SUPPORTED = 0x80004002;
        public const uint MAPI_E_NO_SUPPORT = 0x80040102;
        
        // MAPI Store access flags
        public const uint MDB_WRITE = 0x00000004;
        public const uint MAPI_BEST_ACCESS = 0x00000010;
        public const uint MAPI_MODIFY = 0x00000001;
        
        // MAPI Property access flags
        public const uint MAPI_UNICODE = 0x80000000;
        
        // MAPI Property types
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
        
        // MAPI Folder types
        public const uint FOLDER_INBOX = 1;
        public const uint FOLDER_OUTBOX = 2;
        public const uint FOLDER_SENTMAIL = 3;
        public const uint FOLDER_DELETEDITEMS = 4;
        public const uint FOLDER_CALENDAR = 9;
        public const uint FOLDER_CONTACTS = 10;
        public const uint FOLDER_TASKS = 13;
        public const uint FOLDER_NOTES = 12;
        
        // Message flags
        public const uint MSGFLAG_READ = 0x00000001;
        public const uint MSGFLAG_UNMODIFIED = 0x00000002;
        public const uint MSGFLAG_SUBMIT = 0x00000004;
        public const uint MSGFLAG_UNSENT = 0x00000008;
        public const uint MSGFLAG_HASATTACH = 0x00000010;
        public const uint MSGFLAG_FROMME = 0x00000020;
        public const uint MSGFLAG_ASSOCIATED = 0x00000040;
        public const uint MSGFLAG_RESEND = 0x00000080;
        
        // MAPI Table flags
        public const uint MAPI_DEFERRED_ERRORS = 0x00000008;
        public const uint MAPI_ASSOCIATED = 0x00000040;
        
        // Property tags (commonly used)
        public const uint PR_ENTRYID = 0x0FFF0102;
        public const uint PR_OBJECT_TYPE = 0x0FFE0003;
        public const uint PR_DISPLAY_NAME = 0x3001001E;
        public const uint PR_SUBJECT = 0x0037001E;
        public const uint PR_MESSAGE_CLASS = 0x001A001E;
        public const uint PR_BODY = 0x1000001E;
        public const uint PR_BODY_HTML = 0x1013001E;
        public const uint PR_SENDER_NAME = 0x0C1A001E;
        public const uint PR_SENDER_EMAIL_ADDRESS = 0x0C1F001E;
        public const uint PR_SENT_REPRESENTING_NAME = 0x0042001E;
        public const uint PR_SENT_REPRESENTING_EMAIL_ADDRESS = 0x0065001E;
        public const uint PR_MESSAGE_DELIVERY_TIME = 0x0E060040;
        public const uint PR_CLIENT_SUBMIT_TIME = 0x00390040;
        public const uint PR_MESSAGE_FLAGS = 0x0E070003;
        public const uint PR_MESSAGE_SIZE = 0x0E080003;
        public const uint PR_IMPORTANCE = 0x00170003;
        public const uint PR_PRIORITY = 0x00260003;
        public const uint PR_SENSITIVITY = 0x00360003;
        public const uint PR_HASATTACH = 0x0E1B000B;
        public const uint PR_ATTACHMENT_TABLE = 0x3690000D;
        
        // Store properties
        public const uint PR_STORE_ENTRYID = 0x0FFB0102;
        public const uint PR_STORE_RECORD_KEY = 0x0FFA0102;
        public const uint PR_STORE_SUPPORT_MASK = 0x340D0003;
        public const uint PR_MDB_PROVIDER = 0x34140102;
        
        // Folder properties
        public const uint PR_CONTENT_COUNT = 0x36020003;
        public const uint PR_CONTENT_UNREAD = 0x36030003;
        public const uint PR_FOLDER_TYPE = 0x36010003;
        
        // Calendar specific properties
        public const uint PR_START_TIME = 0x60060040;
        public const uint PR_END_TIME = 0x60070040;
        public const uint PR_LOCATION = 0x6008001E;
        public const uint PR_APPOINTMENT_DURATION = 0x60050003;
        public const uint PR_APPOINTMENT_STATE_FLAGS = 0x60070003;
        public const uint PR_RECURRING = 0x6005000B;
        
        // Contact specific properties
        public const uint PR_GIVEN_NAME = 0x3A06001E;
        public const uint PR_SURNAME = 0x3A11001E;
        public const uint PR_EMAIL_ADDRESS = 0x3003001E;
        public const uint PR_BUSINESS_TELEPHONE_NUMBER = 0x3A08001E;
        public const uint PR_MOBILE_TELEPHONE_NUMBER = 0x3A1C001E;
        public const uint PR_COMPANY_NAME = 0x3A16001E;
        public const uint PR_TITLE = 0x3A17001E;
        public const uint PR_DEPARTMENT_NAME = 0x3A18001E;
        
        // Importance levels
        public const uint IMPORTANCE_LOW = 0;
        public const uint IMPORTANCE_NORMAL = 1;
        public const uint IMPORTANCE_HIGH = 2;
        
        // Priority levels
        public const int PRIORITY_LOW = -1;
        public const int PRIORITY_NORMAL = 0;
        public const int PRIORITY_HIGH = 1;
        
        // Sensitivity levels
        public const uint SENSITIVITY_NONE = 0;
        public const uint SENSITIVITY_PERSONAL = 1;
        public const uint SENSITIVITY_PRIVATE = 2;
        public const uint SENSITIVITY_CONFIDENTIAL = 3;
    }
    
    /// <summary>
    /// MAPI Folder types enumeration
    /// </summary>
    public enum MAPIFolderType
    {
        Inbox = 1,
        Outbox = 2,
        SentMail = 3,
        DeletedItems = 4,
        Calendar = 9,
        Contacts = 10,
        Tasks = 13,
        Notes = 12
    }
    
    /// <summary>
    /// MAPI Message importance levels
    /// </summary>
    public enum MAPIImportance
    {
        Low = 0,
        Normal = 1,
        High = 2
    }
    
    /// <summary>
    /// MAPI Message priority levels
    /// </summary>
    public enum MAPIPriority
    {
        Low = -1,
        Normal = 0,
        High = 1
    }
    
    /// <summary>
    /// MAPI Message sensitivity levels
    /// </summary>
    public enum MAPISensitivity
    {
        None = 0,
        Personal = 1,
        Private = 2,
        Confidential = 3
    }
    
    /// <summary>
    /// MAPI Object types
    /// </summary>
    public enum MAPIObjectType
    {
        Store = 1,
        AddressBook = 2,
        Folder = 3,
        AddressBookContainer = 4,
        Message = 5,
        MailUser = 6,
        Attachment = 7,
        DistributionList = 8,
        Profile = 9,
        Status = 10,
        Session = 11
    }
}