using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using ItemInfo.Caches.Models;
using ItemInfo.Services;
using SPTarkov.Common.Extensions;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Hideout;
using SPTarkov.Server.Core.Models.Enums.Hideout;
using SPTarkov.Server.Core.Models.Spt.Hideout;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace ItemInfo.Caches;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PostDBModLoader)]
public sealed class HideoutCache(ISptLogger<HideoutCache> logger, DatabaseService db) : IOnLoad
{
    public FrozenDictionary<HideoutAreas, HideoutArea> AreasByType { get; private set; }

    public FrozenDictionary<MongoId, HideoutAreas> AreaTypeById { get; private set; }

    public FrozenDictionary<MongoId, IReadOnlyList<HideoutAreaRequirement>> AreaRequirements
    {
        get;
        private set;
    }

    public FrozenDictionary<
        (MongoId, string),
        IReadOnlyList<StageRequirement>
    > AreaStageRequirements { get; private set; }

    public FrozenDictionary<
        MongoId,
        ImmutableList<StageRequirementSlim>
    > AreaStageRequirementsByTemplate { get; private set; }

    public FrozenDictionary<
        (MongoId, MongoId),
        IReadOnlyList<StageImprovementRequirement>
    > AreaStageImprovementRequirement { get; private set; }

    public FrozenDictionary<
        MongoId,
        ImmutableList<StageImprovementRequirementSlim>
    > AreaStageImprovementRequirementByTemplate { get; private set; }

    public FrozenDictionary<MongoId, HideoutProductionData> ProductionsByRequiredItem
    {
        get;
        private set;
    }

    public FrozenDictionary<MongoId, HideoutProductionData> ProductionsByRewardItem
    {
        get;
        private set;
    }
    public double GpuBoostRate { get; private set; }

    public async Task OnLoad()
    {
        await Task.Run(() =>
        {
            var watch = new Stopwatch();
            watch.Start();
            var hideout = db.GetHideout();
            var hideoutProd = hideout.Production;

            AreasByType = BuildAreasByType(hideout);
            AreaTypeById = BuildAreaTypeById();
            AreaRequirements = BuildAreaRequirements();
            AreaStageRequirements = BuildStageRequirements();
            AreaStageRequirementsByTemplate = BuildStageRequirementsByTemplate();
            AreaStageImprovementRequirement = BuildStageImprovementRequirements();
            AreaStageImprovementRequirementByTemplate =
                BuildStageImprovementRequirementByTemplate();
            ProductionsByRequiredItem = BuildProductionByRequiredItem(hideoutProd);
            ProductionsByRewardItem = BuildProductionByRewardItem(hideoutProd);
            GpuBoostRate = BuildGpuBoostRate(hideout);

            watch.Stop();
            logger.Info($"Hideout Cache loaded in {watch.ElapsedMilliseconds}ms");
        });
    }

    private FrozenDictionary<HideoutAreas, HideoutArea> BuildAreasByType(Hideout hideout)
    {
        var areas = hideout.Areas.Where(x => x.Type != HideoutAreas.ChristmasIllumination);
        if (hideout.CustomAreas is { Count: > 0 })
        {
            areas = areas.Concat(hideout.CustomAreas);
        }

        return areas.ToFrozenDictionary(k => k.Type ?? HideoutAreas.NotSet, v => v);
    }

    private FrozenDictionary<MongoId, HideoutAreas> BuildAreaTypeById() =>
        AreasByType
            .Select(abt => (id: abt.Value.Id, type: abt.Key))
            .ToFrozenDictionary(k => k.id, v => v.type);

    private FrozenDictionary<
        MongoId,
        IReadOnlyList<HideoutAreaRequirement>
    > BuildAreaRequirements() =>
        AreasByType
            .Where(x => x.Value.Requirements is { Count: > 0 })
            .ToFrozenDictionary(
                k => k.Value.Id,
                IReadOnlyList<HideoutAreaRequirement> (v) => v.Value.Requirements!
            );

    private FrozenDictionary<
        (MongoId, string),
        IReadOnlyList<StageRequirement>
    > BuildStageRequirements() =>
        AreasByType
            .Where(a => a.Value.Stages is { Count: > 0 })
            .SelectMany(a =>
                a.Value.Stages!.Where(s => s.Value.Requirements is { Count: > 0 })
                    .Select(s =>
                        (
                            Key: (a.Value.Id, s.Key),
                            Value: (IReadOnlyList<StageRequirement>)
                                s.Value.Requirements!.Where(r => r.Type == "Item").ToImmutableList()
                        )
                    )
            )
            .ToFrozenDictionary(x => x.Key, x => x.Value);

