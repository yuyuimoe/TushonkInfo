using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using TushonkInfo.Caches.Models;
using TushonkInfo.Extensions;
using TushonkInfo.Services;
using TushonkInfo.Utils;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;

namespace TushonkInfo.Caches;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.RagfairCallbacks + 65536)]
public sealed class ItemDataCache(
    ISptLogger<ItemDataCache> logger,
    DatabaseService db,
    ItemBaseClassService itemBaseClassService,
    RandomUtil randomUtil,
    LocalizationService localizationService,
    RagfairUtils ragfairUtils,
    TraderCache traderCache
) : IOnLoad
{
    public FrozenDictionary<MongoId, TemplateItemSlim> TemplateItemSlims { get; private set; }

    /// <summary>
    /// Dictionary of item's highest sell price
    /// Key = Item Template MongoID
    /// Value = KeyValuePair of Trader MongoID and price
    /// </summary>
    public FrozenDictionary<MongoId, KeyValuePair<MongoId, double>> TemplateBestTraderPrice
    {
        get;
        private set;
    }

    public FrozenDictionary<MongoId, double> FleaPrices { get; private set; }

    public FrozenDictionary<MongoId, double> HandbookPrices { get; private set; }

    public FrozenSet<MongoId> ItemsWithHandbookEntry { get; private set; }

    /// <summary>
    /// Items that cannot be sold on the flea market, either by flag
    /// (CanSellOnRagfair) or by the BSG static blacklist.
    /// </summary>
    public FrozenSet<MongoId> FleaBannedItems { get; private set; }

    public FrozenDictionary<MongoId, double> InnerOuterSlotRatios { get; private set; }

    public FrozenDictionary<MongoId, FrozenSet<MongoId>> TemplateBaseClasses { get; private set; }

    public FrozenDictionary<MongoId, HeadsetInfo> TemplateHeadsetInfo { get; private set; }

    /// <summary>
    /// Dictionary of armor stats per item.
    /// Key = Item Template MongoID
    /// </summary>
    public FrozenDictionary<MongoId, ArmorItemSlim> TemplateArmorInfo { get; private set; }

    /// <summary>
    /// Dictionary of armor materials from globals.
    /// Key = ArmorMaterial
    /// </summary>
    public FrozenDictionary<ArmorMaterial, ArmorMaterialSlim> ArmorMaterials { get; private set; }

    public Task OnLoad()
    {
        var watch = new Stopwatch();
        watch.Start();

        var items = db.GetItems();
        var locales = db.GetLocales().Languages;
        var handbook = db.GetHandbook().Items;

        var headphones = items
            .Where(i => i.Value.Parent == BaseClasses.HEADPHONES)
            .ToFrozenDictionary();

        TemplateItemSlims = BuildTemplateItemSlims(items);
        TemplateBaseClasses = BuildTemplateBaseClasses();
        TemplateHeadsetInfo = BuildTemplateHeadsetInfo(headphones);
        TemplateArmorInfo = BuildTemplateArmorInfo(items);
        ArmorMaterials = BuildArmorMaterials();
        FleaPrices = BuildFleaPrices();
        ItemsWithHandbookEntry = BuildItemsWithHandbook(handbook);
        FleaBannedItems = BuildFleaBannedItems(items);
        HandbookPrices = BuildHandbookPrices(handbook);
        InnerOuterSlotRatios = BuildSlotRatio();
        TemplateBestTraderPrice = BuildBestTraderPrice();

        watch.Stop();
        logger.Info($"Item Cache loaded in {watch.ElapsedMilliseconds}ms");
        return Task.CompletedTask;
    }

    private FrozenDictionary<MongoId, TemplateItemSlim> BuildTemplateItemSlims(
        Dictionary<MongoId, TemplateItem> items
    ) =>
        items
            .Select(i => new TemplateItemSlim(
                TemplateId: i.Key,
                Width: i.Value.Properties?.Width ?? 1,
                Height: i.Value.Properties?.Height ?? 1,
                StackMaxSize: i.Value.Properties?.StackMaxSize ?? 1,
                Grids: i.Value.Properties?.Grids?.ToImmutableList() ?? []
            ))
            .ToFrozenDictionary(k => k.TemplateId);

    private FrozenDictionary<MongoId, HeadsetInfo> BuildTemplateHeadsetInfo(
        FrozenDictionary<MongoId, TemplateItem> headphones
    ) =>
        headphones
            .Select(i => new HeadsetInfo(
                i.Key,
                BuildAmbientVolume(i.Value.Properties!),
                i.Value.Properties!.CompressorAttack ?? 35d,
                i.Value.Properties!.CompressorGain ?? 10d,
                i.Value.Properties!.CompressorThreshold ?? -20d,
                i.Value.Properties!.Distortion ?? 0.28d
            ))
            .ToFrozenDictionary(x => x.TemplateId);

    private FrozenDictionary<MongoId, ArmorItemSlim> BuildTemplateArmorInfo(
        Dictionary<MongoId, TemplateItem> items
    ) =>
        items
            .Where(i => i.Value.Properties is { ArmorClass: > 0, ArmorMaterial: not null })
            .Select(i => new ArmorItemSlim(
                i.Key,
                i.Value.Properties!.ArmorClass!.Value,
                i.Value.Properties.ArmorMaterial!.Value,
                i.Value.Properties.MaxDurability ?? 0d
            ))
            .ToFrozenDictionary(x => x.TemplateId);

    private FrozenDictionary<ArmorMaterial, ArmorMaterialSlim> BuildArmorMaterials() =>
        (db.GetGlobals().Configuration.ArmorMaterials ?? new()).ToFrozenDictionary(
            k => k.Key,
            v => new ArmorMaterialSlim(
                v.Value.Destructibility,
                v.Value.MinRepairDegradation,
                v.Value.MaxRepairDegradation
            )
        );

    /// <summary>
    /// Ambient volume as the old mod displayed it: the four compressor send levels,
    /// re-based to their neutral values.
    /// </summary>
    private static double BuildAmbientVolume(TemplateItemProperties properties) =>
        Math.Round(
            (
                (properties.AmbientCompressorSendLevel ?? -10d)
                + 10d
                + (properties.EffectsReturnsGrEnvCommonCompressorSendLeveloupVolume ?? -7d)
                + 7d
                + (properties.EnvNatureCompressorSendLevel ?? -5d)
                + 5d
                + (properties.EnvTechnicalCompressorSendLevel ?? -7d)
                + 7d
            ),
            1
        );

    private FrozenDictionary<MongoId, FrozenSet<MongoId>> BuildTemplateBaseClasses() =>
        TemplateItemSlims.ToFrozenDictionary(
            i => i.Key,
            i => itemBaseClassService.GetItemBaseClasses(i.Key).ToFrozenSet()
        );

    private FrozenDictionary<MongoId, double> BuildFleaPrices() =>
        TemplateItemSlims.ToFrozenDictionary(
            k => k.Key,
            v => ragfairUtils.GetItemPrice(v.Value.TemplateId)
        );

    private FrozenSet<MongoId> BuildItemsWithHandbook(List<HandbookItem> handbookItems) =>
        handbookItems.Select(hi => hi.Id).ToFrozenSet();

    private FrozenSet<MongoId> BuildFleaBannedItems(Dictionary<MongoId, TemplateItem> items)
    {
        if (ConfigurationService.Config.UseBsgStaticFleaBanList.Enabled)
        {
            return ConfigurationService
                .BsgBlackList.BsgBlackList.Where(MongoId.IsValidMongoId)
                .Select(id => new MongoId(id))
                .ToFrozenSet();
        }

        return items
            .Where(i => i.Value.Properties?.CanSellOnRagfair == false)
            .Select(i => i.Key)
            .ToFrozenSet();
    }

    private FrozenDictionary<MongoId, double> BuildHandbookPrices(
        List<HandbookItem> handbookItems
    ) => handbookItems.ToFrozenDictionary(k => k.Id, v => v.Price ?? 0);

    private FrozenDictionary<MongoId, double> BuildSlotRatio() =>
        TemplateItemSlims
            .Where(i => !i.Value.Grids.IsEmpty)
            .ToFrozenDictionary(
                k => k.Key,
                v =>
                {
                    var cells = v.Value.Width * v.Value.Height;
                    var innerSlots = v.Value.Grids.Sum(x =>
                        (x.Properties?.CellsH ?? 0) * (x.Properties?.CellsV ?? 0)
                    );

                    // Inner slots per outer cell: > 1 means the container creates space
                    return cells == 0 ? 0 : (double)innerSlots / cells;
                }
            );

    private FrozenDictionary<MongoId, KeyValuePair<MongoId, double>> BuildBestTraderPrice() =>
        TemplateItemSlims
            .Keys.Where(tplId =>
                ItemsWithHandbookEntry.Contains(tplId)
                && traderCache.TemplateTraderAssorts.ContainsKey(tplId)
            )
            .Select(tplId =>
            {
                var baseClass = TemplateBaseClasses[tplId];

                var offers = traderCache
                    .TemplateTraderAssorts[tplId]
                    .Keys.Where(traderId =>
                    {
                        var buyData = traderCache.TraderBuyData[traderId];
                        var blacklist = traderCache.TraderBuyBlacklists[traderId];
                        return buyData.IdList.Contains(tplId)
                            && buyData.Category.Overlaps(baseClass)
                            && !blacklist.IdList.Contains(tplId);
                    })
                    .Select(traderId => new KeyValuePair<MongoId, double>(
                        traderId,
                        randomUtil.GetPercentOfValue(
                            traderCache.TraderBuyPriceCoefficient[traderId],
                            HandbookPrices[tplId],
                            0
                        )
                    ))
                    .DefaultIfEmpty(new KeyValuePair<MongoId, double>(MongoId.Empty(), 0))
                    .MaxBy(x => x.Value);

                return (Key: tplId, Best: offers);
            })
            .Where(x => x.Best.Key != default)
            .ToFrozenDictionary(x => x.Key, x => x.Best);
}
