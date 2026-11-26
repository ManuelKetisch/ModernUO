using System;
using System.Collections.Generic;
using Server.Engines.Craft;

namespace Server.Items;

internal readonly record struct ExcelsiorMaterial(CraftResourceInfo Info, CraftResourceType Family, int Skill);

internal static class ExcelsiorMaterials
{
    internal static ExcelsiorMaterial[] CreateCatalog() =>
    [
        Metal(CraftResource.Blaze, "Blaze", 100, 0x489, 145, 60,
            (2, 7, 1, 1, 1), (100, 0, 0, 0), (4, 5, 65, 95),
            typeof(BlazeIngot), typeof(BlazeOre), typeof(BlazeGranite)),
        Metal(CraftResource.Ice, "Ice", 105, 0x480, 170, 70,
            (2, 1, 9, 1, 2), (0, 100, 0, 0), (4, 5, 70, 100),
            typeof(IceIngot), typeof(IceOre), typeof(IceGranite)),
        Metal(CraftResource.Toxic, "Toxic", 110, 0x5F1, 195, 80,
            (2, 1, 1, 7, 1), (0, 0, 100, 0), (5, 5, 75, 100),
            typeof(ToxicIngot), typeof(ToxicOre), typeof(ToxicGranite)),
        Metal(CraftResource.Electrum, "Electrum", 115, 0x4A4, 220, 90,
            (4, 3, 3, 2, 11), (0, 0, 0, 100), (5, 6, 80, 100),
            typeof(ElectrumIngot), typeof(ElectrumOre), typeof(ElectrumGranite)),
        Metal(CraftResource.Platinum, "Platinum", 119, 0x482, 270, 100,
            (10, 3, 3, 3, 2), (0, 0, 0, 0), (5, 6, 85, 100),
            typeof(PlatinumIngot), typeof(PlatinumOre), typeof(PlatinumGranite)),
        Hide(CraftResource.PolarLeather, "Polar", 100, 0x480, 120, 50,
            (1, 0, 3, 0, 0), (4, 5, 60, 90), typeof(PolarLeather), typeof(PolarHides)),
        Hide(CraftResource.SyntheticLeather, "Synthetic", 102, 0x4A4, 145, 60,
            (4, 2, 2, 2, 2), (4, 5, 65, 95), typeof(SyntheticLeather), typeof(SyntheticHides)),
        Hide(CraftResource.BlazeLeather, "Blaze", 105, 0x489, 145, 60,
            (1, 6, 0, 1, 0), (4, 5, 70, 95), typeof(BlazeLeather), typeof(BlazeHides)),
        Hide(CraftResource.DaemonicLeather, "Daemonic", 109, 0x66A, 170, 70,
            (2, 5, 1, 1, 2), (5, 5, 75, 100), typeof(DaemonicLeather), typeof(DaemonicHides)),
        Hide(CraftResource.ShadowLeather, "Shadow", 112, 0x455, 195, 80,
            (4, 1, 1, 1, 1), (5, 5, 80, 100), typeof(ShadowLeather), typeof(ShadowHides)),
        Hide(CraftResource.FrostLeather, "Frost", 115, 0x47E, 220, 90,
            (2, 1, 8, 1, 1), (5, 6, 85, 100), typeof(FrostLeather), typeof(FrostHides)),
        Hide(CraftResource.EtherealLeather, "Ethereal", 119, 0x48E, 270, 100,
            (4, 4, 4, 5, 7), (5, 6, 90, 100), typeof(EtherealLeather), typeof(EtherealHides)),
        Wood(CraftResource.PineWood, "Pine", 20, 0x715, 5,
            (0, 20, 0, 0), (1, 2, 25, 50), typeof(PineLog), typeof(PineBoard)),
        Wood(CraftResource.MahoganyWood, "Mahogany", 40, 0x650, 20,
            (0, 0, 10, 20), (2, 2, 35, 60), typeof(MahoganyLog), typeof(MahoganyBoard)),
        Wood(CraftResource.ZircoteWood, "Zircote", 70, 0x53C, 50,
            (25, 25, 25, 25), (2, 3, 45, 70), typeof(ZircoteLog), typeof(ZircoteBoard)),
        Wood(CraftResource.EbonyWood, "Ebony", 80, 0x455, 60,
            (0, 100, 0, 0), (3, 3, 50, 75), typeof(EbonyLog), typeof(EbonyBoard)),
        Wood(CraftResource.BambooWood, "Bamboo", 90, 0x598, 70,
            (0, 0, 0, 100), (3, 4, 55, 85), typeof(BambooLog), typeof(BambooBoard)),
        Wood(CraftResource.PurpleHeartWood, "Purple Heart", 100, 0x56B, 80,
            (100, 0, 0, 0), (4, 4, 65, 95), typeof(PurpleHeartLog), typeof(PurpleHeartBoard)),
        Wood(CraftResource.Redwood, "Redwood", 110, 0x66A, 90,
            (0, 0, 100, 0), (4, 5, 75, 100), typeof(RedwoodLog), typeof(RedwoodBoard)),
        Wood(CraftResource.PetrifiedWood, "Petrified", 119, 0x835, 100,
            (0, 0, 0, 0), (5, 6, 85, 100), typeof(PetrifiedLog), typeof(PetrifiedBoard))
    ];

    internal static IReadOnlyList<ExcelsiorMaterial> CraftingGrades => CraftResources.ExcelsiorGrades;

