using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums.Hideout;

namespace TushonkInfo.Caches.Models;

public record struct StageRequirementSlim(
    MongoId AreaId,
    HideoutAreas AreaType,
    string Stage,
    MongoId TemplateId,
    int Count
);
