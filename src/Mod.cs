using System.Diagnostics;
using ItemInfo.Caches;
using ItemInfo.Constants;
using ItemInfo.Services;
using ItemInfo.Utils;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Utils;

namespace ItemInfo;

[Injectable(TypePriority = OnLoadOrder.PostSptModLoader + 1)]
public class Mod(
    ISptLogger<Mod> logger,
    TemplateItemUtils templateItemUtils,
    ItemDataCache itemDataCache,
    QuestCache questCache,
    QuestUtils questUtils,
    LocalizationService localizationService
) : IOnLoad
{
    public Task OnLoad()
    {
        var config = ConfigurationService.Config;
        if (config is null)
        {
            logger.Error("[ItemInfo] Config was not loaded, skipping item descriptions");
            return Task.CompletedTask;
        }

        var stopWatch = Stopwatch.StartNew();
        var localeChanged = new List<KeyValuePair<string, string>>();
        var nameSuffixes = new Dictionary<MongoId, string>();
        var shortNameSuffixes = new Dictionary<MongoId, string>();

        foreach (var tplId in itemDataCache.TemplateItemSlims.Keys)
        {
            // Ignore "useless" items (no handbook entry), same as the previous implementation
            if (!itemDataCache.ItemsWithHandbookEntry.Contains(tplId))
            {
                continue;
            }

            // One list per category. Categories are separated by a blank line,
            // lines inside a category by a single newline.
            var groups = new List<IReadOnlyList<string>>();

            // Price
            var price = new List<string>();
            if (config.ModPriceInfo.Enabled)
            {
                Add(price, templateItemUtils.GetLocalizedPricing(tplId));
            }
            groups.Add(price);

            // Equipment / container stats
            var gear = new List<string>();

            if (config.ModHeadsetInfo.Enabled)
            {
                Add(gear, templateItemUtils.GetLocalizedHeadsetInfo(tplId));
            }

            if (
                config.ModArmorInfo.Enabled
                && templateItemUtils.GetLocalizedArmorInfo(tplId) is { } armor
            )
            {
                Add(gear, armor.Description);

                if (config.ModArmorInfo.AddArmorToName)
                {
                    AppendSuffix(nameSuffixes, tplId, armor.NameSuffix);
                }

                if (config.ModArmorInfo.AddArmorToShortName)
                {
                    AppendSuffix(shortNameSuffixes, tplId, armor.ShortNameSuffix);
                }
            }

            if (config.ModContainerInfo.Enabled)
            {
                Add(gear, templateItemUtils.GetLocalizedSlotRatio(tplId));
            }

            groups.Add(gear);

            // Barters
            var barters = new List<string>();

            if (config.ModBarterInfo.Enabled)
            {
                barters.AddRange(templateItemUtils.GetLocalizedBarterResultList(tplId));
            }

            if (config.ModBarterResourceInfo.Enabled)
            {
                barters.AddRange(templateItemUtils.GetLocalizedBarterResourceList(tplId));
            }

            groups.Add(barters);

            // Hideout requirements
            var hideoutRequirements = new List<string>();

            if (config.ModHideoutInfo.Enabled)
            {
                hideoutRequirements.AddRange(
                    templateItemUtils.GetLocalizedHideoutAreaRequirement(tplId)
                );
                hideoutRequirements.AddRange(
                    templateItemUtils.GetLocalizedHideoutAreaStageImprovementRequirement(tplId)
                );
            }

            groups.Add(hideoutRequirements);

            // Hideout crafts
            var hideoutCrafts = new List<string>();

            if (config.ModProductionInfo.Enabled)
            {
                hideoutCrafts.AddRange(templateItemUtils.GetLocalizedProduction(tplId));
            }

            if (config.ModCraftingMaterialInfo.Enabled)
            {
                hideoutCrafts.AddRange(templateItemUtils.GetLocalizedCraftingMaterial(tplId));
            }

            groups.Add(hideoutCrafts);

            // Quests
            var quests = new List<string>();

            if (config.ModQuestInfo.Enabled)
            {
                var questLines = templateItemUtils.GetLocalizedQuestInfo(tplId);

                if (questLines.Count > 0)
                {
                    quests.AddRange(questLines);

                    if (
                        config.ModQuestInfo.FirInName
                        && templateItemUtils.HasFoundInRaidQuestRequirement(tplId)
                    )
                    {
                        var firMark = ConfigurationService.Config.Markers.FoundInRaid;

                        if (!string.IsNullOrWhiteSpace(firMark))
                        {
                            AppendSuffix(nameSuffixes, tplId, " " + firMark);
                        }
                    }
                }
            }

            groups.Add(quests);

            var sections = groups.Where(g => g.Count > 0).ToList();

            if (sections.Count == 0)
            {
                continue;
            }

            localeChanged.Add(
                new KeyValuePair<string, string>(
                    $"{tplId} Description",
                    string.Join("\n\n", sections.Select(g => string.Join("\n", g)))
                )
            );
        }

        var questDescriptions = new List<KeyValuePair<string, string>>();

        if (config.ModQuestLinks.Enabled)
        {
            foreach (var questId in questCache.QuestUnlockRequirements.Keys)
            {
                var lines = questUtils.GetLocalizedQuestLinks(questId);

                if (lines.Count > 0)
                {
                    questDescriptions.Add(
                        new KeyValuePair<string, string>(
                            $"{questId} description",
                            string.Join("\n", lines)
                        )
                    );
                }
            }
        }

        localizationService.UpsertLocaleTransformer(localeChanged);
        localizationService.UpsertLocaleTransformer(questDescriptions, prepend: false);
        localizationService.UpsertSuffixes(nameSuffixes);
        localizationService.UpsertSuffixes(shortNameSuffixes, "ShortName");

        stopWatch.Stop();
        logger.Info(
            $"[ItemInfo] Built {localeChanged.Count} item and {questDescriptions.Count} quest descriptions in {stopWatch.ElapsedMilliseconds}ms"
        );
        return Task.CompletedTask;
    }

    private static void Add(List<string> sections, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            sections.Add(value);
        }
    }

    private static void AppendSuffix(
        Dictionary<MongoId, string> suffixes,
        MongoId templateId,
        string suffix
    )
    {
        if (string.IsNullOrEmpty(suffix))
        {
            return;
        }

        suffixes[templateId] = suffixes.TryGetValue(templateId, out var existing)
            ? existing + suffix
            : suffix;
    }
}
