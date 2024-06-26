﻿using System.Collections.Concurrent;
using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;
using static Plex.Extensions.DbContext.Constants;

namespace Plex.Extensions.DbContext;
public static class ConfigurationExtensions
{
    public static (string DbProvider, string ConnectionString) GetDynamicConnectionString(this IConfiguration? configuration,
                                                                HttpRequest? request,
                                                                ILogger? logger = null,
                                                                Dictionary<string, string>? dbProviderMappings = null)
    {
        if (configuration == null) return ("", "");
        string connectionStrKey = configuration[ConnectionStringKey] ?? ConnectionString;
        string connectionString = configuration.GetConfigValue(connectionStrKey);

        string? currentDbName = connectionString.GetDatabaseName(logger);

        string? dbName = request.GetHeaderValue(DbNameHeaderKey);
        if (!string.IsNullOrWhiteSpace(dbName))
        {
            connectionString = connectionString.Replace("%db%", dbName.ToString());
            connectionString = !string.IsNullOrWhiteSpace(currentDbName) ?
                                connectionString.Replace(currentDbName, dbName.ToString()) : connectionString;

            currentDbName = dbName;
        }
        string? dbServerName = request.GetHeaderValue(DbServerHeaderKey);
        if (!string.IsNullOrWhiteSpace(dbServerName))
        {
            connectionString = connectionString.Replace("%server%", dbServerName.ToString());
        }

        string dbProvider = string.IsNullOrWhiteSpace(currentDbName) || dbProviderMappings == null 
                            || dbProviderMappings.Count == 0 ? MSSQL : dbProviderMappings[currentDbName];

        return (dbProvider, connectionString);
    }

    private static readonly ConcurrentDictionary<string, string> _keyValuePairs = new();
    public static string GetConfigValue(this IConfiguration? configuration,
                                        string key,
                                        string defaultValue = "",
                                        string settingName = "AppSetting")
    {
        if (configuration == null) return defaultValue;

        string asSecretKey = $"{settingName}-{key}";
        if (settingName.Equals("AppSettings", StringComparison.InvariantCultureIgnoreCase))
        {
            asSecretKey = $"{settingName[..^1]}-{key}";
        }
        string evKey = $"{settingName}__{key}";
        string settingKey = $"{settingName}:{key}";

        if (_keyValuePairs.ContainsKey(asSecretKey)) return _keyValuePairs[asSecretKey];
        if (_keyValuePairs.ContainsKey(evKey)) return _keyValuePairs[evKey];
        if (_keyValuePairs.ContainsKey(settingKey)) return _keyValuePairs[settingKey];
        if (_keyValuePairs.ContainsKey(key)) return _keyValuePairs[key];

        string? value = configuration[asSecretKey];
        if (!string.IsNullOrWhiteSpace(value))
        {
            value = value.ToExpandEnvironmentVariable();
            _keyValuePairs.TryAdd(asSecretKey, value);
            return value;
        }

        value = Environment.GetEnvironmentVariable(evKey);
        if (!string.IsNullOrWhiteSpace(value))
        {
            value = value.ToExpandEnvironmentVariable();
            _keyValuePairs.TryAdd(evKey, value);
            return value;
        }

        value = configuration.GetValue<string>(settingKey);
        if (!string.IsNullOrWhiteSpace(value))
        {
            value = value.ToExpandEnvironmentVariable();
            _keyValuePairs.TryAdd(settingKey, value);
            return value;
        }

        value = configuration.GetValue<string>(key) ?? defaultValue;
        _keyValuePairs.TryAdd(key, value);
        return value;
    }
    static string ToExpandEnvironmentVariable(this string? variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName)) return "";
        return Environment.ExpandEnvironmentVariables(variableName);
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
    static string? GetDatabaseName(this string connectionString,
                                   ILogger? logger = null)
    {
        // Attempt to parse as key-value pair connection string
        try
        {
            DbConnectionStringBuilder dbConnectionStringBuilder = new()
            {
                ConnectionString = connectionString
            };

            if (dbConnectionStringBuilder.ContainsKey("Database"))
            {
                return dbConnectionStringBuilder["Database"].ToString();
            }
            if (dbConnectionStringBuilder.ContainsKey("Initial Catalog")) // SQL Server alternative key
            {
                return dbConnectionStringBuilder["Initial Catalog"].ToString();
            }
        }
        catch (Exception ex)
        {
            // Handle exception if it's not a standard key-value pair connection string
            logger?.LogError(ex.Message, ex);
        }

        // Handle MongoDB connection string separately if it's not parsed by DbConnectionStringBuilder
        if (connectionString.StartsWith("mongodb://", StringComparison.OrdinalIgnoreCase)
            || connectionString.StartsWith("mongodb+srv://", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString.GetMongoDbname();
        }

        return null;
    }

    static string? GetMongoDbname(this string connectionString)
    {
        // Regular expression to match the database name in the connection string
        string pattern = @"mongodb(?:\+srv)?:\/\/[^\/]+\/([^\/?]+)";
        Match match = Regex.Match(connectionString, pattern);

        if (match.Success && match.Groups.Count > 1)
        {
            return match.Groups[1].Value;
        }

        return null;
    }
}
