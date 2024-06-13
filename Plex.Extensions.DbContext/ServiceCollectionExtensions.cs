using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static Plex.Extensions.DbContext.Constants;
using DbContextBase = Microsoft.EntityFrameworkCore.DbContext;

namespace Plex.Extensions.DbContext;
public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterDbContextPool<TContext>(this IServiceCollection services)
                                        where TContext : DbContextBase
    {
        services.AddDbContextPool<TContext>((sp, options) =>
        {
            options.AppendOptions<TContext>(sp);
        });

        return services;
    }
    public static IServiceCollection RegisterDbContextPool<TContext, TCacheInterceptor>(this IServiceCollection services)
                                                    where TContext : DbContextBase
                                                    where TCacheInterceptor : DbCommandInterceptor
    {
        services.AddDbContextPool<TContext>((sp, options) =>
        {
            var efCoreCacheInterceptor = sp.GetService<TCacheInterceptor>();
            if (efCoreCacheInterceptor != null) options.AddInterceptors(efCoreCacheInterceptor);
            options.AppendOptions<TContext>(sp);
        });

        return services;
    }
    public static IServiceCollection RegisterDbContext<TContext>(this IServiceCollection services)
                                      where TContext : DbContextBase
    {
        services.AddDbContext<TContext>((sp, options) =>
        {
            options.AppendOptions<TContext>(sp);
        });

        return services;
    }
    public static IServiceCollection RegisterDbContext<TContext, TCacheInterceptor>(this IServiceCollection services)
                                                      where TContext : DbContextBase
                                                      where TCacheInterceptor : DbCommandInterceptor
    {
        services.AddDbContext<TContext>((sp, options) =>
        {
            var efCoreCacheInterceptor = sp.GetService<TCacheInterceptor>();
            if (efCoreCacheInterceptor != null) options.AddInterceptors(efCoreCacheInterceptor);
            options.AppendOptions<TContext>(sp);
        });

        return services;
    }
    public static IServiceCollection RegisterSqlConnectionFactory<ISqlConnection, TSqlConnection>(this IServiceCollection services)
                                                                   where TSqlConnection : class
                                                                   where ISqlConnection : class
    {
        services.AddScoped(sp =>
        {
            var context = sp.GetService<IHttpContextAccessor>();
            IConfiguration? configuration = sp.GetService<IConfiguration>();
            string connectionString = configuration.GetDynamicConnectionString(context?.HttpContext?.Request.Headers);
            if (!connectionString.Contains(CommandTimeOut, StringComparison.InvariantCultureIgnoreCase))
            {
                int commandTimeOut = Convert.ToInt32(configuration.GetAppSettingValue(AppSettingKeys.CommandTimeOut, defaultValue: DefaultCommandTimeOutValue));
                connectionString += $";{CommandTimeOut} = {commandTimeOut}";
            }
            return (ISqlConnection)(Activator.CreateInstance(typeof(TSqlConnection), connectionString) ?? new());
        });

        return services;
    }
    static DbContextOptionsBuilder AppendOptions<TContext>(this DbContextOptionsBuilder options,
                                        IServiceProvider sp)
                                        where TContext : DbContextBase
    {
        var configuration = sp.GetService<IConfiguration>();
        bool useLazyLoading = Convert.ToBoolean(configuration.GetAppSettingValue(AppSettingKeys.UseLazyLoading, defaultValue: "false"));
        bool useChangeTrackingProxies = Convert.ToBoolean(configuration.GetAppSettingValue(AppSettingKeys.UseChangeTrackingProxies, defaultValue: "false"));
        options.UseLazyLoadingProxies(useLazyLoading);
        options.UseChangeTrackingProxies(useChangeTrackingProxies);
        bool useQueryTrackingBehavior = Convert.ToBoolean(configuration.GetAppSettingValue(AppSettingKeys.UseQueryTrackingBehavior, defaultValue: "true"));
        if (!useQueryTrackingBehavior) options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

        var context = sp.GetService<IHttpContextAccessor>();
        options.UseDbProvider<TContext>(context, configuration);

        return options;
    }

    static DbContextOptionsBuilder UseDbProvider<TContext>(this DbContextOptionsBuilder dbContextOptBuilder,
                                                            IHttpContextAccessor? context,
                                                            IConfiguration? configuration)
                                                            where TContext : DbContextBase
    {
        string connectionString = configuration.GetDynamicConnectionString(context?.HttpContext?.Request.Headers);

        string provider = MSSQL;
        if (connectionString.Contains(Server) || connectionString.Contains(Constants.Host)) provider = Postgresql;

        bool enableMigration = Convert.ToBoolean(configuration.GetAppSettingValue(AppSettingKeys.EnableMigration, defaultValue: "false"));
        string? migrationsAssembly = null;
        if (enableMigration)
        {
            Assembly assembly = typeof(TContext).GetTypeInfo().Assembly;
            migrationsAssembly = assembly == null || assembly.GetName() == null ? "" : assembly.GetName().Name;
        }

        int commandTimeOut = Convert.ToInt32(configuration.GetAppSettingValue(AppSettingKeys.CommandTimeOut, defaultValue: DefaultCommandTimeOutValue));
        int maxRetryCount = Convert.ToInt32(configuration.GetAppSettingValue(AppSettingKeys.SqlMaxRetryOnFailureCount, defaultValue: "0"));
        switch (provider)
        {
            case Postgresql:
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
