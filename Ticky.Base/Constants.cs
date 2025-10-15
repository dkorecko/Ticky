namespace Ticky.Base
{
    public static class Constants
    {
        public static bool SMTP_ENABLED = true;
        public static bool FULLY_OFFLINE;
        public static bool DISABLE_USER_SIGNUPS = false;
        public static string BASE_URL = string.Empty;
#if DEBUG
        public const string APP_NAME = "Ticky [DEV]";
#else
        public const string APP_NAME = "Ticky";
#endif

        public static class CascadingParameters
        {
            public const string CurrentAccount = "CurrentAccount";
            public const string MainLayout = "MainLayout";
        }

        public static class Defaults
        {
            public const string ADMIN_EMAIL = "admin@ticky.com";
            public const string ADMIN_PASSWORD = "abc123";
        }

        public static class Emails
        {
            public static readonly string BASE_PATH = Path.Combine(WWW_ROOT, "emails");

            public static class Mappings
            {
                public const string VERIFICATION_CODE = "{VERIFICATION_CODE}";
                public const string CARD_CODE = "{CARD_CODE}";
                public const string CARD_TEXT = "{CARD_TEXT}";
                public const string CARD_SCHEDULED_FOR = "{CARD_SCHEDULED_FOR}";
                public const string CARD_DESCRIPTION = "{CARD_DESCRIPTION}";
                public const string CARD_SUBTASKS = "{CARD_SUBTASKS}";
                public const string CARD_URL = "{CARD_URL}";
                public const string BASE_URL = "{BASE_URL}";
                public const string CARD_DEADLINE = "{CARD_DEADLINE}";
                public const string CARD_DEADLINE_SECTION = "{CARD_DEADLINE_SECTION}";
                public const string CARD_DESCRIPTION_SECTION = "{CARD_DESCRIPTION_SECTION}";
                public const string CARD_SUBTASKS_SECTION = "{CARD_SUBTASKS_SECTION}";
                public const string SUBTASK_TEXT = "{SUBTASK_TEXT}";
                public const string SUBTASK_ICON = "{SUBTASK_ICON}";
                public const string SUBTASK_ICON_COLOR = "{SUBTASK_ICON_COLOR}";
                public const string SUBTASK_COMPLETED_CLASS = "{SUBTASK_COMPLETED_CLASS}";
            }
        }

        public static class Hubs
        {
            public const string UPDATE_HUB = "/updatehub";
        }

        public static class Limits
        {
            public const int MINIMUM_SECOND_HOSTED_SERVICE_DELAY = 15;
            public const int DEFAULT_NOTIFICATION_TIME_IN_MS = 5000;
            public const int FILE_NAME_LENGTH = 10;
            public const long MAX_FILE_SIZE = 15360 * 1024;
            public const long MAX_IMAGE_SIZE = MAX_FILE_SIZE;
            public const long MAX_JSON_SIZE = MAX_FILE_SIZE;
            public const int MAX_FILES = 20;
            public const int DEBOUNCE_TIME_IN_MS = 1000;
        }

        public static class Mappings
        {
            public const string LOGIN_PATH = "/auth/login";
            public const string LOGOUT_PATH = "/auth/logout";
            public const string BOARD_PATH = "/boards";
            public const string ATTACHMENTS_API_PATH = "/api/attachments";
            public const string ATTACHMENTS_DOWNLOAD_PATH = ATTACHMENTS_API_PATH + "/download";
        }

        public static class Policies
        {
            public const string RequireAdmin = "RequireAdmin";
        }

        public static class Roles
        {
            public const string Admin = "Admin";
        }

        public static class StorageKeys
        {
            public const string BoardPreferences = "Ticky_BoardPreferences";
            public const string FilterPreferencesPrefix = "Ticky_FilterPreferences";
        }

        public static readonly string WWW_ROOT = $"{AppDomain.CurrentDomain.BaseDirectory}/wwwroot";
        public static readonly string INFORMATION_PATH = Path.Combine(WWW_ROOT, "information.json");
        public static readonly string SAVE_UPLOADED_PATH = $"wwwroot/uploaded";
        public static readonly string SAVE_UPLOADED_IMAGES_PATH = $"{SAVE_UPLOADED_PATH}/images";
        public static readonly string SAVE_UPLOADED_FILES_PATH = $"{SAVE_UPLOADED_PATH}/files";
        public static readonly string ACCESS_UPLOADED_PATH = $"./uploaded";
        public static readonly string ACCESS_UPLOADED_IMAGES_PATH =
            $"{ACCESS_UPLOADED_PATH}/images";
        public static readonly string ACCESS_UPLOADED_FILES_PATH = $"{ACCESS_UPLOADED_PATH}/files";

        public const string REPEATED_KEY = "is repeated by";

        public static readonly Dictionary<string, string> LINK_TYPE_PAIRS =
            new()
            {
                { "is blocked by", "blocks" },
                { "is tested by", "tests" },
                { "relates to", "relates to" },
                { REPEATED_KEY, "repeats" }
            };
    }
}
