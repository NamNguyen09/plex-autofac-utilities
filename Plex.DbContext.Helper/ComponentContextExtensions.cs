using System.Reflection;
using Autofac;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Plex.DbContext.Helper;
public static class ComponentContextExtensions
{
    public static TDbContext RegisterDbContext<TDbContext>(this IComponentContext c,
                                                          WebApplicationBuilder builder)
                                                          where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        var optBuilder = GetDbContextOptions<TDbContext>(c, builder);
        return (TDbContext)(Activator.CreateInstance(typeof(TDbContext), optBuilder.Options) ?? new());
    }

    public static TDbContext RegisterDbContextWithCache<TDbContext, TCacheInterceptor>(
                                                              this IComponentContext c,
                                                              WebApplicationBuilder builder)
                                                              where TDbContext : Microsoft.EntityFrameworkCore.DbContext
                                                              where TCacheInterceptor : DbCommandInterceptor
    {
        var optBuilder = GetDbContextOptions<TDbContext>(c, builder);
        if (c.IsRegistered(typeof(TCacheInterceptor)))
        {
            var efCoreCacheInterceptor = c.Resolve<TCacheInterceptor>();
            optBuilder.AddInterceptors(efCoreCacheInterceptor);
        }

        return (TDbContext)(Activator.CreateInstance(typeof(TDbContext), optBuilder.Options) ?? new());
    }
    public static ContainerBuilder RegisterSqlConnectionFactory<ISqlConnection, TSqlConnection>(this ContainerBuilder containerBuilder,
                                                                                IConfigurationManager configuration)
                                                                                where TSqlConnection : class
                                                                                where ISqlConnection : class
    {
        containerBuilder.Register(c =>
        {
            var context = c.Resolve<IHttpContextAccessor>();
            string connectionString = configuration.GetDynamicConnectionString(context?.HttpContext?.Request.Headers);
            int commandTimeOut = Convert.ToInt32(configuration.GetAppSettingValue("EfSqlCommandTimeOutInSecond", defaultValue: "300"));
            if (!connectionString.Contains("Command Timeout", StringComparison.InvariantCultureIgnoreCase))
            {
                connectionString += $";Command Timeout = {commandTimeOut}";
            }
            return (TSqlConnection)(Activator.CreateInstance(typeof(TSqlConnection), connectionString) ?? new());
        }).As<ISqlConnection>().InstancePerLifetimeScope();

        return containerBuilder;
    }
    static DbContextOptionsBuilder<TDbContext> GetDbContextOptions<TDbContext>(IComponentContext c,
                                                                        WebApplicationBuilder builder)
                                                                        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        bool useLazyLoading = Convert.ToBoolean(builder.Configuration.GetAppSettingValue("EfUseLazyLoading", defaultValue: "false"));
        bool useChangeTrackingProxies = Convert.ToBoolean(builder.Configuration.GetAppSettingValue("EfUseChangeTrackingProxies", defaultValue: "false"));

        var dbContextOptBuilder = new DbContextOptionsBuilder<TDbContext>();
        dbContextOptBuilder.UseLazyLoadingProxies(useLazyLoading);
        dbContextOptBuilder.UseChangeTrackingProxies(useChangeTrackingProxies);
        var context = c.Resolve<IHttpContextAccessor>();
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
