using SPTarkov.Server.Core.Models.Common;

namespace TushonkInfo;

public record TraderPriceInfo(double Price, MongoId TraderId, string ItemParentId);