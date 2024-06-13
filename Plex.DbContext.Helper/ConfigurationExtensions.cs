using System.Text;
using Microsoft.Extensions.Primitives;

namespace Plex.DbContext.Helper;
public static class ConfigurationExtensions
{
    const string DbNameKey = "cx-db";
    public static string GetDynamicConnectionString(this IConfigurationManager configuration,
                                                   IDictionary<string, StringValues>? httpRequestHeaders)
    {
        StringBuilder connectionNameBuilder = new StringBuilder().Append(configuration["ConnectionStringKey"] ?? "ConnectionString");
        if (httpRequestHeaders != null && httpRequestHeaders.TryGetValue(DbNameKey, out StringValues value))
        {
            string? appName = value;
            if (!string.IsNullOrWhiteSpace(appName)) connectionNameBuilder.Append($"-{appName.ToLower()}");
        }

        return $"{configuration[connectionNameBuilder.ToString()]};TrustServerCertificate=True" ?? "";
    }
    public static string GetAppSettingValue(this IConfigurationManager? configuration, string key,
                                            string settingName = "AppSetting",
                                            string defaultValue = "")
    {
        if (configuration == null) return defaultValue;

        string asSecretKey = $"{settingName}-{key}";
        if (settingName.Equals("AppSettings", StringComparison.InvariantCultureIgnoreCase))
        {
            asSecretKey = $"{settingName[..^1]}-{key}";
        }
        string evKey = $"{settingName}__{key}";
        string settingKey = $"{settingName}:{key}";

        string? value = configuration[asSecretKey];
        if (!string.IsNullOrWhiteSpace(value))
        {
            value = Environment.ExpandEnvironmentVariables(value);
            return value;
        }

        value = Environment.GetEnvironmentVariable(evKey);
        if (!string.IsNullOrWhiteSpace(value))
        {
            value = Environment.ExpandEnvironmentVariables(value);
            return value;
        }

        value = configuration.GetValue<string>(settingKey);
        if (!string.IsNullOrWhiteSpace(value))
        {
            value = Environment.ExpandEnvironmentVariables(value);
            return value;
        }

        value = configuration.GetValue<string>(key) ?? defaultValue;
        return value;
    }
}
