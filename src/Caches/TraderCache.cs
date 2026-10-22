using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using ItemInfo.Caches.Models;
using ItemInfo.Extensions;
using ItemInfo.Utils;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace ItemInfo.Caches;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.TraderCallbacks + 65536)]
public sealed class TraderCache(
    ISptLogger<TraderCache> logger,
    DatabaseService db,
    TraderUtils traderUtils,
    RagfairPriceService ragfairPriceService,
    HandbookHelper handbookHelper
) : IOnLoad
{
    public FrozenDictionary<string, FrozenDictionary<MongoId, string>> TraderNicknames
    {
        get;
        private set;
    }

    /// <summary>
    /// Dictionary of Trader's Assorts, per Item
    /// Key = Item Template MongoID
    /// Value = Trader-Assort relation
    /// </summary>
    public FrozenDictionary<
        MongoId,
        FrozenDictionary<MongoId, IReadOnlyList<AssortSlim>>
    > TemplateTraderAssorts { get; private set; }

    /// <summary>
    /// Dictionary for Assort Parents, per Trader
    /// Key = Trader MongoID
    /// Value = Assort-Parent relation
    /// </summary>
    public FrozenDictionary<MongoId, FrozenDictionary<MongoId, MongoId>> TraderAssortsParent
    {
        get;
        private set;
    }

    /// <summary>
    /// Dictionary for Assorts per Loyalty, per Trader
    /// Key = Trader MongoID
    /// Value = Level-Assorts relation
    /// </summary>
    public FrozenDictionary<
        MongoId,
        FrozenDictionary<int, IReadOnlyList<AssortSlim>>
    > TraderLoyaltyAssorts { get; private set; }

    /// <summary>
    /// Dictionary for slim assorts per trader
    /// Key = Trader MongoID
    /// Value = AssortID-SlimAssort relation
    /// </summary>
    public FrozenDictionary<MongoId, FrozenDictionary<MongoId, AssortSlim>> TraderSlimAssorts
    {
        get;
        private set;
    }

    /// <summary>
    /// Dictionary of Barters by Resource Item
    /// Key = Item Template MongoID
    /// Value = List of Barters that needs the item
    /// </summary>
    public FrozenDictionary<MongoId, IReadOnlyList<TraderBarterInfo>> TemplateResourceTraderBarter
    {
        get;
        private set;
    }

    public FrozenDictionary<
        MongoId,
        ImmutableList<KeyValuePair<MongoId, double>>
    > AssortBarterTemplateCount { get; private set; }

    /// <summary>
    /// Dictionary of Barters by Result Item
    /// Key = Item Template MongoID
    /// Value = List of Barters where the item is the result
    /// </summary>
    public FrozenDictionary<MongoId, IReadOnlyList<TraderBarterInfo>> TemplateResultTraderBarter
    {
        get;
        private set;
    }

    /// <summary>
    /// Dictionary of total price of barters by TemplateID
    ///
    /// Key = Template ID
    /// Value = Assort-Value relation
    /// </summary>
    public FrozenDictionary<MongoId, FrozenDictionary<MongoId, double>> TemplateBarterTotalFleaPrice
    {
        get;
        private set;
    }

    /// <summary>
    /// Dictionary of Trader's slimmed blacklisted buy data
    /// Key = Trader MongoID
    /// Value = Slimmed down structure of ItemBuyData
    /// </summary>
    public FrozenDictionary<MongoId, ItemBuyDataSlim> TraderBuyBlacklists { get; private set; }

    /// <summary>
    /// Dictionary of Trader's slimmed buy data
    /// Key = Trader MongoID
    /// Value = Slimmed down structure of ItemBuyData
    /// </summary>
    public FrozenDictionary<MongoId, ItemBuyDataSlim> TraderBuyData { get; private set; }

    /// <summary>
    /// Dictionary of Trader's Buy Price Coefficient
    /// Key = Trader MongoID
    /// Value = Buy Price Coefficient
    /// </summary>
    public FrozenDictionary<MongoId, double> TraderBuyPriceCoefficient { get; private set; }

    public double EuroToRouble { get; set; }

    public double DollarToRouble { get; set; }

    public async Task OnLoad()
    {
        await Task.Run(() =>
        {
            var watch = new Stopwatch();
            watch.Start();

            var traders = db.GetTraders().Values;
            TraderBuyData = BuildTraderBuyData(traders);
            TraderBuyBlacklists = BuildTraderBlacklists(traders);
            TraderBuyPriceCoefficient = BuildTraderBuyPriceCoefficient(traders);
            TraderLoyaltyAssorts = BuildTraderLoyaltyAssorts(traders);
            TraderSlimAssorts = BuildTraderSlimAssorts();
            TemplateTraderAssorts = BuildTemplateTraderAssorts();
            TraderAssortsParent = BuildTraderAssortParents();
            TemplateResultTraderBarter = BuildTemplateResultTraderBarter(traders);
            TemplateResourceTraderBarter = BuildTemplateResourceTraderBarter();
            AssortBarterTemplateCount = BuildAssortBarterTemplateCount();
            TemplateBarterTotalFleaPrice = BuildBarterFleaPrice();

            EuroToRouble = handbookHelper.InRoubles(1, ItemTpl.MONEY_EUROS);
            DollarToRouble = handbookHelper.InRoubles(1, ItemTpl.MONEY_DOLLARS);
            watch.Stop();
            logger.Info($"Trader Cache loaded in {watch.ElapsedMilliseconds}ms");
        });
    }

    private FrozenDictionary<MongoId, ItemBuyDataSlim> BuildTraderBuyData(
        IEnumerable<Trader> traders
    ) =>
        traders.ToFrozenDictionary(
            t => t.Base.Id,
            t =>
            {
                var ib = t.Base.ItemsBuy;
                return new ItemBuyDataSlim(
                    ib?.Category.ToFrozenSet() ?? [],
                    ib?.IdList.ToFrozenSet() ?? []
                );
            }
        );

    private FrozenDictionary<MongoId, ItemBuyDataSlim> BuildTraderBlacklists(
        IEnumerable<Trader> traders
    ) =>
        traders.ToFrozenDictionary(
            t => t.Base.Id,
            t =>
            {
                var ibp = t.Base.ItemsBuyProhibited;
                return new ItemBuyDataSlim(
                    ibp?.Category.ToFrozenSet() ?? [],
                    ibp?.IdList.ToFrozenSet() ?? []
                );
            }
        );

    private FrozenDictionary<MongoId, double> BuildTraderBuyPriceCoefficient(
        IEnumerable<Trader> traders
    ) =>
        traders.ToFrozenDictionary(
            t => t.Base.Id,
            t => 100d - t.Base.LoyaltyLevels?.First().BuyPriceCoefficient ?? 0d
        );

    private FrozenDictionary<
        MongoId,
        FrozenDictionary<int, IReadOnlyList<AssortSlim>>
    > BuildTraderLoyaltyAssorts(IEnumerable<Trader> traders) =>
        traders.ToFrozenDictionary(
            t => t.Base.Id,
            t =>
                t.Assort.Items.GroupBy(i => t.Assort.LoyalLevelItems.GetValueOrDefault(i.Id, 1))
                    .ToFrozenDictionary(
                        g => g.Key,
                        g =>
                            (IReadOnlyList<AssortSlim>)
                                g.Select(i => new AssortSlim(
                                        i.Id,
                                        i.Template,
                                        i.ParentId ?? string.Empty,
                                        g.Key
                                    ))
                                    .ToList()
                    )
        );

    private FrozenDictionary<
        MongoId,
        FrozenDictionary<MongoId, AssortSlim>
    > BuildTraderSlimAssorts() =>
        TraderLoyaltyAssorts.ToFrozenDictionary(
            t => t.Key,
            t => t.Value.SelectMany(a => a.Value).ToFrozenDictionary(a => a.Id)
        );

    private FrozenDictionary<
        MongoId,
        FrozenDictionary<MongoId, IReadOnlyList<AssortSlim>>
    > BuildTemplateTraderAssorts() =>
        TraderLoyaltyAssorts
            .SelectMany(t =>
                t.Value.SelectMany(al =>
                    al.Value.Select(a => new
                    {
                        tid = t.Key,
                        tpl = a.TemplateId,
                        ass = a,
                    })
                )
            )
            .GroupBy(x => x.tpl, x => (x.tid, x.ass))
            .ToFrozenDictionary(
                g => g.Key,
                g =>
                    g.GroupBy(x => x.tid, x => x.ass)
                        .ToFrozenDictionary(k => k.Key, v => (IReadOnlyList<AssortSlim>)v.ToList())
            );

    private FrozenDictionary<
        MongoId,
        FrozenDictionary<MongoId, MongoId>
    > BuildTraderAssortParents() =>
        TraderSlimAssorts.ToFrozenDictionary(
            t => t.Key,
            t => traderUtils.GetAssortParentId(t.Value)
        );

    private FrozenDictionary<
        MongoId,
        IReadOnlyList<TraderBarterInfo>
    > BuildTemplateResultTraderBarter(IEnumerable<Trader> traders) =>
        traders
            .SelectMany(t =>
            {
                if (
                    !TraderSlimAssorts.TryGetValue(t.Base.Id, out var traderAssorts)
                    || !TraderAssortsParent.TryGetValue(t.Base.Id, out var parents)
                )
                {
                    return [];
                }

                return t
                    .Assort.BarterScheme.Where(bs => traderAssorts.ContainsKey(bs.Key))
                    .Select(bs =>
                    {
                        var assort = traderAssorts[bs.Key];

                        return new TraderBarterInfo(
                            AssortId: bs.Key,
                            TemplateId: assort.TemplateId,
                            AssortParentId: parents.GetValueOrDefault(bs.Key),
                            BarterScheme: new TraderBarterScheme(assort.LoyaltyLevel, bs.Value),
                            TraderId: t.Base.Id
                        );
                    });
            })
            .GroupBy(x => x.TemplateId)
            .ToFrozenDictionary(
                g => g.Key,
                g => (IReadOnlyList<TraderBarterInfo>)g.ToImmutableList()
            );

    private FrozenDictionary<
        MongoId,
        IReadOnlyList<TraderBarterInfo>
    > BuildTemplateResourceTraderBarter() =>
        TemplateResultTraderBarter
            .SelectMany(trtb =>
                trtb.Value.SelectMany(tbi =>
                    tbi.BarterScheme!.Barters.SelectMany(bl =>
                        bl.Select(b => (tpl: b.Template, btr: tbi))
                    )
                )
            )
            // Currency is a price, not a barter resource: skipping it keeps the
            // money items (and every money-only purchase) out of the index.
            .Where(x => !x.tpl.IsMoney())
            .GroupBy(x => x.tpl, x => x.btr)
            .ToFrozenDictionary(
                g => g.Key,
                g => (IReadOnlyList<TraderBarterInfo>)g.ToImmutableList()
            );

    private FrozenDictionary<
        MongoId,
        ImmutableList<KeyValuePair<MongoId, double>>
    > BuildAssortBarterTemplateCount() =>
        TemplateResourceTraderBarter
            .SelectMany(trtb =>
                trtb.Value.Where(tbi => tbi.BarterScheme?.Barters is { Count: > 0 })
                    .Select(tbi =>
                        (
                            aid: tbi.AssortId,
                            tpls: tbi.BarterScheme!.Barters.SelectMany(bl =>
                                bl.Select(b => new KeyValuePair<MongoId, double>(
                                        b.Template,
                                        b.Count ?? 1d
                                    ))
                                    .ToImmutableList()
                            )
                        )
                    )
            )
            .GroupBy(x => x.aid)
            .ToFrozenDictionary(k => k.Key, v => v.SelectMany(x => x.tpls).ToImmutableList());

    private FrozenDictionary<MongoId, FrozenDictionary<MongoId, double>> BuildBarterFleaPrice() =>
        TemplateResourceTraderBarter
            .Where(x =>
                x.Key != ItemTpl.MONEY_ROUBLES
                && x.Key != ItemTpl.MONEY_EUROS
                && x.Key != ItemTpl.MONEY_DOLLARS
                && x.Key != ItemTpl.MONEY_GP_COIN
            )
            .ToFrozenDictionary(
                x => x.Key,
                x =>
                    x.Value.SelectMany(bInfo =>
                            bInfo.BarterScheme!.Barters.Select(bList =>
                                (
                                    AssortId: bInfo.AssortId,
                                    Total: bList.Sum(b =>
                                        ragfairPriceService.GetFleaPriceForItem(b.Template)
                                        * (b.Count ?? 1d)
                                    )
                                )
                            )
                        )
                        .GroupBy(x => x.AssortId, x => x.Total)
                        .ToFrozenDictionary(g => g.Key, g => g.Min())
            ); // cheapest barter option if assort has multiple
}
