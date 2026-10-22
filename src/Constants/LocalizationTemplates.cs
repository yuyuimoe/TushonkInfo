namespace TushonkInfo.Constants;

public static class LocalizationTemplates
{
    public const string CurrencyRoubles = "₽";
    public const string CurrencyEuros = "€";
    public const string CurrencyDollars = "$";
    public const string CurrencyGpCoin = "GP";

    // Prices
    public const string PriceFlea = "{Fleaprice}: (price)₽";
    public const string PriceFleaBanned = "{Fleaprice}: {BANNED}";
    public const string PriceItemValue = "{ItemValue}: (price)₽";
    public const string PriceTrader = "{Valuation1}(traderName){Valuation2}: (price)₽";
    public const string PriceUnknownTrader = "{Valuation1}{UNKNOWN}{Valuation2}: (price)₽";

    // Barters
    public const string TotalBarterFleaPrice = "| Σ ≈ (totalPrice)₽";
    public const string DeltaBarterFleaPrice = "| Δ ≈ (diffPrice)₽";

    public const string BuyBarterItem =
        "{Bought}(parent) {at} (traderName) {lv}(loyaltyLevel) < (extraItems) (totalBarterFleaPrice) (deltaBarterPrice)";
    public const string SellBarterItem =
        "{Traded} ×(count) {at} (traderName) {lv}(loyaltyLevel) > (itemName) < … + (extraItems) (totalBarterFleaPrice) (deltaBarterPrice)";

    // Quests
    /// <summary>
    /// (mark) and (fir) are language neutral markers coming from the config file.
    /// </summary>
    public const string QuestRequirement = "(mark)(fir)×(count) > (questName) @ (traderName)";
    public const string QuestRewardItem =
        "(mark)(questName) (questGiver) @ (traderName) {lv}(loyaltyLevel)";

    /// <summary>
    /// Quest relationships. (mark) values come from the QuestLinks config section.
    /// </summary>
    public const string QuestUnlockLine = "(mark)(questName) ((requirements))";
    public const string QuestUnlockLineNoRequirements = "(mark)(questName)";
    public const string QuestPreviousLine = "(mark)(questName)";
    public const string QuestLevelRequirement = "(mark)(level)";
    public const string QuestTraderRequirement = "(traderName) (mark)(level)";
    public const string QuestRequiredQuests = "(mark)(quests)";
    public const string QuestMore = "(mark)… (+(count))";

    // Hideout
    public const string HideoutAreaLevel = "(area) {lv}(requiredLevel)";
    public const string HideoutAreaRequirement = "(mark)×(count) > (area) {lv}(level)";
    public const string HideoutStageImprovement = "(mark)×(count) > (area) (improvement)";

    public const string HideoutCraftingCount = "(itemShortName) ×(count) + ";
    public const string HideoutResourcePercentage = "(itemShortName) ×(percent)% + ";
    public const string HideoutCraftingMultipleItems = "{peritem}";

    public const string HideoutCraftingResult =
        "{Crafted} ×(count) @ (area)(quest) < (components) | Σ(perItem) ≈ (price)₽";
    public const string HideoutCraftingMaterial =
        "(role) ×(count) > (item) ×(amount) @ (area)(quest) < … + (components) | Δ ≈ (profit)₽";
    public const string HideoutBitcoinInformation =
        "{Crafted} @ (area) | GPU: 1× (gpu1), 10× (gpu10), 25× (gpu25), 50× (gpu50)";

    // Gear
    public const string SlotEfficiency = "{Slotefficiency}: ×(ratio) ((slots)/(cells))";
    public const string HeadsetInfo =
        "{AmbientVolume}: (ambient)db | {Boost}: +(boost)db | {Distortion}: (distortion)%";

    public const string ArmorClassInfo = "{Armorclass}: (armorClass) | ";
    public const string ArmorDurability =
        "(classInfo){Effectivedurability}: (effectiveDurability) ({Max}: (maxDurability) x (material): (multiplier)) | {Repairdegradation}: (minRepair)% - (maxRepair)%";
}