    private FrozenDictionary<
        MongoId,
        ImmutableList<StageRequirementSlim>
    > BuildStageRequirementsByTemplate() =>
        AreaStageRequirements
            .SelectMany(asr =>
                asr.Value.Select(sr =>
                    (
                        tpl: sr.TemplateId,
                        area: new StageRequirementSlim(
                            asr.Key.Item1,
                            // Stage requirements do not carry their area type,
                            // resolve it from the area id of the parent stage.
                            AreaTypeById.GetValueOrDefault(asr.Key.Item1, HideoutAreas.NotSet),
                            asr.Key.Item2,
                            sr.TemplateId,
                            sr.Count ?? 1
                        )
                    )
                )
            )
            .GroupBy(x => x.tpl, x => x.area)
            .ToFrozenDictionary(g => g.Key, g => g.ToImmutableList());

    private FrozenDictionary<
        (MongoId, MongoId),
        IReadOnlyList<StageImprovementRequirement>
    > BuildStageImprovementRequirements() =>
        AreasByType
            .Where(a => a.Value.Stages is { Count: > 0 })
            .SelectMany(a =>
                a.Value.Stages!.Where(s => s.Value.Improvements is { Count: > 0 })
                    .SelectMany(s =>
                        s.Value.Improvements!.Where(i => i.Requirements is { Count: > 0 })
                            .Select(i =>
                                (
                                    Key: (a.Value.Id, i.Id),
                                    Value: (IReadOnlyList<StageImprovementRequirement>)
                                        i.Requirements!.Where(r => r.Type == "Item").ToList()
                                )
                            )
                    )
            )
            .ToFrozenDictionary(x => x.Key, x => x.Value);

    private FrozenDictionary<
        MongoId,
        ImmutableList<StageImprovementRequirementSlim>
    > BuildStageImprovementRequirementByTemplate() =>
        AreaStageImprovementRequirement
            .SelectMany(asir =>
                asir.Value.Select(air =>
                    (
                        tpl: air.TemplateId,
                        stage: new StageImprovementRequirementSlim(
                            asir.Key.Item1,
                            asir.Key.Item2,
                            air.TemplateId,
                            air.Count ?? 1,
                            air.IsSpawnedInSession ?? false
                        )
                    )
                )
            )
            .GroupBy(x => x.tpl, x => x.stage)
            .ToFrozenDictionary(g => g.Key, g => g.ToImmutableList());

    private FrozenDictionary<MongoId, HideoutProductionData> BuildProductionByRequiredItem(
        HideoutProductionData prod
    )
    {
        var hideoutRecipes =
            prod.Recipes?.Where(r => r.Requirements is { Count: > 0 })
                .SelectMany(r =>
                    r.Requirements!.Where(re => re.Type is "Item" or "Tool")
                        .Select(re => (k: re.TemplateId ?? MongoId.Empty(), v: r))
                )
                .GroupBy(x => x.k)
                .ToDictionary(g => g.Key, g => g.Select(x => x.v).ToList())
            ?? [];

        var scavCaseRecipes =
            prod.ScavRecipes?.Where(r => r.Requirements is { Count: > 0 })
                .SelectMany(r =>
                    r.Requirements!.Where(re => re.Type is "Item")
                        .Select(re => (k: re.TemplateId ?? MongoId.Empty(), v: r))
                )
                .GroupBy(x => x.k)
                .ToDictionary(g => g.Key, g => g.Select(x => x.v).ToList())
            ?? [];

        return hideoutRecipes
            .Keys.Union(scavCaseRecipes.Keys)
            .ToFrozenDictionary(
                k => k,
                k => new HideoutProductionData
                {
                    Recipes = hideoutRecipes.GetValueOrDefault(k),
                    ScavRecipes = scavCaseRecipes.GetValueOrDefault(k),
                }
            );
    }

    private FrozenDictionary<MongoId, HideoutProductionData> BuildProductionByRewardItem(
        HideoutProductionData prod
    ) =>
        prod.Recipes!.Select(r => (key: r.EndProduct, value: r))
            .GroupBy(x => x.key)
            .ToFrozenDictionary(
                k => k.Key,
                k => new HideoutProductionData() { Recipes = k.Select(x => x.value).ToList() }
            );

    private double BuildGpuBoostRate(Hideout hideout) => hideout.Settings.GpuBoostRate ?? 0.041225;
}
