using System.Collections.Frozen;
using TushonkInfo.Caches.Models;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace TushonkInfo.Extensions;

public static class TraderExtensions
{
    extension(Trader trader)
    {
        public Item? GetAssortFromTemplate(MongoId templateId) =>
            trader.Assort.Items.Find(i => i.Template == templateId);

        public Item? GetAssortFromId(MongoId assortId) =>
            trader.Assort.Items.Find(i => i.Id == assortId);
    }
}
