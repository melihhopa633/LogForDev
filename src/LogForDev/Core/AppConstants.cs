namespace LogForDev.Core;

public static class AppConstants
{
    public static class Auth
    {
        public const string CookieScheme = "Cookie";
        public const string ApiKeyScheme = "ApiKey";
        public const string CookieName = ".LogForDev.Auth";
        public const string DataProtectorPurpose = "Auth.Cookie";
        public const string DashboardOnlyPolicy = "DashboardOnly";
    }

    public static class Database
    {
        public const string HttpContextProjectKey = "Project";
    }

    public static class Paths
    {
        public const string Login = "/login";
        public const string Setup = "/setup";
        public const string ApiAuth = "/api/auth";
        public const string ApiSetup = "/api/setup";
    }
}
