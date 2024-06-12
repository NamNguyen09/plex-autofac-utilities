﻿using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Plex.DbContext.Helper;
public static class ServiceCollectionExtension
{
    public static WebApplicationBuilder RegisterDbContext<TContext>(this WebApplicationBuilder builder)
                                        where TContext : Microsoft.EntityFrameworkCore.DbContext
    {
        builder.Services.AddDbContextPool<TContext>((sp, options) =>
        {
            bool useLazyLoading = Convert.ToBoolean(builder.Configuration["UseLazyLoading"] ?? "false");
            bool useChangeTrackingProxies = Convert.ToBoolean(builder.Configuration["UseChangeTrackingProxies"] ?? "false");
            options.UseLazyLoadingProxies(useLazyLoading);
            options.UseChangeTrackingProxies(useChangeTrackingProxies);
            options.UseDbProvider<TContext>(sp, builder);
        });

        return builder;
    }
    public static WebApplicationBuilder RegisterDbContextWithCache<TContext, TCacheInterceptor>(
                                                      this WebApplicationBuilder builder)
                                                      where TContext : Microsoft.EntityFrameworkCore.DbContext
                                                      where TCacheInterceptor : DbCommandInterceptor
    {
        builder.Services.AddDbContextPool<TContext>((sp, options) =>
        {
            var efCoreCacheInterceptor = sp.GetService<TCacheInterceptor>();
            if (efCoreCacheInterceptor != null) options.AddInterceptors(efCoreCacheInterceptor);
            bool useLazyLoading = Convert.ToBoolean(builder.Configuration["UseLazyLoading"] ?? "false");
            bool useChangeTrackingProxies = Convert.ToBoolean(builder.Configuration["UseChangeTrackingProxies"] ?? "false");
            options.UseLazyLoadingProxies(useLazyLoading);
            options.UseChangeTrackingProxies(useChangeTrackingProxies);
            options.UseDbProvider<TContext>(sp, builder);
        });

        return builder;
    }
    static DbContextOptionsBuilder UseDbProvider<TContext>(this DbContextOptionsBuilder options,
        IServiceProvider sp, WebApplicationBuilder builder) where TContext : Microsoft.EntityFrameworkCore.DbContext
    {
        var context = sp.GetService<IHttpContextAccessor>();
        string connectionString = builder.Configuration.GetDynamicConnectionString(context?.HttpContext?.Request.Headers);

        string provider = "mssql";
        if (connectionString.Contains("server") || connectionString.Contains("host")) provider = "postgresql";

        bool enableMigration = Convert.ToBoolean(builder.Configuration["EnableMigration"] ?? "false");
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
                    options.UseNpgsql(connectionString);
                    break;
                }

                options.UseNpgsql(connectionString, p => p.MigrationsAssembly(migrationsAssembly));
                break;
            default:
                if (!enableMigration)
                {
                    options.UseSqlServer(connectionString);
                    break;
                }

                options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly));
                break;
        }

        return options;
    }
}