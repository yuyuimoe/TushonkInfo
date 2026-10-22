using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;

namespace ItemInfo.Caches.Models;

/// <summary>
/// Armor material properties from globals.
/// </summary>
public record struct ArmorMaterialSlim(
    double Destructibility,
    double MinRepairDegradation,
    double MaxRepairDegradation
);

/// <summary>
/// Armor item stats. Only items that actually have an armor class and material.
/// </summary>
public record struct ArmorItemSlim(
    MongoId TemplateId,
    int ArmorClass,
    ArmorMaterial Material,
    double MaxDurability
);
