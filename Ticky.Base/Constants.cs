namespace Ticky.Base
{
    public static class Constants
    {
        public static bool SMTP_ENABLED = true;
        public static bool FULLY_OFFLINE;
        public static bool DISABLE_USER_SIGNUPS = false;

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
            }
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
        }

        public static class Mappings
        {
            public const string LOGIN_PATH = "/auth/login";
            public const string LOGOUT_PATH = "/auth/logout";
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
        }

        public static readonly string WWW_ROOT = $"{AppDomain.CurrentDomain.BaseDirectory}/wwwroot";
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