    internal static bool IsExtra(CraftResource resource) =>
        resource is >= CraftResource.Blaze and <= CraftResource.Platinum or
            >= CraftResource.PolarLeather and <= CraftResource.EtherealLeather or
            >= CraftResource.PineWood and <= CraftResource.PetrifiedWood;

    internal static int RequiredSkill(CraftResource resource)
    {
        foreach (var grade in CraftingGrades)
        {
            if (grade.Info.Resource == resource)
            {
                return grade.Skill;
            }
        }

        return 0;
    }

    internal static void AddCraftResources(CraftSystem craft, CraftResourceType family, int message, int form = 0)
    {
        foreach (var grade in CraftingGrades)
        {
            if (grade.Family == family && form < grade.Info.ResourceTypes.Length)
            {
                var info = grade.Info;
                craft.AddSubRes(info.ResourceTypes[form], info.Name, grade.Skill, message);
            }
        }
    }

    internal static ExcelsiorMaterial Metal(
        CraftResource resource, string name, int skill, int hue, int durability, int requirements,
        (int Physical, int Fire, int Cold, int Poison, int Energy) armor,
        (int Fire, int Cold, int Poison, int Energy) weapon,
        (int Min, int Max, int MinIntensity, int MaxIntensity) runic, params Type[] types
    )
    {
        var attributes = Runic(runic);
        SetArmor(attributes, armor, durability, 0);
        SetWeapon(attributes, weapon, requirements);
        return new ExcelsiorMaterial(new CraftResourceInfo(hue, 0, name, attributes, resource, types), CraftResourceType.Metal, skill);
    }

    internal static ExcelsiorMaterial Hide(
        CraftResource resource, string name, int skill, int hue, int durability, int requirements,
        (int Physical, int Fire, int Cold, int Poison, int Energy) armor,
        (int Min, int Max, int MinIntensity, int MaxIntensity) runic, params Type[] types
    )
    {
        var attributes = Runic(runic);
        SetArmor(attributes, armor, durability, requirements);
        return new ExcelsiorMaterial(new CraftResourceInfo(hue, 0, name, attributes, resource, types), CraftResourceType.Leather, skill);
    }

    internal static ExcelsiorMaterial Wood(
        CraftResource resource, string name, int skill, int hue, int requirements,
        (int Fire, int Cold, int Poison, int Energy) weapon,
        (int Min, int Max, int MinIntensity, int MaxIntensity) runic, params Type[] types
    )
    {
        var attributes = Runic(runic);
        var (armor, durability) = GetWoodArmorStats(resource);
        SetArmor(attributes, armor, durability, requirements);
        SetWeapon(attributes, weapon, requirements);
        return new ExcelsiorMaterial(new CraftResourceInfo(hue, 0, name, attributes, resource, types), CraftResourceType.Wood, skill);
    }

    private static ((int Physical, int Fire, int Cold, int Poison, int Energy) Resists, int Durability) GetWoodArmorStats(
        CraftResource resource
    ) =>
        resource switch
        {
            CraftResource.PineWood         => ((2, 1, 3, 1, 1), 50),
            CraftResource.MahoganyWood     => ((2, 2, 1, 2, 3), 65),
            CraftResource.ZircoteWood      => ((3, 3, 3, 3, 3), 90),
            CraftResource.EbonyWood        => ((3, 2, 6, 2, 3), 105),
            CraftResource.BambooWood       => ((3, 2, 3, 2, 7), 115),
            CraftResource.PurpleHeartWood  => ((3, 8, 2, 3, 3), 130),
            CraftResource.Redwood          => ((4, 3, 3, 8, 3), 150),
            CraftResource.PetrifiedWood    => ((8, 4, 4, 4, 4), 180),
            CraftResource.ExcelsiorAshWood => ((2, 4, 2, 1, 2), 70),
            CraftResource.ExcelsiorYewWood => ((3, 2, 4, 1, 3), 80),
            CraftResource.ExcelsiorOakWood => ((3, 2, 2, 4, 3), 90),
            _                              => ((2, 2, 2, 2, 2), 60)
        };

    private static CraftAttributeInfo Runic((int Min, int Max, int MinIntensity, int MaxIntensity) runic) =>
        new()
        {
            RunicMinAttributes = runic.Min,
            RunicMaxAttributes = runic.Max,
            RunicMinIntensity = runic.MinIntensity,
            RunicMaxIntensity = runic.MaxIntensity
        };

    private static void SetArmor(
        CraftAttributeInfo attributes, (int Physical, int Fire, int Cold, int Poison, int Energy) armor,
        int durability, int requirements
    )
    {
        attributes.ArmorPhysicalResist = armor.Physical;
        attributes.ArmorFireResist = armor.Fire;
        attributes.ArmorColdResist = armor.Cold;
        attributes.ArmorPoisonResist = armor.Poison;
        attributes.ArmorEnergyResist = armor.Energy;
        // The reference totals include the normal exceptional bonus.
        attributes.ArmorDurability = durability - 20;
        attributes.ArmorLowerRequirements = requirements;
    }

    private static void SetWeapon(
        CraftAttributeInfo attributes, (int Fire, int Cold, int Poison, int Energy) damage, int requirements
    )
    {
        attributes.WeaponFireDamage = damage.Fire;
        attributes.WeaponColdDamage = damage.Cold;
        attributes.WeaponPoisonDamage = damage.Poison;
        attributes.WeaponEnergyDamage = damage.Energy;
        attributes.WeaponLowerRequirements = requirements;
    }
}
