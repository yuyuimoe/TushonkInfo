using System.Collections.Concurrent;
using System.Text.Json;
using ItemInfo.Constants;
using ItemInfo.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums.Hideout;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Json;

namespace ItemInfo.Services;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.Database + 4)]
public class LocalizationService(
    ISptLogger<LocalizationService> logger,
    JsonUtil jsonUtil,
    LocaleService localeService,
    DatabaseService dbService
) : IOnLoad
{
    private static ModTranslation? AllLocales { get; set; }

    /// <summary>
    /// Game locale database per language, fetched once. Kept as a map because
    /// LocaleService.GetLocaleDb() may walk the lazy loaded locale lists.
    /// </summary>
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _localeDbs = new();

    public async Task OnLoad()
    {
        var translationFile = await jsonUtil.DeserializeFromFileAsync<ModTranslation>(
            ModPath.TranslationFile
        );
        if (translationFile is null)
        {
            var ex = new NullReferenceException("Could not find translation file");
            logger.Critical(
                "Failed to load translations file. Using translations keys instead",
                ex
            );
            return;
        }

        AllLocales = translationFile;

        foreach (
            var (lang, langdef) in translationFile.RawLanguages.Where(kvp => kvp.Key != "debug")
        )
        {
            Dictionary<string, string> deserializedLangDef =
                langdef.Deserialize<Dictionary<string, string>>() ?? new();
            AllLocales.Language.Add(lang, deserializedLangDef);
        }
    }

    /// <summary>
    /// Mod translation for a key: user locale first, then english, then the key itself.
    /// </summary>
    public bool TryGetKey(string key, out string localized)
    {
        if (AllLocales is null)
        {
            localized = key;
            return false;
        }

        var userLocale = GetUserLocale;
        if (
            AllLocales.Language.TryGetValue(userLocale, out var locale)
            && locale.TryGetValue(key, out var value)
        )
        {
            localized = value;
            return true;
        }

        if (
            AllLocales.Language.TryGetValue("en", out var english)
            && english.TryGetValue(key, out var englishValue)
        )
        {
            logger.Debug($"Missing key {key} for locale {userLocale}, using english");
            localized = englishValue;
            return true;
        }

        logger.Debug($"Missing key {key} for locale {userLocale} and english");
        localized = key;
        return false;
    }

    private static string GetUserLocale => ConfigurationService.Config.UserLocale;

    public string GetItemName(MongoId? tpl, string locale) =>
        tpl is null ? string.Empty : GetGameTranslation(tpl + " Name", locale);

    public string GetItemShortName(MongoId? tpl, string locale) =>
        tpl is null ? string.Empty : GetGameTranslation(tpl + " ShortName", locale);

    public string GetTraderNickname(MongoId? tpl, string locale) =>
        tpl is null ? string.Empty : GetGameTranslation(tpl + " Nickname", locale);

    public string GetQuestName(MongoId tpl, string locale) =>
        GetGameTranslation(tpl + " name", locale);

    public string GetHideoutName(HideoutAreas area, string locale) =>
        area == HideoutAreas.NotSet
            ? GetTranslation("UNKNOWN")
            : GetGameTranslation($"hideout_area_{area}_name", locale);

    public string GetGameTranslation(string key, string lang) =>
        GetLocaleDb(lang).GetValueOrDefault(key, key);

    private Dictionary<string, string> GetLocaleDb(string lang) =>
        _localeDbs.GetOrAdd(lang, l => localeService.GetLocaleDb(l));

    public string GetTranslation(string key) => GetTranslation(key, GetUserLocale);

    public string GetTranslation(string key, string lang)
    {
        /*
         * TODO: Remove this code. SPT has support for custom locales.
         * https://github.com/sp-tarkov/server-mod-examples/blob/main/25AddCustomLocales/AddCustomLocales.cs
         */

        if (AllLocales is null)
        {
            return key;
        }

        string? name;
        if (lang != "en" && AllLocales.Language.ContainsKey(lang))
        {
            if (!AllLocales.Language[lang].TryGetValue(key, out name))
            {
                logger.Debug(
                    $"Failed to find key {key} for locale {lang}. Falling back to english"
                );
                return GetTranslation(key, "en");
            }

            return name;
        }

        if (!AllLocales.Language["en"].TryGetValue(key, out name))
        {
            logger.Debug(
                "Failed to find key for locale in English. Falling back to debug information"
            );
            return "GetLocalization() unknown locale " + key;
        }

        return name;
    }

    /// <summary>
    /// Injects the generated texts into every game locale, so players see them
    /// regardless of the language their client requests (the text itself is built
    /// in <see cref="GetUserLocale"/>).
    /// </summary>
    public void UpsertLocaleTransformer(List<KeyValuePair<string, string>> newLocales)
    {
        if (newLocales.Count == 0)
        {
            return;
        }

        foreach (var (_, locales) in dbService.GetLocales().Global)
        {
            AppendLocales(locales, newLocales);
        }
    }

    public void UpsertLocaleTransformer(
        List<KeyValuePair<string, string>> newLocales,
        string locale
    )
    {
        if (dbService.GetLocales().Global.TryGetValue(locale, out var locales))
        {
            AppendLocales(locales, newLocales);
        }
    }

    private static void AppendLocales(
        LazyLoad<Dictionary<string, string>> locales,
        List<KeyValuePair<string, string>> newLocales
    ) =>
        locales.AddTransformer(l =>
        {
            foreach (var (k, v) in newLocales)
            {
                if (l.TryGetValue(k, out var existing))
                {
                    l[k] = v + "\n" + existing;
                }
                else
                {
                    l.Add(k, v);
                }
            }
            return l;
        });

    /// <summary>
    /// Appends a suffix to every "&lt;tpl&gt; &lt;keySuffix&gt;" entry ("Name", "ShortName", ...).
    /// Applied to every language (the suffix is language neutral), matching the
    /// previous behaviour.
    /// </summary>
    public void UpsertSuffixes(
        IReadOnlyDictionary<MongoId, string> suffixes,
        string keySuffix = "Name"
    )
    {
        if (suffixes.Count == 0)
        {
            return;
        }

        var globalLocales = dbService.GetLocales().Global;
        foreach (var (lang, locales) in globalLocales)
        {
            locales.AddTransformer(l =>
            {
                foreach (var (tpl, suffix) in suffixes)
                {
                    var key = $"{tpl} {keySuffix}";
                    if (l.TryGetValue(key, out var name))
                    {
                        l[key] = name + suffix;
                    }
                }
                return l;
            });
        }
    }
}
