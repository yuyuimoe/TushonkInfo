using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace TushonkInfo.Extensions;

public static class ItemExtensions
{
    extension(TemplateItem item)
    {
        public int GetItemSlotDensity()
        {
            var (x, y) = item.GetItemSlotSize();

            return (x * y) / item.Properties?.StackMaxSize ?? -1;
        }

        public (int x, int y) GetItemSlotSize()
        {
            return item.Properties is null
                ? (1, 1)
                : (item.Properties.Width ?? 1, item.Properties.Height ?? 1);
        }

        public int GetItemInnerSlotCount()
        {
            var grids = item.Properties?.Grids?.ToArray();
            if (grids is null || !grids.Any())
            {
                return 0;
            }

            return grids.Sum(x => x.Properties?.CellsH * x.Properties?.CellsV ?? 0);
        }

        public bool IsMoney()
        {
            return item.Id == ItemTpl.MONEY_ROUBLES
                || item.Id == ItemTpl.MONEY_DOLLARS
                || item.Id == ItemTpl.MONEY_EUROS
                || item.Id == ItemTpl.MONEY_GP_COIN;
        }
    }
}
