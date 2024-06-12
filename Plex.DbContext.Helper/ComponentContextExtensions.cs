using System.Reflection;
using Autofac;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Plex.DbContext.Helper;
public static class ComponentContextExtensions
{
    public static TContext RegisterDbContext<TContext>(this IComponentContext c,
              WebApplicationBuilder builder)
              where TContext : Microsoft.EntityFrameworkCore.DbContext
    {
        bool enableMigration = Convert.ToBoolean(builder.Configuration["EnableMigration"] ?? "false");
        var optBuilder = GetDbContextOptions<TContext>(c, builder, enableMigration);
        return (TContext)(Activator.CreateInstance(typeof(TContext), optBuilder.Options, enableMigration) ?? new());
    }

    public static TContext RegisterDbContextWithEfCoreCache<TContext, TCacheInterceptor>(
                  this IComponentContext c,
                  WebApplicationBuilder builder)
                  where TContext : Microsoft.EntityFrameworkCore.DbContext
                  where TCacheInterceptor : DbCommandInterceptor
    {
        bool enableMigration = Convert.ToBoolean(builder.Configuration["EnableMigration"] ?? "false");

        var optBuilder = GetDbContextOptions<TContext>(c, builder, enableMigration);
        if (c.IsRegistered(typeof(TCacheInterceptor)))
        {
            var efCoreCacheInterceptor = c.Resolve<TCacheInterceptor>();
            optBuilder.AddInterceptors(efCoreCacheInterceptor);
        }

        return (TContext)(Activator.CreateInstance(typeof(TContext), optBuilder.Options, enableMigration) ?? new());
    }
    static DbContextOptionsBuilder<TContext> GetDbContextOptions<TContext>(IComponentContext c,
                                                                        WebApplicationBuilder builder,
                                                                        bool enableMigration)
                                                                        where TContext : Microsoft.EntityFrameworkCore.DbContext
    {
        bool useLazyLoading = Convert.ToBoolean(builder.Configuration["UseLazyLoading"] ?? "false");
        bool useChangeTrackingProxies = Convert.ToBoolean(builder.Configuration["UseChangeTrackingProxies"] ?? "false");

        var dbContextOptBuilder = new DbContextOptionsBuilder<TContext>();
        dbContextOptBuilder.UseLazyLoadingProxies(useLazyLoading);
        dbContextOptBuilder.UseChangeTrackingProxies(useChangeTrackingProxies);

        var context = c.Resolve<IHttpContextAccessor>();
        string connectionString = builder.Configuration.GetDynamicConnectionString(context?.HttpContext?.Request.Headers);

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
