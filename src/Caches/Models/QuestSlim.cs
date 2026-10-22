using System.Collections.Immutable;
using SPTarkov.Server.Core.Models.Common;

namespace ItemInfo.Caches.Models;

/// <summary>
/// How the item is used to complete a quest.
/// </summary>
public enum QuestItemRequirementKind : byte
{
    /// <summary>HandoverItem - the item is consumed when turned in.</summary>
    Handover,

    /// <summary>FindItem - the item is only shown in the inventory.</summary>
    Find,

    /// <summary>LeaveItemAtLocation - the item is planted in a zone.</summary>
    Place,
}

/// <summary>
/// Item that a quest requires. The quest giver is resolved with
/// <see cref="ItemInfo.Caches.QuestCache.GetQuestTrader"/>.
/// </summary>
public record struct QuestItemRequirementSlim(
    MongoId QuestId,
    MongoId TemplateId,
    QuestItemRequirementKind Kind,
    int Count,
    bool FoundInRaid
);

/// <summary>
/// Item that a quest gives as a reward. Count is the root item quantity,
/// attachments of a preset are not listed.
/// </summary>
public record struct QuestItemRewardSlim(
    MongoId QuestId,
    MongoId TemplateId,
    int Count,
    bool FoundInRaid,
    bool OnAccept
);

/// <summary>
/// Trader assort (offer) that a quest unlocks.
/// </summary>
public record struct QuestAssortUnlockSlim(
    MongoId QuestId,
    MongoId TraderId,
    MongoId AssortId,
    MongoId TemplateId,
    int LoyaltyLevel
);

/// <summary>
/// Conditions needed to unlock a quest (its AvailableForStart block).
/// Zero/Empty means the requirement is not present.
/// </summary>
public record struct QuestUnlockRequirementSlim(
    int PlayerLevel,
    MongoId TraderId,
    int TraderLoyalty,
    ImmutableArray<MongoId> RequiredQuests
);
