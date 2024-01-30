using Autofac;

namespace Plex.Autofac.Helper;

public static class ContainerBuilderExtensions
{
    public static ContainerBuilder RegisterAssemblyWithMetaData<TMetaData, TIService>(this ContainerBuilder containerBuilder, string metaName)
                                                                where TMetaData : class
                                                                where TIService : class
    {
        var metaDatas = typeof(TMetaData).Assembly.GetTypes().Where(t => typeof(TIService).IsAssignableFrom(t) && !t.IsInterface).ToList();
        foreach (var item in metaDatas)
        {
            object? name = (from t in item.CustomAttributes.Where(t => t.ConstructorArguments != null
                                                               && t.ConstructorArguments.First().Value != null)
                            select t.ConstructorArguments.First().Value).FirstOrDefault();
            if (name == null) continue;
            containerBuilder.RegisterType(item).As<TIService>().WithMetadata(metaName, name);
        }

        return containerBuilder;
    }
}
