using Server.Items;

namespace Server.Engines.BulkOrders;

internal static class BulkOrderMaterials
{
    private readonly record struct Grade(BulkMaterialType Material, CraftResource Resource, double Required);

    private static readonly Grade[] _metals =
    [
        new(BulkMaterialType.Blaze, CraftResource.Blaze, 100),
        new(BulkMaterialType.Ice, CraftResource.Ice, 105),
        new(BulkMaterialType.Toxic, CraftResource.Toxic, 110),
        new(BulkMaterialType.Electrum, CraftResource.Electrum, 115),
        new(BulkMaterialType.Platinum, CraftResource.Platinum, 119)
    ];

    private static readonly Grade[] _leathers =
    [
        new(BulkMaterialType.PolarLeather, CraftResource.PolarLeather, 100),
        new(BulkMaterialType.SyntheticLeather, CraftResource.SyntheticLeather, 102),
        new(BulkMaterialType.BlazeLeather, CraftResource.BlazeLeather, 105),
        new(BulkMaterialType.DaemonicLeather, CraftResource.DaemonicLeather, 109),
        new(BulkMaterialType.ShadowLeather, CraftResource.ShadowLeather, 112),
        new(BulkMaterialType.FrostLeather, CraftResource.FrostLeather, 115),
        new(BulkMaterialType.EtherealLeather, CraftResource.EtherealLeather, 119)
    ];

    private static readonly Grade[] _woods =
    [
        new(BulkMaterialType.Pine, CraftResource.PineWood, 20),
        new(BulkMaterialType.Mahogany, CraftResource.MahoganyWood, 40),
        new(BulkMaterialType.Zircote, CraftResource.ZircoteWood, 70),
        new(BulkMaterialType.Ebony, CraftResource.EbonyWood, 80),
        new(BulkMaterialType.Bamboo, CraftResource.BambooWood, 90),
        new(BulkMaterialType.PurpleHeart, CraftResource.PurpleHeartWood, 100),
        new(BulkMaterialType.Redwood, CraftResource.Redwood, 110),
        new(BulkMaterialType.Petrified, CraftResource.PetrifiedWood, 119)
    ];

    private static readonly Grade[][] _additional = [_metals, _leathers, _woods];

    internal static bool IsMetal(BulkMaterialType material) =>
        material is >= BulkMaterialType.DullCopper and <= BulkMaterialType.Valorite or
            >= BulkMaterialType.Blaze and <= BulkMaterialType.Platinum;

    internal static bool IsLeather(BulkMaterialType material) =>
        material is >= BulkMaterialType.Spined and <= BulkMaterialType.Barbed or
            >= BulkMaterialType.PolarLeather and <= BulkMaterialType.EtherealLeather;

    internal static bool IsWood(BulkMaterialType material) =>
        material is >= BulkMaterialType.NaturalWood and <= BulkMaterialType.Frostwood or
            >= BulkMaterialType.Pine and <= BulkMaterialType.Petrified;

    internal static bool IsAdditional(BulkMaterialType material) =>
        material is >= BulkMaterialType.Blaze and <= BulkMaterialType.Petrified;

    internal static string AdditionalName(BulkMaterialType material)
    {
        foreach (var group in _additional)
        {
            foreach (var grade in group)
            {
                if (grade.Material == material)
                {
                    return CraftResources.GetName(grade.Resource);
                }
            }
        }

        return null;
    }

    internal static BulkMaterialType ForAdditionalResource(CraftResource resource)
    {
        foreach (var group in _additional)
        {
            foreach (var grade in group)
            {
                if (grade.Resource == resource)
                {
                    return grade.Material;
                }
            }
        }

        return BulkMaterialType.None;
    }

    internal static BulkMaterialType SelectAdditional(Mobile player, BODType profession, BulkMaterialType fallback)
    {
        if ((profession is BODType.Smith or BODType.Tailor && !ExcelsiorOrderRules.Selected(player)) ||
            (WoodworkBulkOrders.IsWoodwork(profession) && !SoloProgression.AppliesTo(player)))
        {
            return fallback;
        }

        var (grades, skill, chance) = profession switch
        {
            BODType.Smith => (_metals, SkillName.Blacksmith, 0.2),
            BODType.Tailor => (_leathers, SkillName.Tailoring, 0.2),
            BODType.Carpentry => (_woods, SkillName.Carpentry, 0.35),
            _ => (_woods, SkillName.Fletching, 0.35)
        };

        var baseSkill = player.Skills[skill].Base;
        var count = 0;
        while (count < grades.Length && baseSkill >= grades[count].Required)
        {
            count++;
        }

        return count > 0 && Utility.RandomDouble() < chance
            ? grades[Utility.Random(count)].Material
            : fallback;
    }

    internal static T UpgradeLargeOffer<T>(Mobile player, T deed) where T : LargeBOD
    {
        if (deed.Material != BulkMaterialType.None)
        {
            deed.Material = SelectAdditional(player, deed.Profession, deed.Material);
        }

        return deed;
    }
}
