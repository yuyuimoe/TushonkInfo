using System.Collections.Immutable;
using ItemInfo.Caches;
using ItemInfo.Constants;
using ItemInfo.Models;
using ItemInfo.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;

namespace ItemInfo.Utils;

[Injectable]
public class QuestUtils(
    QuestCache questCache,
    LocaleCache localeCache,
    LocalizationService localizationService
)
{
    private string Locale =>
        localeCache.QuestNames.ContainsKey(ConfigurationService.Config.UserLocale)
            ? ConfigurationService.Config.UserLocale
            : "en";

    private string GetQuestName(MongoId questId) =>
        localeCache.QuestNames.TryGetValue(Locale, out var names)
        && names.TryGetValue(questId, out var name)
            ? name
            : questId.ToString() ?? string.Empty;

    private string GetTraderName(MongoId traderId) =>
        localeCache.TraderNicknames.TryGetValue(Locale, out var names)
        && names.TryGetValue(traderId, out var name)
            ? name
            : traderId.ToString() ?? string.Empty;

    /// <summary>
    /// Where the quest came from (its prerequisite quests) and which quests it
    /// unlocks, with the level, trader loyalty and quests required for each of
    /// those follow-ups.
    /// </summary>
    public List<string> GetLocalizedQuestLinks(MongoId questId)
    {
        var config = ConfigurationService.Config.ModQuestLinks;
        var lines = new List<string>();

        if (!config.Enabled)
        {
            return lines;
        }

        foreach (var unlockedId in questCache.GetUnlockedQuests(questId))
        {
            var requirement = questCache.QuestUnlockRequirements.GetValueOrDefault(unlockedId);
            var parts = new List<string>();

            if (requirement.PlayerLevel > 0)
            {
                parts.Add(
                    Format(
                        LocalizationTemplates.QuestLevelRequirement,
                        ("mark", WithTrailingSpace(config.Level)),
                        ("level", requirement.PlayerLevel)
                    )
                );
            }

            if (requirement.TraderLoyalty > 0)
            {
                parts.Add(
                    Format(
                        LocalizationTemplates.QuestTraderRequirement,
                        ("traderName", GetTraderName(requirement.TraderId)),
                        ("mark", WithTrailingSpace(config.Loyalty)),
                        ("level", requirement.TraderLoyalty)
                    )
                );
            }

            if (requirement.RequiredQuests.Length > 0)
            {
                parts.Add(
                    Format(
                        LocalizationTemplates.QuestRequiredQuests,
                        ("mark", WithTrailingSpace(config.Required)),
                        (
                            "quests",
                            string.Join(", ", requirement.RequiredQuests.Select(GetQuestName))
                        )
                    )
                );
            }

            var line = new FormatString(
                parts.Count > 0
                    ? LocalizationTemplates.QuestUnlockLine
                    : LocalizationTemplates.QuestUnlockLineNoRequirements,
                localizationService
            );
            line.AddData("mark", WithTrailingSpace(config.Unlocked));
            line.AddData("questName", GetQuestName(unlockedId));
            line.AddData("requirements", string.Join(", ", parts));
            lines.Add(line.ToString());
        }

        var prerequisites = questCache.GetQuestPrerequisites(questId);
        var shown = 0;

        foreach (var previousId in prerequisites)
        {
            if (config.MaxRequiredQuests > 0 && shown >= config.MaxRequiredQuests)
            {
                break;
            }

            var line = new FormatString(
                LocalizationTemplates.QuestPreviousLine,
                localizationService
            );
            line.AddData("mark", WithTrailingSpace(config.Previous));
            line.AddData("questName", GetQuestName(previousId));
            lines.Add(line.ToString());
            shown++;
        }

        if (config.MaxRequiredQuests > 0 && prerequisites.Length > shown)
        {
            lines.Add(
                Format(
                    LocalizationTemplates.QuestMore,
                    ("mark", WithTrailingSpace(config.Previous)),
                    ("count", prerequisites.Length - shown)
                )
            );
        }

        return lines;
    }

    /// <summary>
    /// Comma separated quest names, truncated to maxRequiredQuests with a "+N" suffix.
    /// </summary>
    private string FormatQuestList(ImmutableArray<MongoId> quests, ModConfig.QuestLinks config)
    {
        var limit =
            config.MaxRequiredQuests > 0 && quests.Length > config.MaxRequiredQuests
                ? config.MaxRequiredQuests
                : quests.Length;

        var names = string.Join(", ", quests.Take(limit).Select(GetQuestName));

        return limit < quests.Length ? $"{names}, … (+{quests.Length - limit})" : names;
    }

    private string Format(string template, params (string Key, object Value)[] data)
    {
        var line = new FormatString(template, localizationService);

        foreach (var (key, value) in data)
        {
            line.AddData(key, value);
        }

        return line.ToString();
    }

    private static string WithTrailingSpace(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : value + " ";
}
