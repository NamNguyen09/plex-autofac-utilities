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
        bool enableMigration = Convert.ToBoolean(builder.Configuration.GetAppSettingValue("EnableMigration", defaultValue: "false"));
        var optBuilder = GetDbContextOptions<TDbContext>(c, builder, enableMigration);
        return (TDbContext)(Activator.CreateInstance(typeof(TDbContext), optBuilder.Options, enableMigration) ?? new());
    }

    public static TDbContext RegisterDbContextWithEfCoreCache<TDbContext, TCacheInterceptor>(
                  this IComponentContext c,
                  WebApplicationBuilder builder)
                  where TDbContext : Microsoft.EntityFrameworkCore.DbContext
                  where TCacheInterceptor : DbCommandInterceptor
    {
        bool enableMigration = Convert.ToBoolean(builder.Configuration.GetAppSettingValue("EnableMigration", defaultValue: "false"));

        var optBuilder = GetDbContextOptions<TDbContext>(c, builder, enableMigration);
        if (c.IsRegistered(typeof(TCacheInterceptor)))
        {
            var efCoreCacheInterceptor = c.Resolve<TCacheInterceptor>();
            optBuilder.AddInterceptors(efCoreCacheInterceptor);
        }

        return (TDbContext)(Activator.CreateInstance(typeof(TDbContext), optBuilder.Options, enableMigration) ?? new());
    }
    static DbContextOptionsBuilder<TDbContext> GetDbContextOptions<TDbContext>(IComponentContext c,
                                                                        WebApplicationBuilder builder,
                                                                        bool enableMigration)
                                                                        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        bool useLazyLoading = Convert.ToBoolean(builder.Configuration.GetAppSettingValue("UseLazyLoading", defaultValue: "false"));
        bool useChangeTrackingProxies = Convert.ToBoolean(builder.Configuration.GetAppSettingValue("UseChangeTrackingProxies", defaultValue: "false"));

        var dbContextOptBuilder = new DbContextOptionsBuilder<TDbContext>();
        dbContextOptBuilder.UseLazyLoadingProxies(useLazyLoading);
        dbContextOptBuilder.UseChangeTrackingProxies(useChangeTrackingProxies);

        var context = c.Resolve<IHttpContextAccessor>();
        string connectionString = builder.Configuration.GetDynamicConnectionString(context?.HttpContext?.Request.Headers);

        string provider = "mssql";
        if (connectionString.Contains("server") || connectionString.Contains("host")) provider = "postgresql";

        string? migrationsAssembly = null;
        if (enableMigration)
        {
            Assembly assembly = typeof(TDbContext).GetTypeInfo().Assembly;
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
