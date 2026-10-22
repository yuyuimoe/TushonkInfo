using System.Buffers;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace TushonkInfo.Utils;

[Injectable]
public class RagfairUtils(ISptLogger<RagfairUtils> logger, RagfairPriceService ragfairPriceService)
{
    public double GetBarterTotalPrice(List<BarterScheme> barterScheme)
    {
        return barterScheme
            .Where(x =>
                x.Template != ItemTpl.MONEY_ROUBLES
                && x.Template != ItemTpl.MONEY_EUROS
                && x.Template != ItemTpl.MONEY_DOLLARS
                && x.Template != ItemTpl.MONEY_GP_COIN
            )
            .Sum(x => ragfairPriceService.GetFleaPriceForItem(x.Template) * x.Count ?? 1);
    }

    public double GetItemPrice(MongoId tpl)
    {
        return ragfairPriceService.GetFleaPriceForItem(tpl);
    }
}
