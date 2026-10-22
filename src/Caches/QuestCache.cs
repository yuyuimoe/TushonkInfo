using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using ItemInfo.Caches.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace ItemInfo.Caches;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PostDBModLoader + 65540)]
public sealed class QuestCache(ISptLogger<QuestCache> logger, DatabaseService db) : IOnLoad
{
    /// <summary>
    /// Dictionary of quests that require an item.
    /// Key = Item Template MongoID
    /// </summary>
    public FrozenDictionary<
        MongoId,
        ImmutableArray<QuestItemRequirementSlim>
    > TemplateQuestRequirements { get; private set; }

    /// <summary>
    /// Dictionary of quests that reward an item.
    /// Key = Item Template MongoID
    /// </summary>
    public FrozenDictionary<MongoId, ImmutableArray<QuestItemRewardSlim>> TemplateQuestRewards
    {
        get;
        private set;
    }

    /// <summary>
    /// Dictionary of quests that unlock a trader assort.
    /// Key = Item Template MongoID
    /// </summary>
    public FrozenDictionary<MongoId, ImmutableArray<QuestAssortUnlockSlim>> TemplateQuestUnlocks
    {
        get;
        private set;
    }

    public async Task OnLoad()
    {
        await Task.Run(() =>
        {
            var watch = new Stopwatch();
            watch.Start();

            var quests = db.GetQuests();

            TemplateQuestRequirements = BuildTemplateQuestRequirements(quests);
            TemplateQuestRewards = BuildTemplateQuestRewards(quests);
            TemplateQuestUnlocks = BuildTemplateQuestUnlocks(quests);

            watch.Stop();
            logger.Info($"Quest Cache loaded in {watch.ElapsedMilliseconds}ms");
        });
    }

    /// <summary>
    /// Requirements of a single quest. Not indexed on purpose - scans the
    /// requirement index, so use it for one quest at a time (UI panel), not per item.
    /// For the item-to-quests direction use <see cref="TemplateQuestRequirements"/>.
    /// </summary>
    public IEnumerable<QuestItemRequirementSlim> GetQuestRequirements(MongoId questId) =>
        TemplateQuestRequirements.Values.SelectMany(r => r).Where(r => r.QuestId == questId);

    /// <summary>
    /// Rewards of a single quest. Not indexed, see <see cref="GetQuestRequirements"/>.
    /// </summary>
    public IEnumerable<QuestItemRewardSlim> GetQuestRewards(MongoId questId) =>
        TemplateQuestRewards.Values.SelectMany(r => r).Where(r => r.QuestId == questId);

    /// <summary>
    /// Assorts unlocked by a single quest. Not indexed, see <see cref="GetQuestRequirements"/>.
    /// </summary>
    public IEnumerable<QuestAssortUnlockSlim> GetQuestUnlocks(MongoId questId) =>
        TemplateQuestUnlocks.Values.SelectMany(u => u).Where(u => u.QuestId == questId);

    /// <summary>
    /// Quests that unlock a specific assort of an item. Needs both ids because the
    /// index is keyed by template, so this stays an O(assort unlocks of the item) filter.
    /// </summary>
    public IEnumerable<QuestAssortUnlockSlim> GetAssortUnlocks(
        MongoId templateId,
        MongoId assortId
    ) =>
        TemplateQuestUnlocks.TryGetValue(templateId, out var unlocks)
            ? unlocks.Where(u => u.AssortId == assortId)
            : [];

    /// <summary>
    /// Trader that gives the quest. Single lookup on the database, nothing cached.
    /// </summary>
    public MongoId GetQuestTrader(MongoId questId) =>
        db.GetQuests().TryGetValue(questId, out var quest) ? quest.TraderId : MongoId.Empty();

    private static FrozenDictionary<
        MongoId,
        ImmutableArray<QuestItemRequirementSlim>
    > BuildTemplateQuestRequirements(Dictionary<MongoId, Quest> quests) =>
        quests
            .SelectMany(q => ExpandRequirements(q.Value, q.Key))
            .GroupBy(r => r.TemplateId)
            .ToFrozenDictionary(g => g.Key, g => g.ToImmutableArray());

    private static FrozenDictionary<
        MongoId,
        ImmutableArray<QuestItemRewardSlim>
    > BuildTemplateQuestRewards(Dictionary<MongoId, Quest> quests) =>
        quests
            .SelectMany(q => ExpandRewards(q.Value, q.Key))
            .GroupBy(r => r.TemplateId)
            .ToFrozenDictionary(g => g.Key, g => g.ToImmutableArray());

