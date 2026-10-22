using SPTarkov.Server.Core.Models.Common;

namespace TushonkInfo.Caches.Models;

public record struct StageImprovementRequirementSlim(
    MongoId AreaId,
    MongoId StageId,
    MongoId TemplateId,
    int Count,
    bool FoundInRaid
);
