using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace TushonkInfo;

public record TraderBarterScheme(int LoyaltyLevel, List<List<BarterScheme>> Barters);

public record TraderBarterInfo(
    MongoId AssortId,
    string AssortParentId,
    MongoId TemplateId,
    MongoId TraderId,
    TraderBarterScheme? BarterScheme
    );