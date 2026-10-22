using System.Collections.Frozen;
using SPTarkov.Server.Core.Models.Common;

namespace TushonkInfo.Caches.Models;

public record struct ItemBuyDataSlim(FrozenSet<MongoId> Category, FrozenSet<MongoId> IdList);
