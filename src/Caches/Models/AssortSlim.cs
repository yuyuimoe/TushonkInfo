using SPTarkov.Server.Core.Models.Common;

namespace TushonkInfo.Caches.Models;

public record struct AssortSlim(MongoId Id, MongoId TemplateId, string ParentId, int LoyaltyLevel);
