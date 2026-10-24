using System.Collections.Concurrent;
using System.Text.Json;
using TushonkInfo.Constants;
using TushonkInfo.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums.Hideout;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Json;

namespace TushonkInfo.Services;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.Database + 4)]
public class LocalizationService(
    ISptLogger<LocalizationService> logger,
    JsonUtil jsonUtil,
    LocaleService localeService,
    DatabaseService dbService
) : IOnLoad
{
    /// <summary>
    /// Mod translations live in the game locale database under this prefix, so they
    /// cannot collide with game keys.
    /// </summary>
    private const string KeyPrefix = "TushonkInfo.";

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

        var languages = translationFile
            .RawLanguages.Where(kvp => kvp.Key != "debug")
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Deserialize<Dictionary<string, string>>() ?? new()
            );

        languages.TryGetValue("en", out var english);

        // https://github.com/sp-tarkov/server-mod-examples/blob/main/25AddCustomLocales/AddCustomLocales.cs
        foreach (var (lang, gameLocale) in dbService.GetLocales().Global)
        {
            // Missing keys fall back to english here, once, instead of on every lookup
            var translations = new Dictionary<string, string>();
            foreach (var source in new[] { english, languages.GetValueOrDefault(lang) })
            {
                foreach (var (key, value) in source ?? [])
                {
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        translations[KeyPrefix + key] = value;
                    }
                }
            }

            gameLocale.AddTransformer(l =>
            {
                foreach (var (key, value) in translations)
                {
                    l[key] = value;
                }
                return l;
            });
        }
    }

    /// <summary>
    /// Mod translation for a key: user locale first, then english.
    /// </summary>
    public bool TryGetKey(string key, out string localized)
    {
        var userLocale = GetUserLocale;
        var prefixedKey = KeyPrefix + key;

        if (GetLocaleDb(userLocale).TryGetValue(prefixedKey, out var value))
        {
            localized = value;
            return true;
        }

        if (userLocale != "en" && GetLocaleDb("en").TryGetValue(prefixedKey, out var englishValue))
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

    public string GetHideoutName(HideoutAreas area, string locale)
    {
        if (area == HideoutAreas.NotSet)
        {
            return GetTranslation("UNKNOWN");
        }

        // The game locale keys use the numeric area id (hideout_area_2_name),
        // not the enum name.
        var key = $"hideout_area_{(int)area}_name";
        var name = GetGameTranslation(key, locale);

        return name == key ? area.ToString() : name;
    }

    public string GetGameTranslation(string key, string lang) =>
        GetLocaleDb(lang).GetValueOrDefault(key, key);

    private Dictionary<string, string> GetLocaleDb(string lang) =>
        _localeDbs.GetOrAdd(lang, l => localeService.GetLocaleDb(l));

    public string GetTranslation(string key) => GetTranslation(key, GetUserLocale);

    public string GetTranslation(string key, string lang)
    {
        var prefixedKey = KeyPrefix + key;

        if (GetLocaleDb(lang).TryGetValue(prefixedKey, out var value))
        {
            return value;
        }

        if (lang != "en" && GetLocaleDb("en").TryGetValue(prefixedKey, out var englishValue))
        {
            logger.Debug($"Failed to find key {key} for locale {lang}. Falling back to english");
            return englishValue;
        }

        logger.Debug($"Failed to find key {key} for locale {lang} and english");
        return key;
    }

    /// <summary>
    /// Injects the generated texts into every game locale, so players see them
    /// regardless of the language their client requests (the text itself is built
    /// in <see cref="GetUserLocale"/>).
    /// </summary>
    public void UpsertLocaleTransformer(
        List<KeyValuePair<string, string>> newLocales,
        bool prepend = true
    )
    {
        if (newLocales.Count == 0)
        {
            return;
        }

        foreach (var (_, locales) in dbService.GetLocales().Global)
        {
            AppendLocales(locales, newLocales, prepend);
        }
    }

    public void UpsertLocaleTransformer(
        List<KeyValuePair<string, string>> newLocales,
        string locale,
        bool prepend = true
    )
    {
        if (dbService.GetLocales().Global.TryGetValue(locale, out var locales))
        {
            AppendLocales(locales, newLocales, prepend);
        }
    }

    private static void AppendLocales(
        LazyLoad<Dictionary<string, string>> locales,
        List<KeyValuePair<string, string>> newLocales,
        bool prepend
    ) =>
        locales.AddTransformer(l =>
        {
            foreach (var (k, v) in newLocales)
            {
                if (l.TryGetValue(k, out var existing))
                {
                    // Blank line between our block and the original text
                    l[k] = prepend ? v + "\n\n" + existing : existing + "\n\n" + v;
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
