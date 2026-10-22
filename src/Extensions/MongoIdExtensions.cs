using SPTarkov.Server.Core.Models.Common;

namespace TushonkInfo.Extensions;

public static class MongoIdExtensions
{
    extension(MongoId templateId)
    {
        /// <summary>
        /// True for the currency templates (roubles, euros, dollars, GP coin).
        /// </summary>
        public bool IsMoney() =>
            templateId == ItemTpl.MONEY_ROUBLES
            || templateId == ItemTpl.MONEY_EUROS
            || templateId == ItemTpl.MONEY_DOLLARS
            || templateId == ItemTpl.MONEY_GP_COIN;
    }
}
