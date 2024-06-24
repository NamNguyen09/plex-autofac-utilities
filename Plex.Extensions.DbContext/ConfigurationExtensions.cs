using System.Data.Common;
using System.Text;
using Microsoft.Extensions.Primitives;
using static Plex.Extensions.DbContext.Constants;

namespace Plex.Extensions.DbContext;
public static class ConfigurationExtensions
{	
	public static string GetDynamicConnectionString(this IConfiguration? configuration,
													HttpRequest? request)
	{
		if (configuration == null) return configuration.GetAppSettingValue(ConnectionString);
		StringBuilder connectionNameBuilder = new StringBuilder().Append(configuration[ConnectionStringKey] ?? ConnectionString);
		string connectionString = $"{configuration.GetAppSettingValue(connectionNameBuilder.ToString())};TrustServerCertificate=True";
		DbConnectionStringBuilder dbConnectionStringBuilder = new()
		{
			ConnectionString = connectionString
		};

		string? dbName = request.GetHeaderValue(DbNameHeaderKey);
		if (!string.IsNullOrWhiteSpace(dbName)
			&& dbConnectionStringBuilder.ContainsKey(Database))
		{
			dbConnectionStringBuilder[Database] = dbName.ToString();
		}

		string? dbServerName = request.GetHeaderValue(DbServerHeaderKey);
		if (!string.IsNullOrWhiteSpace(dbServerName)
			&& dbConnectionStringBuilder.ContainsKey(Server))
		{
			dbConnectionStringBuilder[Server] = dbServerName.ToString();
		}

		return dbConnectionStringBuilder.ConnectionString;
	}
	public static string GetAppSettingValue(this IConfiguration? configuration, string key,
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

	static string? GetHeaderValue(this HttpRequest? request, string key)
	{
		if (request == null || request.Headers == null) return null;

		if (request.Headers.TryGetValue(key, out StringValues value))
		{
			return value;
		}

		return null;
	}
}
