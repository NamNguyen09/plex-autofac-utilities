using System.Reflection;
using Autofac;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static Plex.Extensions.DbContext.Constants;
using DbContextBase = Microsoft.EntityFrameworkCore.DbContext;

namespace Plex.Extensions.DbContext;
public static class ComponentContextExtensions
{
    public static TContext RegisterDbContext<TContext>(this IComponentContext c)
                                                       where TContext : DbContextBase
    {
        var optBuilder = GetDbContextOptions<TContext>(c);
        return (TContext)(Activator.CreateInstance(typeof(TContext), optBuilder.Options) ?? new());
    }

    public static TContext RegisterDbContext<TContext, TCacheInterceptor>(this IComponentContext c)
                                                              where TContext : DbContextBase
                                                              where TCacheInterceptor : DbCommandInterceptor
    {
        var optBuilder = GetDbContextOptions<TContext>(c);
        if (c.IsRegistered(typeof(TCacheInterceptor)))
        {
            var efCoreCacheInterceptor = c.Resolve<TCacheInterceptor>();
            optBuilder.AddInterceptors(efCoreCacheInterceptor);
        }

        return (TContext)(Activator.CreateInstance(typeof(TContext), optBuilder.Options) ?? new());
    }
    public static ContainerBuilder RegisterSqlConnectionFactory<ISqlConnection, TSqlConnection>(
                                                                    this ContainerBuilder containerBuilder)
                                                                    where TSqlConnection : class
                                                                    where ISqlConnection : class
    {
        containerBuilder.Register(c =>
        {
            var context = c.Resolve<IHttpContextAccessor>();
            IConfiguration configuration = c.Resolve<IConfiguration>();
            string connectionString = configuration.GetDynamicConnectionString(context?.HttpContext?.Request.Headers);
            int commandTimeOut = Convert.ToInt32(configuration.GetAppSettingValue(AppSettingKeys.CommandTimeOut, defaultValue: DefaultCommandTimeOutValue));
            if (!connectionString.Contains(CommandTimeOut, StringComparison.InvariantCultureIgnoreCase))
            {
                connectionString += $";{CommandTimeOut} = {commandTimeOut}";
            }
            return (TSqlConnection)(Activator.CreateInstance(typeof(TSqlConnection), connectionString) ?? new());
        }).As<ISqlConnection>().InstancePerLifetimeScope();

        return containerBuilder;
    }
    static DbContextOptionsBuilder<TContext> GetDbContextOptions<TContext>(IComponentContext c)
                                               where TContext : DbContextBase
    {
        IConfiguration configuration = c.Resolve<IConfiguration>();
        bool useLazyLoading = Convert.ToBoolean(configuration.GetAppSettingValue(AppSettingKeys.UseLazyLoading, defaultValue: "false"));
        bool useChangeTrackingProxies = Convert.ToBoolean(configuration.GetAppSettingValue(AppSettingKeys.UseChangeTrackingProxies, defaultValue: "false"));

        var dbContextOptBuilder = new DbContextOptionsBuilder<TContext>();
        dbContextOptBuilder.UseLazyLoadingProxies(useLazyLoading);
        dbContextOptBuilder.UseChangeTrackingProxies(useChangeTrackingProxies);

        var context = c.Resolve<IHttpContextAccessor>();
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
