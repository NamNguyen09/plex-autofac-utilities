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
		services.AddKeyedSingleton<PlexDbOptions>("dbcontextpool");
		services.AddDbContextPool<TContext>((sp, options) =>
		{
			var plexDbOptions = sp.GetRequiredKeyedService<PlexDbOptions>("dbcontextpool");
			options.AppendOptions<TContext>(plexDbOptions);
		});

		return services;
	}
	public static IServiceCollection RegisterDbContextPool<TContext, TCacheInterceptor>(this IServiceCollection services)
													where TContext : DbContextBase
													where TCacheInterceptor : DbCommandInterceptor
	{
		services.AddKeyedSingleton<PlexDbOptions>("dbcontextpoolwithcache");
		services.AddDbContextPool<TContext>((sp, options) =>
		{
			var efCoreCacheInterceptor = sp.GetService<TCacheInterceptor>();
			if (efCoreCacheInterceptor != null) options.AddInterceptors(efCoreCacheInterceptor);
			var plexDbOptions = sp.GetRequiredKeyedService<PlexDbOptions>("dbcontextpoolwithcache");
			options.AppendOptions<TContext>(plexDbOptions);
		});

		return services;
	}
	public static IServiceCollection RegisterDbContext<TContext>(this IServiceCollection services)
									  where TContext : DbContextBase
	{
		services.AddKeyedScoped<PlexDbOptions>("dbcontext");
		services.AddDbContext<TContext>((sp, options) =>
		{
			var plexDbOptions = sp.GetRequiredKeyedService<PlexDbOptions>("dbcontext");
			options.AppendOptions<TContext>(plexDbOptions);
		});

		return services;
	}
	public static IServiceCollection RegisterDbContext<TContext, TCacheInterceptor>(this IServiceCollection services)
													  where TContext : DbContextBase
													  where TCacheInterceptor : DbCommandInterceptor
	{
		services.AddKeyedScoped<PlexDbOptions>("dbcontextwithcache");
		services.AddDbContext<TContext>((sp, options) =>
		{
			var efCoreCacheInterceptor = sp.GetService<TCacheInterceptor>();
			if (efCoreCacheInterceptor != null) options.AddInterceptors(efCoreCacheInterceptor);
			var plexDbOptions = sp.GetRequiredKeyedService<PlexDbOptions>("dbcontextwithcache");
			options.AppendOptions<TContext>(plexDbOptions);
		});

		return services;
	}
	public static IServiceCollection RegisterSqlConnectionFactory<ISqlConnection, TSqlConnection>(this IServiceCollection services)
																   where TSqlConnection : class
																   where ISqlConnection : class
	{
		services.AddKeyedScoped<PlexDbOptions>("sqlconnectionfactory");
		services.AddScoped(sp =>
		{
			var plexDbOptions = sp.GetRequiredKeyedService<PlexDbOptions>("sqlconnectionfactory");
			string connectionString = plexDbOptions.ConnectionString;
			if (!connectionString.Contains(CommandTimeOut, StringComparison.InvariantCultureIgnoreCase))
			{
				connectionString += $";{CommandTimeOut} = {plexDbOptions.CommandTimeOut}";
			}
			if (!connectionString.Contains(TrustServerCertificate, StringComparison.InvariantCultureIgnoreCase))
			{
				connectionString += $";{TrustServerCertificate}=True";
			}

			return (ISqlConnection)(Activator.CreateInstance(typeof(TSqlConnection), connectionString) ?? new());
		});

		return services;
	}
	static DbContextOptionsBuilder AppendOptions<TContext>(this DbContextOptionsBuilder options,
														PlexDbOptions plexDbOptions)
														where TContext : DbContextBase
	{
		options.UseLazyLoadingProxies(plexDbOptions.UseLazyLoading);
		options.UseChangeTrackingProxies(plexDbOptions.UseChangeTrackingProxies);
		if (!plexDbOptions.UseQueryTrackingBehavior) options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

		options.UseDbProvider<TContext>(plexDbOptions);

		return options;
	}

	static DbContextOptionsBuilder UseDbProvider<TContext>(this DbContextOptionsBuilder dbContextOptBuilder,
															PlexDbOptions plexDbOptions)
															where TContext : DbContextBase
	{
		string provider = plexDbOptions.DbProvider;
		string? migrationsAssembly = null;
		if (plexDbOptions.EnableMigration)
		{
			Assembly assembly = typeof(TContext).GetTypeInfo().Assembly;
			migrationsAssembly = assembly == null || assembly.GetName() == null ? "" : assembly.GetName().Name;
		}

		string connectionString = plexDbOptions.ConnectionString;
		switch (provider)
		{
			case Postgresql:
				dbContextOptBuilder.UseNpgsql(connectionString, options =>
				   {
					   options.CommandTimeout(plexDbOptions.CommandTimeOut);
					   if (!string.IsNullOrWhiteSpace(migrationsAssembly)) options.MigrationsAssembly(migrationsAssembly);
					   if (plexDbOptions.MaxRetryCount > 0) options.EnableRetryOnFailure(plexDbOptions.MaxRetryCount);
				   });
				break;
			default:
				if (!connectionString.Contains(TrustServerCertificate, StringComparison.InvariantCultureIgnoreCase))
				{
					connectionString += $";{TrustServerCertificate}=True";
				}

				dbContextOptBuilder.UseSqlServer(connectionString, options =>
				{
					options.CommandTimeout(plexDbOptions.CommandTimeOut);
					if (!string.IsNullOrWhiteSpace(migrationsAssembly)) options.MigrationsAssembly(migrationsAssembly);
					if (plexDbOptions.MaxRetryCount > 0) options.EnableRetryOnFailure(plexDbOptions.MaxRetryCount);
				});
				break;
		}

		return dbContextOptBuilder;
	}
}
