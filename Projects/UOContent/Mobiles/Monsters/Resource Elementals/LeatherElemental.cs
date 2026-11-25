using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0)]
public partial class LeatherElemental : BaseCreature
{
    [SerializableField(0, setter: "private")]
    private CraftResource _resource;

    [SerializableField(1, setter: "private")]
    private bool _followUp;

    [Constructible]
    public LeatherElemental(CraftResource resource = CraftResource.SpinedLeather, bool followUp = false)
        : base(AIType.AI_Melee)
    {
        Resource = CraftResources.GetType(resource) == CraftResourceType.Leather
            ? resource
            : CraftResource.SpinedLeather;
        FollowUp = followUp;

        var rank = CraftResources.GetIndex(Resource);
        Body = 112;
        BaseSoundID = 268;
        Hue = CraftResources.GetHue(Resource);
        SetStr(155 + rank * 13, 175 + rank * 13);
        SetDex(75 + rank * 12, 95 + rank * 12);
        SetInt(50 + rank * 8, 70 + rank * 8);
        SetHits(105 + rank * 13, 125 + rank * 13);
        SetDamage(9 + rank * 2, 13 + rank * 2);
        SetDamageType(ResistanceType.Physical, 70);
        SetDamageType(ResistanceType.Poison, 30);
        SetResistance(ResistanceType.Physical, 20 + rank * 5, 28 + rank * 5);
        SetResistance(ResistanceType.Fire, 15 + rank * 4, 24 + rank * 4);
        SetResistance(ResistanceType.Cold, 20 + rank * 4, 30 + rank * 4);
        SetResistance(ResistanceType.Poison, 22 + rank * 5, 30 + rank * 5);
        SetResistance(ResistanceType.Energy, 18 + rank * 4, 27 + rank * 4);
        SetSkill(SkillName.MagicResist, 40 + rank * 5, 50 + rank * 5);
        SetSkill(SkillName.Tactics, 55 + rank * 5, 65 + rank * 5);
        SetSkill(SkillName.Wrestling, 55 + rank * 5, 65 + rank * 5);
        Fame = 500 + rank * 200;
        Karma = -Fame;
        VirtualArmor = 16 + rank * 3;

        PackItem(new RunicSewingKit(Resource, 5));
    }

    public override int Hides => 50;
    public override HideType HideType => (HideType)CraftResources.GetIndex(Resource);
    public override string DefaultName => $"a {CraftResources.GetName(Resource).ToLowerInvariant()} leather elemental";
    public override string CorpseName => "a leather elemental corpse";
    public override bool BleedImmune => true;
}
