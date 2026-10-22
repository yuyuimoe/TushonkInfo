using System.Collections.Frozen;
using TushonkInfo.Caches.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;

namespace TushonkInfo.Utils;

[Injectable]
public class TraderUtils
{
    public FrozenDictionary<MongoId, MongoId> GetAssortParentId(
        IReadOnlyDictionary<MongoId, AssortSlim> assortsById
    )
    {
        var rootCache = new Dictionary<MongoId, MongoId>();
        var assortVisited = new HashSet<MongoId>();

        foreach (var tpl in assortsById.Keys)
        {
            FindRoot(tpl);
        }

        return rootCache.ToFrozenDictionary();

        MongoId FindRoot(MongoId tpl)
        {
            if (rootCache.TryGetValue(tpl, out var cachedRoot))
            {
                return cachedRoot;
            }

            if (
                !assortsById.TryGetValue(tpl, out var assort)
                || assort.ParentId == "hideout"
                || string.IsNullOrEmpty(assort.ParentId)
                || !assortVisited.Add(tpl)
            )
            {
                return tpl;
            }

            var root = FindRoot(assort.ParentId);
            rootCache[tpl] = root;
            return root;
        }
    }
}