    private static FrozenDictionary<
        MongoId,
        ImmutableArray<QuestAssortUnlockSlim>
    > BuildTemplateQuestUnlocks(Dictionary<MongoId, Quest> quests) =>
        quests
            .SelectMany(q => ExpandAssortUnlocks(q.Value, q.Key))
            .GroupBy(u => u.TemplateId)
            .ToFrozenDictionary(g => g.Key, g => g.ToImmutableArray());

    /// <summary>
    /// Item conditions: HandoverItem, FindItem and LeaveItemAtLocation.
    /// </summary>
    private static IEnumerable<QuestItemRequirementSlim> ExpandRequirements(
        Quest quest,
        MongoId questId
    )
    {
        var conditions =
            quest.Conditions?.AvailableForFinish?.Where(qc =>
                qc is { Target: not null } && GetKind(qc.ConditionType) is not null
            )
            ?? [];

        foreach (var condition in conditions)
        {
            var kind = GetKind(condition.ConditionType)!.Value;
            var count = (int)(condition.Value ?? 1d);
            var foundInRaid = condition.OnlyFoundInRaid ?? false;

            if (condition.Target!.Item is { } single)
            {
                yield return new QuestItemRequirementSlim(
                    questId,
                    AsMongoId(single),
                    kind,
                    count,
                    foundInRaid
                );

                continue;
            }

            foreach (var template in condition.Target.List ?? [])
            {
                yield return new QuestItemRequirementSlim(
                    questId,
                    AsMongoId(template),
                    kind,
                    count,
                    foundInRaid
                );
            }
        }
    }

    /// <summary>
    /// Item rewards. The item matching reward.Target is the root of the reward,
    /// the remaining entries are its attachments.
    /// </summary>
    private static IEnumerable<QuestItemRewardSlim> ExpandRewards(Quest quest, MongoId questId)
    {
        foreach (var (bucket, reward) in GetRewards(quest))
        {
            if (reward is not { Type: RewardType.Item, Items: { Count: > 0 } items })
            {
                continue;
            }

            yield return new QuestItemRewardSlim(
                questId,
                GetRootItem(reward, items).Template,
                (int)(reward.Value ?? 1d),
                reward.FindInRaid ?? false,
                string.Equals(bucket, "Started", StringComparison.OrdinalIgnoreCase)
            );
        }
    }

    /// <summary>
    /// AssortmentUnlock rewards: reward.Target is the assort id of the unlocked offer,
    /// reward.TraderId is the trader that now sells it.
    /// </summary>
    private static IEnumerable<QuestAssortUnlockSlim> ExpandAssortUnlocks(
        Quest quest,
        MongoId questId
    )
    {
        foreach (var (_, reward) in GetRewards(quest))
        {
            if (reward is not { Type: RewardType.AssortmentUnlock, Items: { Count: > 0 } items })
            {
                continue;
            }

            yield return new QuestAssortUnlockSlim(
                questId,
                AsMongoId(reward.TraderId),
                AsMongoId(reward.Target),
                GetRootItem(reward, items).Template,
                reward.LoyaltyLevel ?? 0
            );
        }
    }

    private static QuestItemRequirementKind? GetKind(string? conditionType) =>
        conditionType switch
        {
            "HandoverItem" => QuestItemRequirementKind.Handover,
            "FindItem" => QuestItemRequirementKind.Find,
            "LeaveItemAtLocation" => QuestItemRequirementKind.Place,
            _ => null,
        };

    private static IEnumerable<(string Bucket, Reward Reward)> GetRewards(Quest quest)
    {
        if (quest.Rewards is null)
        {
            yield break;
        }

        foreach (var (bucket, rewards) in quest.Rewards)
        {
            foreach (var reward in rewards)
            {
                yield return (bucket, reward);
            }
        }
    }

    private static Item GetRootItem(Reward reward, List<Item> items) =>
        reward.Target is null
            ? items[0]
            : items.FirstOrDefault(i => i.Id == reward.Target) ?? items[0];

    private static MongoId AsMongoId(object? value) =>
        value switch
        {
            MongoId id => id,
            string mongoIdString when MongoId.IsValidMongoId(mongoIdString) => new MongoId(
                mongoIdString
            ),
            _ => MongoId.Empty(),
        };
}
