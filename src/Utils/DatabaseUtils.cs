using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace TushonkInfo.Utils;

[Injectable]
public class DatabaseUtils(ISptLogger<DatabaseUtils> logger, DatabaseService dbService)
{
    public TemplateItem? GetItemFromTpl(MongoId tpl)
    {
        if (!dbService.GetItems().TryGetValue(tpl, out var item))
        {
            logger.Warning($"Could not get item instance for Tpl {tpl}");
        }

        return item;
    }

    public Trader? GetTraderFromId(MongoId traderId)
    {
        var trader = dbService.GetTrader(traderId);
        if (trader is null)
        {
            logger.Warning($"Could not get trader instance from Tpl {traderId}");
        }

        return trader;
    }
    
    public HandbookItem? GetHandbookItem(MongoId tpl)
    {
        return dbService.GetHandbook().Items.FirstOrDefault(i => i.Id == tpl);
    }
    
}