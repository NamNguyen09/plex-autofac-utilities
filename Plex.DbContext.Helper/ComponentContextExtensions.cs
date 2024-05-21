using System.Reflection;
using System.Text;
using Autofac;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Plex.DbContext.Helper;
public static class ComponentContextExtensions
{
    const string DbNameKey = "cx-db";
    public static TContext RegisterDbContext<TContext>(
              this IComponentContext c,
              string connectionString,
              bool useLazyLoading = false,
              bool enableMigration = false)
              where TContext : Microsoft.EntityFrameworkCore.DbContext
    {
        var optBuilder = GetDbContextOptions<TContext>(c, connectionString, useLazyLoading);
        return (TContext)(Activator.CreateInstance(typeof(TContext), optBuilder.Options, enableMigration) ?? new());
    }

    public static TContext RegisterDbContextWithEfCoreCache<TContext, TCacheInterceptor>(
                  this IComponentContext c,
                  string connectionString,
                  bool useLazyLoading = false,
                  bool enableMigration = false)
                  where TContext : Microsoft.EntityFrameworkCore.DbContext
                  where TCacheInterceptor : DbCommandInterceptor
    {
        var optBuilder = GetDbContextOptions<TContext>(c, connectionString, useLazyLoading, enableMigration);
        if (c.IsRegistered(typeof(TCacheInterceptor)))
        {
            var efCoreCacheInterceptor = c.Resolve<TCacheInterceptor>();
            optBuilder.AddInterceptors(efCoreCacheInterceptor);
        }

        return (TContext)(Activator.CreateInstance(typeof(TContext), optBuilder.Options, enableMigration) ?? new());
    }
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
    static DbContextOptionsBuilder<TContext> GetDbContextOptions<TContext>(IComponentContext c,
                                                                        string connectionString,
                                                                        bool useLazyLoading = false,
                                                                        bool enableMigration = false)
                                                                        where TContext : Microsoft.EntityFrameworkCore.DbContext
    {
        var dbContextOptBuilder = new DbContextOptionsBuilder<TContext>();
        dbContextOptBuilder.UseLazyLoadingProxies(useLazyLoading);

        string provider = "mssql";
        if (connectionString.Contains("server") || connectionString.Contains("host")) provider = "postgresql";

        string? migrationsAssembly = null;
        if (enableMigration)
        {
            Assembly assembly = typeof(TContext).GetTypeInfo().Assembly;
            migrationsAssembly = assembly == null || assembly.GetName() == null ? "" : assembly.GetName().Name;
        }

        switch (provider)
        {
            case "postgresql":
                if (!enableMigration)
                {
                    dbContextOptBuilder.UseNpgsql(connectionString);
                    break;
                }

                dbContextOptBuilder.UseNpgsql(connectionString, p => p.MigrationsAssembly(migrationsAssembly));
                break;
            default:
                if (!enableMigration)
                {
                    dbContextOptBuilder.UseSqlServer(connectionString);
                    break;
                }

                dbContextOptBuilder.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly));
                break;
        }

        return dbContextOptBuilder;
    }
}
