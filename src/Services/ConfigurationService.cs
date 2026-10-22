using TushonkInfo.Constants;
using TushonkInfo.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace TushonkInfo.Services;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.Database + 2)]
public class ConfigurationService(ISptLogger<ConfigurationService> logger, JsonUtil jsonUtil)
    : IOnLoad
{
    public static ModConfig Config { get; private set; }

    public static ModBsgBlackList BsgBlackList { get; private set; } = new();

    public async Task OnLoad()
    {
        var configFile = await jsonUtil.DeserializeFromFileAsync<ModConfig>(ModPath.ConfigFile);
        if (configFile is null)
        {
            logger.Critical(
                "Failed to load configuration file",
                new NullReferenceException("Deserialization of config file returned null")
            );
            return;
        }

        Config = configFile;

        if (!Config.UseBsgStaticFleaBanList.Enabled)
        {
            return;
        }

        var blackListFile = await jsonUtil.DeserializeFromFileAsync<ModBsgBlackList>(
            ModPath.BsgBlackListFile
        );
        if (blackListFile is null)
        {
            logger.Critical(
                "Failed to load bsgblacklist file, falling back to CanSellOnRagfair",
                new NullReferenceException("Deserialization of bsgblacklist file returned null")
            );
            return;
        }

        BsgBlackList = blackListFile;
    }
}
