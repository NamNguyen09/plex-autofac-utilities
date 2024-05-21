using System.Reflection;
using Autofac;

namespace Plex.Autofac.Helper;
public static class ContainerBuilderExtensions
{
    public static ContainerBuilder RegisterFromAssemblyTypesWithMetaData<IService>(this ContainerBuilder containerBuilder,
                                                                                    Assembly[] assemblies,
                                                                                    string metaName)
                                                                                    where IService : class
    {
        var types = assemblies.SelectMany(s => s.GetTypes()).Where(t => typeof(IService).IsAssignableFrom(t)
                                                                        && !t.IsInterface).ToArray();
        foreach (Type? item in types)
        {
            if (item == null) continue;
            object? name = (from t in item.CustomAttributes
                            where t.AttributeType.Name == metaName
                                  && t.ConstructorArguments != null
                                  && t.ConstructorArguments.First().Value != null
                            select t.ConstructorArguments.First().Value)
                            .FirstOrDefault();

            string? functionName = name?.ToString()?.ToLower() ?? "";
            if (string.IsNullOrWhiteSpace(functionName)) continue;
            containerBuilder.RegisterType(item).As<IService>()
                            .WithMetadata(metaName, functionName)
                            .InstancePerLifetimeScope();
        }

        return containerBuilder;
    }
    public static ContainerBuilder RegisterFromAssemblyTypes<IService>(this ContainerBuilder containerBuilder,
                                                               Assembly[] assemblies)
                                                               where IService : class
    {
        var types = assemblies.SelectMany(s => s.GetTypes()).Where(t => typeof(IService).IsAssignableFrom(t)
                                                                        && !t.IsInterface).ToArray();
        foreach (Type? item in types)
        {
            if (item == null) continue;
            containerBuilder.RegisterType(item).As<IService>().InstancePerLifetimeScope();
        }

        return containerBuilder;
    }
}
