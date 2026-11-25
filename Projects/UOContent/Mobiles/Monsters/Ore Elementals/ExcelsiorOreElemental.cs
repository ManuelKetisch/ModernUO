using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0)]
public sealed partial class ExcelsiorOreElemental : BaseCreature
{
    [SerializableField(0, setter: "private")]
    private CraftResource _resource;

    [Constructible]
    public ExcelsiorOreElemental(CraftResource resource = CraftResource.Blaze) : base(AIType.AI_Melee)
    {
        Resource = resource is >= CraftResource.Blaze and <= CraftResource.Platinum ? resource : CraftResource.Blaze;
        var info = CraftResources.GetInfo(Resource);
        var rank = (int)Resource - (int)CraftResource.Blaze;

        Body = 110;
        Hue = info.Hue;
        BaseSoundID = 268;
        SetStr(280 + rank * 30, 310 + rank * 30);
        SetDex(125 + rank * 15, 150 + rank * 15);
        SetInt(105 + rank * 10, 135 + rank * 10);
        SetHits(210 + rank * 35, 240 + rank * 35);
        SetDamage(14 + rank * 3, 20 + rank * 3);
        var attributes = info.AttributeInfo;
        SetDamageType(ResistanceType.Physical, attributes.WeaponFireDamage == 0 &&
            attributes.WeaponColdDamage == 0 && attributes.WeaponPoisonDamage == 0 && attributes.WeaponEnergyDamage == 0 ? 100 : 35);
        SetDamageType(ResistanceType.Fire, attributes.WeaponFireDamage * 65 / 100);
        SetDamageType(ResistanceType.Cold, attributes.WeaponColdDamage * 65 / 100);
        SetDamageType(ResistanceType.Poison, attributes.WeaponPoisonDamage * 65 / 100);
        SetDamageType(ResistanceType.Energy, attributes.WeaponEnergyDamage * 65 / 100);

        SetResistance(ResistanceType.Physical, 45 + rank * 6, 55 + rank * 6);
        SetResistance(ResistanceType.Fire, 35 + rank * 5, 45 + rank * 5);
        SetResistance(ResistanceType.Cold, 35 + rank * 5, 45 + rank * 5);
        SetResistance(ResistanceType.Poison, 35 + rank * 5, 45 + rank * 5);
        SetResistance(ResistanceType.Energy, 35 + rank * 5, 45 + rank * 5);
        SetSkill(SkillName.MagicResist, 75 + rank * 5, 90 + rank * 5);
        SetSkill(SkillName.Tactics, 80 + rank * 5, 95 + rank * 5);
        SetSkill(SkillName.Wrestling, 80 + rank * 5, 95 + rank * 5);
        Fame = 5600 + rank * 800;
        Karma = -Fame;
        VirtualArmor = 35 + rank * 6;

        var ore = info.ResourceTypes[1].CreateEntityInstance<BaseOre>(25);
        ore.ItemID = 0x19B9;
        PackItem(ore);
    }

    public override string DefaultName => $"a {CraftResources.GetName(Resource):L} ore elemental";
    public override string CorpseName => "an ore elemental corpse";
    public override bool BleedImmune => true;
    public override bool AutoDispel => true;

    public override void GenerateLoot() => AddLoot(LootPack.Average);
}
