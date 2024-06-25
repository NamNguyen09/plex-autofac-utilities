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
		PlexDbOptions plexDbOptions = c.ResolveKeyed<PlexDbOptions>("dbcontext");
		var optBuilder = GetDbContextOptions<TContext>(c, plexDbOptions);
		return (TContext)(Activator.CreateInstance(typeof(TContext), optBuilder.Options) ?? new());
	}

	public static TContext RegisterDbContext<TContext, TCacheInterceptor>(this IComponentContext c)
															  where TContext : DbContextBase
															  where TCacheInterceptor : DbCommandInterceptor
	{
		PlexDbOptions plexDbOptions = c.ResolveKeyed<PlexDbOptions>("dbcontextwithcache");
		var optBuilder = GetDbContextOptions<TContext>(c, plexDbOptions);
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
		containerBuilder.RegisterType<PlexDbOptions>()
						.Keyed<PlexDbOptions>("sqlconnectionfactory")
						.AsSelf().InstancePerLifetimeScope();
		containerBuilder.Register(c =>
		{
			PlexDbOptions plexDbOptions = c.ResolveKeyed<PlexDbOptions>("sqlconnectionfactory");
			string connectionString = plexDbOptions.ConnectionString;
			if (!connectionString.Contains(CommandTimeOut, StringComparison.InvariantCultureIgnoreCase))
			{
				connectionString += $";{CommandTimeOut} = {plexDbOptions.CommandTimeOut}";
			}
			return (TSqlConnection)(Activator.CreateInstance(typeof(TSqlConnection), connectionString) ?? new());
		}).As<ISqlConnection>().InstancePerLifetimeScope();

		return containerBuilder;
	}
	static DbContextOptionsBuilder<TContext> GetDbContextOptions<TContext>(IComponentContext c, PlexDbOptions plexDbOptions)
											   where TContext : DbContextBase
	{
		var dbContextOptBuilder = new DbContextOptionsBuilder<TContext>();
		dbContextOptBuilder.UseLazyLoadingProxies(plexDbOptions.UseLazyLoading);
		dbContextOptBuilder.UseChangeTrackingProxies(plexDbOptions.UseChangeTrackingProxies);

		string? migrationsAssembly = null;
		if (plexDbOptions.EnableMigration)
		{
			Assembly assembly = typeof(TContext).GetTypeInfo().Assembly;
			migrationsAssembly = assembly == null || assembly.GetName() == null ? "" : assembly.GetName().Name;
		}

		string connectionString = plexDbOptions.ConnectionString;
		switch (plexDbOptions.DbProvider)
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
