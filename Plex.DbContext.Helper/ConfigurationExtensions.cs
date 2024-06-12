using Microsoft.Extensions.Primitives;
using System.Text;

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
}
