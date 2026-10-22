using System.Collections.Frozen;
using System.Globalization;
using TushonkInfo.Caches;
using TushonkInfo.Caches.Models;
using TushonkInfo.Constants;
using TushonkInfo.Extensions;
using TushonkInfo.Models;
using TushonkInfo.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Hideout;
using SPTarkov.Server.Core.Models.Enums.Hideout;
using SPTarkov.Server.Core.Models.Utils;

namespace TushonkInfo.Utils;

[Injectable]
public class TemplateItemUtils(
    ISptLogger<TemplateItemUtils> logger,
    ItemDataCache itemDataCache,
    TraderCache traderCache,
    LocaleCache localeCache,
    HideoutCache hideoutCache,
    QuestCache questCache,
    DatabaseUtils databaseUtils,
    LocalizationService localizationService
)
{
    private const string BitcoinTemplate = "59faff1d86f7746c51718c9c";

    /// <summary>
    /// Configured language, falling back to english when it is not a game locale.
    /// </summary>
    private string Locale =>
        localeCache.TemplateShortNames.ContainsKey(ConfigurationService.Config.UserLocale)
            ? ConfigurationService.Config.UserLocale
            : "en";

    private FrozenDictionary<MongoId, string> ShortNames => localeCache.TemplateShortNames[Locale];

    private FrozenDictionary<MongoId, string> TraderNames => localeCache.TraderNicknames[Locale];

    private string GetTraderName(MongoId traderId) =>
        TraderNames.TryGetValue(traderId, out var name)
            ? name
            : traderId.ToString() ?? string.Empty;

    private string GetShortName(MongoId templateId) =>
        ShortNames.TryGetValue(templateId, out var name)
            ? name
            : templateId.ToString() ?? string.Empty;

    private (double price, double profit) CalculateBarterTotalPrice(in TraderBarterInfo barterInfo)
    {
        var barterPrice = 0d;
        if (
            traderCache.TemplateBarterTotalFleaPrice.TryGetValue(
                barterInfo.TemplateId,
                out var barterTotals
            )
        )
        {
            if (!barterTotals.TryGetValue(barterInfo.AssortId, out barterPrice))
            {
                barterPrice = barterTotals.GetValueOrDefault(barterInfo.AssortParentId, 0d);
            }
        }

        if (barterPrice <= 0)
        {
            return (0d, 0d);
        }

        var profit =
            barterPrice - itemDataCache.FleaPrices.GetValueOrDefault(barterInfo.TemplateId, 0d);
        return (barterPrice, profit);
    }

    private string FormatItemRequirements(
        in List<List<BarterScheme>> barters,
        FrozenDictionary<MongoId, string> templateShortNames
    ) =>
        string.Join(
            ", ",
            barters.SelectMany(x =>
                x.Select(y => $"{y.Count ?? 1d}x {templateShortNames[y.Template]}")
            )
        );

    private string FormatBarterPrice(
        string template,
        string dataKey,
        double value,
        in LocalizationService service
    ) => FormatString.QuickFormat(template, dataKey, value.FormatToPrice(), service);

    public string GetLocalizedSlotRatio(MongoId templateId)
    {
        if (
            !itemDataCache.InnerOuterSlotRatios.TryGetValue(templateId, out var ratio)
            || !itemDataCache.TemplateItemSlims.TryGetValue(templateId, out var item)
        )
        {
            return string.Empty;
        }

        var innerSlots = item.Grids.Sum(x =>
            (x.Properties?.CellsH ?? 0) * (x.Properties?.CellsV ?? 0)
        );
        var cells = item.Width * item.Height;

        var slotEfficiency = new FormatString(
            LocalizationTemplates.SlotEfficiency,
            localizationService
        );
        slotEfficiency.AddData(
            "ratio",
            Math.Round(ratio, 2).ToString(CultureInfo.InvariantCulture)
        );
        slotEfficiency.AddData("slots", innerSlots);
        slotEfficiency.AddData("cells", cells);

        return slotEfficiency.ToString();
    }

    public string GetLocalizedPricing(MongoId templateId)
    {
        var priceInfo = ConfigurationService.Config.ModPriceInfo;
        var segments = new List<string>();
        var isBanned = itemDataCache.FleaBannedItems.Contains(templateId);

        if (priceInfo.AddFleaPrice)
        {
            segments.Add(
                isBanned
                    ? new FormatString(
                        LocalizationTemplates.PriceFleaBanned,
                        localizationService
                    ).ToString()
                    : FormatBarterPrice(
                        LocalizationTemplates.PriceFlea,
                        "price",
                        itemDataCache.FleaPrices.GetValueOrDefault(templateId, 0d),
                        localizationService
                    )
            );
        }

        if (priceInfo.AddItemValue)
        {
            segments.Add(
                FormatBarterPrice(
                    LocalizationTemplates.PriceItemValue,
                    "price",
                    itemDataCache.HandbookPrices.GetValueOrDefault(templateId, 0d),
                    localizationService
                )
            );
        }

        if (itemDataCache.TemplateBestTraderPrice.TryGetValue(templateId, out var offer))
        {
            var traderPrice = new FormatString(
                LocalizationTemplates.PriceTrader,
                localizationService
            );
            traderPrice.AddData("traderName", GetTraderName(offer.Key));
            traderPrice.AddData("price", offer.Value.FormatToPrice());
            segments.Add(traderPrice.ToString());
        }
        else
        {
            segments.Add(
                FormatBarterPrice(
                    LocalizationTemplates.PriceUnknownTrader,
                    "price",
                    0d,
                    localizationService
                )
            );
        }

        return string.Join(" | ", segments);
    }

    public string GetLocalizedHeadsetInfo(MongoId templateId)
    {
        if (!itemDataCache.TemplateHeadsetInfo.TryGetValue(templateId, out var headsetInfo))
        {
            return string.Empty;
        }

        var boost = headsetInfo.CompressionGain + Math.Abs(headsetInfo.CompressorThreshold + 20d);

        var headset = new FormatString(LocalizationTemplates.HeadsetInfo, localizationService);
        headset.AddData("ambient", Math.Round(headsetInfo.AmbientVolume, 1));
        headset.AddData("boost", Math.Round(boost, 1));
        headset.AddData("distortion", Math.Round(headsetInfo.Distortion * 100d, 1));

        return headset.ToString();
    }

    public List<string> GetLocalizedHideoutAreaRequirement(MongoId templateId)
    {
        if (!hideoutCache.AreaStageRequirementsByTemplate.TryGetValue(templateId, out var areas))
        {
            return [];
        }

        var hideoutAreaNames = localeCache.HideoutAreaNames[Locale];

        return areas
            .Select(a =>
            {
                var line = new FormatString(
                    LocalizationTemplates.HideoutAreaRequirement,
                    localizationService
                );
                line.AddData(
                    "mark",
                    WithTrailingSpace(ConfigurationService.Config.Markers.Requirement)
                );
                line.AddData(
                    "area",
                    hideoutAreaNames.GetValueOrDefault(
                        a.AreaType,
                        localizationService.GetTranslation("UNKNOWN")
                    )
                );
                line.AddData("level", a.Stage);
                line.AddData("count", a.Count);
                return line.ToString();
            })
            .ToList();
    }

    public List<string> GetLocalizedHideoutAreaStageImprovementRequirement(MongoId templateId)
    {
        if (
            !hideoutCache.AreaStageImprovementRequirementByTemplate.TryGetValue(
                templateId,
                out var areas
            )
        )
        {
            return [];
        }

        var hideoutAreaNames = localeCache.HideoutAreaNames[Locale];

        return areas
            .Select(a =>
            {
                var line = new FormatString(
                    LocalizationTemplates.HideoutStageImprovement,
                    localizationService
                );
                line.AddData(
                    "mark",
                    WithTrailingSpace(ConfigurationService.Config.Markers.Requirement)
                );
                line.AddData(
                    "area",
                    hideoutAreaNames.GetValueOrDefault(
                        hideoutCache.AreaTypeById.GetValueOrDefault(a.AreaId, HideoutAreas.NotSet),
                        localizationService.GetTranslation("UNKNOWN")
                    )
                );
                line.AddData("improvement", ConfigurationService.Config.Markers.Improvement);
                line.AddData("count", a.Count);
                return line.ToString();
            })
            .ToList();
    }

    public List<string> GetLocalizedBarterResultList(MongoId templateId)
    {
        if (!traderCache.TemplateResultTraderBarter.TryGetValue(templateId, out var barterInfoList))
        {
            return [];
        }

        var templateShortNames = localeCache.TemplateShortNames[Locale];

        var returnList = new List<string>();

        foreach (var barterInfo in barterInfoList)
        {
            var requiredItems = FormatItemRequirements(
                barterInfo.BarterScheme!.Barters,
                templateShortNames
            );

            var buyBarterTemplate = new FormatString(
                LocalizationTemplates.BuyBarterItem,
                localizationService
            );
            buyBarterTemplate.AddData("traderName", GetTraderName(barterInfo.TraderId));
            buyBarterTemplate.AddData("loyaltyLevel", barterInfo.BarterScheme!.LoyaltyLevel!);
            buyBarterTemplate.AddData("extraItems", requiredItems);

            var parentText = barterInfo.AssortParentId is "" or "hideout"
                ? string.Empty
                : $" ∈ {templateShortNames[barterInfo.AssortParentId]}";

            buyBarterTemplate.AddData("parent", parentText);

            var (price, profit) = CalculateBarterTotalPrice(barterInfo);
            if (price > 0d)
            {
                buyBarterTemplate.AddData(
                    "totalBarterFleaPrice",
                    FormatBarterPrice(
                        LocalizationTemplates.TotalBarterFleaPrice,
                        "totalPrice",
                        price,
                        localizationService
                    )
                );

                buyBarterTemplate.AddData(
                    "deltaBarterPrice",
                    FormatBarterPrice(
                        LocalizationTemplates.DeltaBarterFleaPrice,
                        "diffPrice",
                        profit,
                        localizationService
                    )
                );
            }

            returnList.Add(buyBarterTemplate.ToString());
        }

        return returnList;
    }

    public List<string> GetLocalizedBarterResourceList(MongoId templateId)
    {
        if (
            !traderCache.TemplateResourceTraderBarter.TryGetValue(
                templateId,
                out var barterInfoList
            )
        )
        {
            return [];
        }

        var templateShortNames = localeCache.TemplateShortNames[Locale];

        var returnList = new List<string>();

        foreach (var barterInfo in barterInfoList)
        {
            var sellBarterTemplate = new FormatString(
                LocalizationTemplates.SellBarterItem,
                localizationService
            );

            if (
                !traderCache.AssortBarterTemplateCount.TryGetValue(
                    barterInfo.AssortId,
                    out var barterCounts
                )
            )
            {
                continue;
            }

            var matchingCount = barterCounts.FirstOrDefault(x => x.Key == templateId);

            sellBarterTemplate.AddData("traderName", GetTraderName(barterInfo.TraderId));
            sellBarterTemplate.AddData("loyaltyLevel", barterInfo.BarterScheme!.LoyaltyLevel);
            sellBarterTemplate.AddData("itemName", templateShortNames[barterInfo.TemplateId]);
            sellBarterTemplate.AddData("count", matchingCount.Value);
            sellBarterTemplate.AddData(
                "extraItems",
                FormatItemRequirements(barterInfo.BarterScheme!.Barters, templateShortNames)
            );

            if (CalculateBarterTotalPrice(barterInfo) is (var price and > 0, var profit))
            {
                sellBarterTemplate.AddData(
                    "totalBarterFleaPrice",
                    FormatBarterPrice(
                        LocalizationTemplates.TotalBarterFleaPrice,
                        "totalPrice",
                        price,
                        localizationService
                    )
                );

                sellBarterTemplate.AddData(
                    "deltaBarterPrice",
                    FormatBarterPrice(
                        LocalizationTemplates.DeltaBarterFleaPrice,
                        "diffPrice",
                        profit,
                        localizationService
                    )
                );
            }

            returnList.Add(sellBarterTemplate.ToString());
        }

        return returnList;
    }

    /// <summary>
    /// Quests that require the item (Handover / Find / Place) and quests that
    /// unlock it on a trader.
    /// </summary>
    public List<string> GetLocalizedQuestInfo(MongoId templateId)
    {
        var lines = new List<string>();

        var markers = ConfigurationService.Config.Markers;

        if (questCache.TemplateQuestRequirements.TryGetValue(templateId, out var requirements))
        {
            foreach (var requirement in requirements)
            {
                var line = new FormatString(
                    LocalizationTemplates.QuestRequirement,
                    localizationService
                );

                line.AddData(
                    "mark",
                    WithTrailingSpace(
                        requirement.Kind == QuestItemRequirementKind.Place
                            ? markers.Place
                            : markers.Requirement
                    )
                );
                line.AddData(
                    "fir",
                    requirement.FoundInRaid ? WithTrailingSpace(markers.FoundInRaid) : string.Empty
                );
                line.AddData("count", requirement.Count);
                line.AddData(
                    "questName",
                    localizationService.GetQuestName(requirement.QuestId, Locale)
                );
                line.AddData(
                    "traderName",
                    GetTraderName(questCache.GetQuestTrader(requirement.QuestId))
                );

                lines.Add(line.ToString());
            }
        }

        if (questCache.TemplateQuestUnlocks.TryGetValue(templateId, out var unlocks))
        {
            foreach (var unlock in unlocks)
            {
                var line = new FormatString(
                    LocalizationTemplates.QuestRewardItem,
                    localizationService
                );

                line.AddData("mark", WithTrailingSpace(markers.Unlock));
                line.AddData("questName", localizationService.GetQuestName(unlock.QuestId, Locale));
                line.AddData(
                    "questGiver",
                    GetTraderName(questCache.GetQuestTrader(unlock.QuestId))
                );
                line.AddData("traderName", GetTraderName(unlock.TraderId));
                line.AddData("loyaltyLevel", unlock.LoyaltyLevel);

                lines.Add(line.ToString());
            }
        }

        // The same quest can list the same item twice (e.g. two planting zones)
        return lines.Distinct().ToList();
    }

    /// <summary>
    /// Recipes that produce the item ("what does it craft into").
    /// </summary>
    public List<string> GetLocalizedProduction(MongoId templateId)
    {
        if (
            !hideoutCache.ProductionsByRewardItem.TryGetValue(templateId, out var production)
            || production.Recipes is null
        )
        {
            return [];
        }

        var lines = new List<string>();

        foreach (var recipe in production.Recipes)
        {
            var requirements = recipe.Requirements;
            if (requirements is null || requirements.Count == 0)
            {
                continue;
            }

            if (
                recipe.AreaType == HideoutAreas.ChristmasIllumination
                || (recipe.Locked == true && requirements.All(r => r.QuestId is not null))
            )
            {
                continue;
            }

            var area = GetRecipeArea(recipe.AreaType, requirements);
            var outputCount = recipe.Count ?? 1;

            if (recipe.EndProduct == BitcoinTemplate)
            {
                var productionTime = recipe.ProductionTime ?? 0d;
                var bitcoin = new FormatString(
                    LocalizationTemplates.HideoutBitcoinInformation,
                    localizationService
                );
                bitcoin.AddData("area", area);
                bitcoin.AddData("gpu1", FormatDuration(GpuTime(1, productionTime)));
                bitcoin.AddData("gpu10", FormatDuration(GpuTime(10, productionTime)));
                bitcoin.AddData("gpu25", FormatDuration(GpuTime(25, productionTime)));
                bitcoin.AddData("gpu50", FormatDuration(GpuTime(50, productionTime)));
                lines.Add(bitcoin.ToString());
                continue;
            }

            var (components, cost) = BuildRecipeComponents(requirements);

            var result = new FormatString(
                LocalizationTemplates.HideoutCraftingResult,
                localizationService
            );
            result.AddData("count", outputCount);
            result.AddData("area", area);
            result.AddData("quest", GetQuestRequirement(requirements));
            result.AddData("components", components);
            result.AddData(
                "perItem",
                outputCount > 1 ? " " + localizationService.GetTranslation("peritem") : string.Empty
            );
            result.AddData("price", Math.Round(cost / outputCount).FormatToPrice());
            lines.Add(result.ToString());
        }

        return lines;
    }

    /// <summary>
    /// Recipes that consume the item ("what uses it as a crafting material").
    /// </summary>
    public List<string> GetLocalizedCraftingMaterial(MongoId templateId)
    {
        if (
            !hideoutCache.ProductionsByRequiredItem.TryGetValue(templateId, out var production)
            || production.Recipes is null
        )
        {
            return [];
        }

        var lines = new List<string>();

        foreach (var recipe in production.Recipes)
        {
            var requirements = recipe.Requirements;
            if (requirements is null)
            {
                continue;
            }

            if (recipe.AreaType == HideoutAreas.ChristmasIllumination)
            {
                continue;
            }

            var requirement = requirements.FirstOrDefault(r =>
                r.TemplateId == templateId && r.Type is "Item" or "Tool"
            );
            if (requirement is null)
            {
                continue;
            }

            var (components, cost) = BuildRecipeComponents(requirements, templateId);
            var outputCount = recipe.Count ?? 1;
            var profit =
                itemDataCache.FleaPrices.GetValueOrDefault(recipe.EndProduct, 0d) * outputCount
                - cost;

            var line = new FormatString(
                LocalizationTemplates.HideoutCraftingMaterial,
                localizationService
            );
            line.AddData(
                "role",
                localizationService.GetTranslation(requirement.Type == "Tool" ? "Tool" : "Part")
            );
            line.AddData("count", requirement.Count ?? 1);
            line.AddData("item", localizationService.GetItemName(recipe.EndProduct, Locale));
            line.AddData("amount", outputCount);
            line.AddData("area", GetRecipeArea(recipe.AreaType, requirements));
            line.AddData("quest", GetQuestRequirement(requirements));
            line.AddData("components", components);
            line.AddData("profit", Math.Round(profit).FormatToPrice());
            lines.Add(line.ToString());
        }

        return lines;
    }

    /// <summary>
    /// Armor class, effective durability and repair degradation, plus the
    /// name/short name suffix used by the armor config options.
    /// </summary>
    public (string Description, string NameSuffix, string ShortNameSuffix)? GetLocalizedArmorInfo(
        MongoId templateId
    )
    {
        if (
            !itemDataCache.TemplateArmorInfo.TryGetValue(templateId, out var armor)
            || !itemDataCache.ArmorMaterials.TryGetValue(armor.Material, out var material)
            || material.Destructibility <= 0d
        )
        {
            return null;
        }

        var maxDurability = Math.Round(armor.MaxDurability);
        var effectiveDurability = Math.Round(armor.MaxDurability / material.Destructibility);

        var classInfo = ConfigurationService.Config.ModArmorInfo.AddArmorClassInfo
            ? FormatString.QuickFormat(
                LocalizationTemplates.ArmorClassInfo,
                "armorClass",
                armor.ArmorClass,
                localizationService
            )
            : string.Empty;

        var line = new FormatString(LocalizationTemplates.ArmorDurability, localizationService);
        line.AddData("classInfo", classInfo);
        line.AddData("effectiveDurability", effectiveDurability);
        line.AddData("maxDurability", maxDurability);
        line.AddData(
            "material",
            localizationService.GetGameTranslation("Mat" + armor.Material, Locale)
        );
        line.AddData("multiplier", Math.Round(1d / material.Destructibility, 1));
        line.AddData("minRepair", Math.Round(material.MinRepairDegradation * 100d));
        line.AddData("maxRepair", Math.Round(material.MaxRepairDegradation * 100d));

        var suffix = $" ({armor.ArmorClass}/{maxDurability})";
        return (line.ToString(), suffix, suffix);
    }

    private string GetRecipeArea(HideoutAreas? areaType, List<Requirement> requirements)
    {
        var areaName = areaType is null
            ? "UNKNOWN AREA"
            : localeCache
                .HideoutAreaNames[Locale]
                .GetValueOrDefault(areaType.Value, areaType.Value.ToString());
        var level = requirements.FirstOrDefault(r => r.Type == "Area")?.RequiredLevel;

        if (level is null or <= 0)
        {
            return areaName;
        }

        var area = new FormatString(LocalizationTemplates.HideoutAreaLevel, localizationService);
        area.AddData("area", areaName);
        area.AddData("requiredLevel", level);
        return area.ToString();
    }

    private string GetQuestRequirement(List<Requirement> requirements)
    {
        var questId = requirements.FirstOrDefault(r => r.Type == "QuestComplete")?.QuestId;

        if (questId is null)
        {
            return string.Empty;
        }

        var name = localizationService.GetQuestName(questId.Value, Locale);
        var fir = ConfigurationService.Config.Markers.FoundInRaid;

        return string.IsNullOrWhiteSpace(fir) ? $" ({name})" : $" ({name} {fir})";
    }

    /// <summary>
    /// Item and resource requirements of a recipe, formatted and totaled by flea price.
    /// </summary>
    private (string Components, double Cost) BuildRecipeComponents(
        List<Requirement> requirements,
        MongoId? skipTemplate = null
    )
    {
        var parts = new List<string>();
        var cost = 0d;

        foreach (var requirement in requirements)
        {
            if (requirement.TemplateId is not { } tpl)
            {
                continue;
            }

            if (skipTemplate is { } skip && tpl == skip)
            {
                continue;
            }

            switch (requirement.Type)
            {
                case "Item":
                case "Tool":
                {
                    var count = requirement.Count ?? 1;
                    var component = new FormatString(
                        LocalizationTemplates.HideoutCraftingCount,
                        localizationService
                    );
                    component.AddData("itemShortName", GetShortName(tpl));
                    component.AddData("count", count);
                    parts.Add(component.ToString());
                    cost += itemDataCache.FleaPrices.GetValueOrDefault(tpl, 0d) * count;
                    break;
                }

                case "Resource":
                {
                    var resource = requirement.Resource ?? 0;
                    var capacity = databaseUtils.GetItemFromTpl(tpl)?.Properties?.Resource ?? 0d;
                    var proportion = capacity > 0d ? resource / capacity : 1d;
                    var component = new FormatString(
                        LocalizationTemplates.HideoutResourcePercentage,
                        localizationService
                    );
                    component.AddData("itemShortName", GetShortName(tpl));
                    component.AddData("percent", Math.Round(proportion * 100d));
                    parts.Add(component.ToString());
                    cost += itemDataCache.FleaPrices.GetValueOrDefault(tpl, 0d) * proportion;
                    break;
                }
            }
        }

        var components = string.Concat(parts);
        if (components.EndsWith("+ ", StringComparison.Ordinal))
        {
            components = components[..^2];
        }

        return (components.TrimEnd(), cost);
    }

    private static string WithTrailingSpace(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : value + " ";

    private double GpuTime(int gpus, double time) =>
        time / (1d + (gpus - 1) * hideoutCache.GpuBoostRate);

    private string FormatDuration(double seconds)
    {
        var hours = Math.Truncate(seconds / 3600d);
        var minutes = Math.Round((seconds - hours * 3600d) / 60d);

        return $"{hours} {localizationService.GetGameTranslation("HOURS", Locale)} "
            + $"{minutes} {localizationService.GetGameTranslation("Min", Locale)}";
    }

    public bool HasFoundInRaidQuestRequirement(MongoId templateId) =>
        questCache.TemplateQuestRequirements.TryGetValue(templateId, out var requirements)
        && requirements.Any(r => r.FoundInRaid);
}
