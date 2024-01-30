using Autofac.Features.Metadata;

namespace Plex.Autofac.Helper;

public static class MetaDataExtensions
{
    public static Meta<T>? ResolvedMeta<T>(this IEnumerable<Meta<T>> interfaces, string metaName, string servieName)
    {
        return (from t in interfaces
                let metadata = t.Metadata[metaName]
                where metadata != null && metadata.ToString() == servieName
                select t).FirstOrDefault();
    }
}