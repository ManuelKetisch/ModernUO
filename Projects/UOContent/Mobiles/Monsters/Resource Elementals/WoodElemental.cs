using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0)]
public partial class WoodElemental : BaseCreature
{
    [SerializableField(0, setter: "private")]
    private CraftResource _resource;

    [Constructible]
    public WoodElemental(CraftResource resource = CraftResource.OakWood) : base(AIType.AI_Melee)
    {
        Resource = CraftResources.GetType(resource) == CraftResourceType.Wood && resource != CraftResource.RegularWood
            ? resource
            : CraftResource.OakWood;

        var rank = Resource switch
        {
            CraftResource.PineWood        => 0,
            CraftResource.AshWood         => 1,
            CraftResource.MahoganyWood    => 2,
            CraftResource.YewWood         => 3,
            CraftResource.OakWood         => 4,
            CraftResource.ZircoteWood     => 5,
            CraftResource.EbonyWood       => 6,
            CraftResource.BambooWood      => 7,
            CraftResource.PurpleHeartWood => 8,
            CraftResource.Redwood         => 9,
            CraftResource.PetrifiedWood   => 10,
            CraftResource.Heartwood       => 5,
            CraftResource.Bloodwood       => 6,
            _                             => 7
        };

        Body = 301;
        Hue = CraftResources.GetHue(Resource);
        SetStr(170 + rank * 10, 220 + rank * 10);
        SetDex(120 + rank * 10, 140 + rank * 10);
        SetStam(120 + rank * 10, 140 + rank * 10);
        SetInt(70 + rank * 10, 80 + rank * 10);
        SetMana(70 + rank * 10, 80 + rank * 10);
        SetHits(120 + rank * 10, 140 + rank * 10);
        SetDamage(11 + rank * 2, 15 + rank * 2);

        // The reference damage entries sum above 100. Treat them as relative weights.
        var physicalWeight = 20 + rank * 5;
        var elementWeight = 15 + rank * 5;
        var elementShare = 100 * elementWeight / (physicalWeight + 4 * elementWeight);
        SetDamageType(ResistanceType.Physical, 100 - 4 * elementShare);
        SetDamageType(ResistanceType.Fire, elementShare);
        SetDamageType(ResistanceType.Cold, elementShare);
        SetDamageType(ResistanceType.Poison, elementShare);
        SetDamageType(ResistanceType.Energy, elementShare);

        SetResistance(ResistanceType.Physical, 20 + rank * 5, 30 + rank * 5);
        SetResistance(ResistanceType.Fire, 15 + rank * 5, 20 + rank * 5);
        SetResistance(ResistanceType.Cold, 15 + rank * 5, 20 + rank * 5);
        SetResistance(ResistanceType.Poison, 15 + rank * 5, 20 + rank * 5);
        SetResistance(ResistanceType.Energy, 15 + rank * 5, 20 + rank * 5);
        SetSkill(SkillName.MagicResist, 45 + rank * 5, 55 + rank * 5);
        SetSkill(SkillName.Tactics, 60 + rank * 5, 70 + rank * 5);
        SetSkill(SkillName.Wrestling, 60 + rank * 5, 70 + rank * 5);
        Fame = 750 + rank * 250;
        Karma = -Fame;
        VirtualArmor = 20 + rank * 3;

        PackItem(CraftResources.GetInfo(Resource).ResourceTypes[0].CreateEntityInstance<Item>(50));
        PackItem(new RunicFletcherTool(Resource, 5));
    }

    public override string DefaultName => Resource is CraftResource.OakWood or CraftResource.EbonyWood
        ? $"an {CraftResources.GetName(Resource).ToLowerInvariant()} elemental"
        : $"a {CraftResources.GetName(Resource).ToLowerInvariant()} elemental";
    public override string CorpseName => "a wood elemental corpse";
    public override bool BleedImmune => true;
    public override int GetIdleSound() => 443;
    public override int GetDeathSound() => 31;
    public override int GetAttackSound() => 672;
}
