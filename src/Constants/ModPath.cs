using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;

namespace ItemInfo.Constants;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.Database + 1)]
public class ModPath(ModHelper modHelper) : IOnLoad
{
    public static string ModFolder { get; private set; }
    public static string ConfigFile { get; private set; }
    public static string TiersFile { get; private set; }
    public static string TiersHexFile { get; private set; }
    public static string TranslationFile { get; private set; }
    public static string BsgBlackListFile { get; private set; }

    public Task OnLoad()
    {
        ModFolder = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        ConfigFile = Path.Combine(ModFolder, "config/config.json");
        TiersFile = Path.Combine(ModFolder, "config/tiers.json");
        TiersHexFile = Path.Combine(ModFolder, "config/tiers_hex.json");
        TranslationFile = Path.Combine(ModFolder, "config/translations.json");
        BsgBlackListFile = Path.Combine(ModFolder, "config/bsgblacklist.json");
        return Task.CompletedTask;
    }
}
