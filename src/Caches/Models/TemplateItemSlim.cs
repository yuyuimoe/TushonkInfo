using System.Collections.Immutable;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace TushonkInfo.Caches.Models;

public record struct TemplateItemSlim(
    MongoId TemplateId,
    int Width,
    int Height,
    int StackMaxSize,
    ImmutableList<Grid> Grids
);
