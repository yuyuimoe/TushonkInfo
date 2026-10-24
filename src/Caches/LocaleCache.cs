using System.Collections.Frozen;
using System.Diagnostics;
using TushonkInfo.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums.Hideout;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace TushonkInfo.Caches;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.RagfairCallbacks + 65537)]
public sealed class LocaleCache(
    ISptLogger<LocaleCache> logger,
    DatabaseService db,
    LocalizationService localizationService
) : IOnLoad
{
    public FrozenDictionary<string, FrozenDictionary<MongoId, string>> TemplateShortNames
    {
        get;
        private set;
    }
    public FrozenDictionary<string, FrozenDictionary<MongoId, string>> TraderNicknames
    {
        get;
        private set;
    }
    public FrozenDictionary<string, FrozenDictionary<MongoId, string>> QuestNames
    {
        get;
        private set;
    }

    public FrozenDictionary<string, FrozenDictionary<HideoutAreas, string>> HideoutAreaNames;

    public async Task OnLoad()
    {
        await Task.Run(() =>
        {
            var watch = new Stopwatch();
            watch.Start();

            var languages = db.GetLocales()
                .Languages.Where(l =>
                    l.Key == ConfigurationService.Config.UserLocale || l.Key == "en"
                )
                .ToFrozenDictionary();
            var itemKeySet = db.GetItems().Keys.ToFrozenSet();
            var traderKeySet = db.GetTraders().Keys.ToFrozenSet();
            var questKeySet = db.GetQuests().Keys.ToFrozenSet();

            TemplateShortNames = BuildTemplateShortNames(languages, itemKeySet);
            TraderNicknames = BuildTraderNicknames(languages, traderKeySet);
            QuestNames = BuildQuestNames(languages, questKeySet);
            HideoutAreaNames = BuildHideoutAreaNames(languages);

            watch.Stop();
            logger.Info($"Locale Cache loaded in {watch.ElapsedMilliseconds}ms");
        });
    }

    private FrozenDictionary<string, FrozenDictionary<MongoId, string>> BuildTemplateShortNames(
        FrozenDictionary<string, string> locales,
        FrozenSet<MongoId> itemKeySet
    ) =>
        locales.ToFrozenDictionary(
            k => k.Key,
            l =>
                itemKeySet.ToFrozenDictionary(
                    k => k,
                    v => localizationService.GetItemShortName(v, l.Key)
                )
        );

    private FrozenDictionary<string, FrozenDictionary<MongoId, string>> BuildTraderNicknames(
        FrozenDictionary<string, string> locales,
        FrozenSet<MongoId> traderKeySet
    ) =>
        locales.ToFrozenDictionary(
            k => k.Key,
            l =>
                traderKeySet.ToFrozenDictionary(
                    k => k,
                    v => localizationService.GetTraderNickname(v, l.Key)
                )
        );

    private FrozenDictionary<string, FrozenDictionary<MongoId, string>> BuildQuestNames(
        FrozenDictionary<string, string> locales,
        FrozenSet<MongoId> questKeySet
    ) =>
        locales.ToFrozenDictionary(
            k => k.Key,
            l =>
                questKeySet.ToFrozenDictionary(
                    k => k,
                    v => localizationService.GetQuestName(v, l.Key)
                )
        );

    private FrozenDictionary<string, FrozenDictionary<HideoutAreas, string>> BuildHideoutAreaNames(
        FrozenDictionary<string, string> locales
    ) =>
        locales.ToFrozenDictionary(
            k => k.Key,
            l =>
                Enum.GetValues<HideoutAreas>()
                    .ToFrozenDictionary(a => a, a => localizationService.GetHideoutName(a, l.Key))
        );
}
