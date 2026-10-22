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

            var sections = new List<string>();

            if (config.ModPriceInfo.Enabled)
            {
                Add(sections, templateItemUtils.GetLocalizedPricing(tplId));
            }

            if (config.ModHeadsetInfo.Enabled)
            {
                Add(sections, templateItemUtils.GetLocalizedHeadsetInfo(tplId));
            }

            if (
                config.ModArmorInfo.Enabled
                && templateItemUtils.GetLocalizedArmorInfo(tplId) is { } armor
            )
            {
                Add(sections, armor.Description);

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
                Add(sections, templateItemUtils.GetLocalizedSlotRatio(tplId));
            }

            if (config.ModQuestInfo.Enabled)
            {
                var questLines = templateItemUtils.GetLocalizedQuestInfo(tplId);

                if (questLines.Count > 0)
                {
                    sections.AddRange(questLines);

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

            if (config.ModHideoutInfo.Enabled)
            {
                sections.AddRange(templateItemUtils.GetLocalizedHideoutAreaRequirement(tplId));
                sections.AddRange(
                    templateItemUtils.GetLocalizedHideoutAreaStageImprovementRequirement(tplId)
                );
            }

            if (config.ModBarterInfo.Enabled)
            {
                sections.AddRange(templateItemUtils.GetLocalizedBarterResultList(tplId));
            }

            if (config.ModProductionInfo.Enabled)
            {
                sections.AddRange(templateItemUtils.GetLocalizedProduction(tplId));
            }

            if (config.ModCraftingMaterialInfo.Enabled)
            {
                sections.AddRange(templateItemUtils.GetLocalizedCraftingMaterial(tplId));
            }

            if (config.ModBarterResourceInfo.Enabled)
            {
                sections.AddRange(templateItemUtils.GetLocalizedBarterResourceList(tplId));
            }

            if (sections.Count == 0)
            {
                continue;
            }

            localeChanged.Add(
                new KeyValuePair<string, string>(
                    $"{tplId} Description",
                    string.Join("\n", sections)
                )
            );
        }

        localizationService.UpsertLocaleTransformer(localeChanged);
        localizationService.UpsertSuffixes(nameSuffixes);
        localizationService.UpsertSuffixes(shortNameSuffixes, "ShortName");

        stopWatch.Stop();
        logger.Info(
            $"[ItemInfo] Built {localeChanged.Count} item descriptions in {stopWatch.ElapsedMilliseconds}ms"
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
