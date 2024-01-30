using Autofac.Features.Metadata;

namespace Plex.DbContext.Helper;

public static class MetaDataExtensions
{
    public static Meta<T>? ResolvedMeta<T, TMeta>(this IEnumerable<Meta<T>> interfaces, object metaName)
    {
        return (from t in interfaces
                let metadata = t.Metadata[nameof(TMeta)]
                where metadata != null && metadata.Equals(metaName)
                select t).FirstOrDefault();
    }
}
