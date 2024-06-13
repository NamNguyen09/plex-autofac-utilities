using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Plex.DbContext.Helper;
public static class ServiceCollectionExtension
{
    public static WebApplicationBuilder RegisterDbContext<TDbContext>(this WebApplicationBuilder builder)
                                        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        builder.Services.AddDbContextPool<TDbContext>((sp, options) =>
        {
            bool useLazyLoading = Convert.ToBoolean(builder.Configuration.GetAppSettingValue("EfUseLazyLoading", defaultValue: "false"));
            bool useChangeTrackingProxies = Convert.ToBoolean(builder.Configuration.GetAppSettingValue("EfUseChangeTrackingProxies", defaultValue: "false"));
            options.UseLazyLoadingProxies(useLazyLoading);
            options.UseChangeTrackingProxies(useChangeTrackingProxies);
            bool useQueryTrackingBehavior = Convert.ToBoolean(builder.Configuration.GetAppSettingValue("EfUseQueryTrackingBehavior", defaultValue: "true"));
            if (!useQueryTrackingBehavior) options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            options.UseDbProvider<TDbContext>(sp, builder);
        });

        return builder;
    }
    public static WebApplicationBuilder RegisterDbContextWithCache<TDbContext, TCacheInterceptor>(
                                                      this WebApplicationBuilder builder)
                                                      where TDbContext : Microsoft.EntityFrameworkCore.DbContext
                                                      where TCacheInterceptor : DbCommandInterceptor
    {
        builder.Services.AddDbContextPool<TDbContext>((sp, options) =>
        {
            var efCoreCacheInterceptor = sp.GetService<TCacheInterceptor>();
            if (efCoreCacheInterceptor != null) options.AddInterceptors(efCoreCacheInterceptor);
            bool useLazyLoading = Convert.ToBoolean(builder.Configuration.GetAppSettingValue("EfUseLazyLoading", defaultValue: "false"));
            bool useChangeTrackingProxies = Convert.ToBoolean(builder.Configuration.GetAppSettingValue("EfUseChangeTrackingProxies", defaultValue: "false"));
            options.UseLazyLoadingProxies(useLazyLoading);
            options.UseChangeTrackingProxies(useChangeTrackingProxies);
            bool useQueryTrackingBehavior = Convert.ToBoolean(builder.Configuration.GetAppSettingValue("EfUseQueryTrackingBehavior", defaultValue: "true"));
            if (!useQueryTrackingBehavior) options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            options.UseDbProvider<TDbContext>(sp, builder);
        });

        return builder;
    }
    static DbContextOptionsBuilder UseDbProvider<TDbContext>(this DbContextOptionsBuilder dbContextOptBuilder,
                                                            IServiceProvider sp, WebApplicationBuilder builder)
                                                            where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        var context = sp.GetService<IHttpContextAccessor>();
        string connectionString = builder.Configuration.GetDynamicConnectionString(context?.HttpContext?.Request.Headers);

        string provider = "mssql";
        if (connectionString.Contains("server") || connectionString.Contains("host")) provider = "postgresql";

        bool enableMigration = Convert.ToBoolean(builder.Configuration.GetAppSettingValue("EfEnableMigration", defaultValue: "false"));
        string? migrationsAssembly = null;
        if (enableMigration)
        {
            Assembly assembly = typeof(TDbContext).GetTypeInfo().Assembly;
            migrationsAssembly = assembly == null || assembly.GetName() == null ? "" : assembly.GetName().Name;
        }

        int commandTimeOut = Convert.ToInt32(builder.Configuration.GetAppSettingValue("EfSqlCommandTimeOutInSecond", defaultValue: "300"));
        int maxRetryCount = Convert.ToInt32(builder.Configuration.GetAppSettingValue("EfSqlMaxRetryOnFailureCount", defaultValue: "0"));
        switch (provider)
        {
            case "postgresql":
                dbContextOptBuilder.UseNpgsql(connectionString, options =>
                   {
                       options.CommandTimeout(commandTimeOut);
                       if (!string.IsNullOrWhiteSpace(migrationsAssembly)) options.MigrationsAssembly(migrationsAssembly);
                       if (maxRetryCount > 0) options.EnableRetryOnFailure(maxRetryCount);
                   });
                break;
            default:
                dbContextOptBuilder.UseSqlServer(connectionString, options =>
                {
                    options.CommandTimeout(commandTimeOut);
                    if (!string.IsNullOrWhiteSpace(migrationsAssembly)) options.MigrationsAssembly(migrationsAssembly);
                    if (maxRetryCount > 0) options.EnableRetryOnFailure(maxRetryCount);
                });
                break;
        }

        return dbContextOptBuilder;
    }
}
