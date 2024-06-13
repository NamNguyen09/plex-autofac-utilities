namespace Plex.Extensions.DbContext;
internal static class Constants
{
    internal const string CommandTimeOut = "Command Timeout";
    internal const string DefaultCommandTimeOutValue = "300";
    internal const string Postgresql = "postgresql";
    internal const string MSSQL = "mssql";
    internal const string Host = "host";
    internal const string Server = "server";
    internal static class AppSettingKeys
    {
        internal const string CommandTimeOut = "EfSqlCommandTimeOutInSecond";
        internal const string UseLazyLoading = "EfUseLazyLoading";
        internal const string UseChangeTrackingProxies = "EfUseChangeTrackingProxies";
        internal const string UseQueryTrackingBehavior = "EfUseQueryTrackingBehavior";
        internal const string SqlMaxRetryOnFailureCount = "EfSqlMaxRetryOnFailureCount";
        internal const string EnableMigration = "EfEnableMigration";
    }
}
