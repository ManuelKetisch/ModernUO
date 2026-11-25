using ModernUO.Serialization;
using Server.Engines.BioEngineering;
using Server.Engines.PetProgression;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.BulkOrders;

internal static class TamingBulkOrderRewards
{
    private static readonly RewardItem _leash = new(1, _ => new PetLeash());
    private static readonly RewardItem _brush = new(1, _ => new PetResurrectionBrush());
    private static readonly RewardItem _bond = new(1, _ => new PetBondingDeed());
    private static readonly RewardItem _tool = new(1, _ => new BioEngineeringTool(5));
    private static readonly RewardItem _book = new(1, _ => new BioEngineeringBook());
    private static readonly RewardItem _dreadHorn = new(1, _ => new MountedDreadHorn());
    private static readonly RewardItem _pixie = new(1, _ => new MountedPixieBlueDeed());
    private static readonly RewardItem _woodHorse = new(1, _ => MakeHorse("wooden horse statue", 0x973));
    private static readonly RewardItem _bardedHorse = new(1, _ => MakeHorse("barded horse decoration", 0x8AB));
    private static readonly RewardItem _smallLeather = new(1, _ =>
        Utility.RandomBool() ? new Leather(50) : new SpinedLeather(50));

    private static readonly RewardGroup _small10 = new(0, PetScroll(105), _brush, _leash, _woodHorse, _smallLeather);
    private static readonly RewardGroup _small15 = new(0, PetScroll(105), _leash, _woodHorse, _smallLeather);
    private static readonly RewardGroup _small20 = new(0, PlayerScroll(105), _leash, _bond, _tool, _smallLeather);

    private static readonly RewardGroup _three10 = new(0,
        Clothing(SkillName.Veterinary, 3), Clothing(SkillName.AnimalLore, 3), _bardedHorse, _bond, _tool,
        new RewardItem(1, _ => new SpinedLeather(Utility.RandomMinMax(300, 400))), Organics(1500, 2000));
    private static readonly RewardGroup _three15 = new(0,
        PetScroll(115), Clothing(SkillName.Veterinary, 3), Clothing(SkillName.AnimalLore, 3), _bardedHorse,
        new RewardItem(1, _ => new BarbedLeather(Utility.RandomMinMax(350, 450))), Organics(2000, 2500));
    private static readonly RewardGroup _three20 = new(0,
        PetScroll(115), Clothing(SkillName.Veterinary, 4), Clothing(SkillName.AnimalLore, 4), _dreadHorn,
        SpecialtyLeather(CraftResource.PolarLeather, 400, 500), Organics(2500, 3000));
    private static readonly RewardGroup _four15 = new(0,
        PlayerScroll(115), Clothing(SkillName.Veterinary, 4), Clothing(SkillName.AnimalLore, 4), _dreadHorn,
        SpecialtyLeather(CraftResource.SyntheticLeather, 450, 550), Organics(3000, 3500));
    private static readonly RewardGroup _four20 = new(0,
        PlayerScroll(115), Clothing(SkillName.Veterinary, 5), Clothing(SkillName.AnimalLore, 5), _pixie,
        SpecialtyLeather(CraftResource.BlazeLeather, 500, 600), Organics(3500, 4000));
    private static readonly RewardGroup _five15 = new(0,
        PetScroll(120), PlayerScroll(120), Clothing(SkillName.Veterinary, 5), Clothing(SkillName.AnimalLore, 5), _pixie,
        SpecialtyLeather(CraftResource.DaemonicLeather, 550, 650), Organics(4000, 4500));
    private static readonly RewardGroup _five20 = new(0,
        PetScroll(120), PlayerScroll(120), SpecialtyLeather(CraftResource.ShadowLeather, 600, 700), Organics(4500, 5000));
    private static readonly RewardGroup _six15 = new(0,
        PetScroll(130), _book, SpecialtyLeather(CraftResource.FrostLeather, 650, 750), Organics(5000, 5500));
    private static readonly RewardGroup _six20 = new(0,
        PetScroll(130), PlayerScroll(120), _book, SpecialtyLeather(CraftResource.EtherealLeather, 700, 800), Organics(5500, 6000));

    internal static RewardGroup GetSmall(int amount) => amount switch
    {
        20 => _small20,
        15 => _small15,
        _  => _small10
    };

    internal static RewardGroup GetLarge(int species, int amount) => (species, amount) switch
    {
        (>= 6, 20) => _six20,
        (>= 6, 15) => _six15,
        (>= 5, 20) or (>= 6, _) => _five20,
        (>= 5, 15) => _five15,
        (>= 4, 20) or (>= 5, _) => _four20,
        (>= 4, 15) => _four15,
        (_, 20) or (>= 4, _) => _three20,
        (_, 15) => _three15,
        _ => _three10
    };

    private static RewardItem PlayerScroll(int cap) => new(1, _ => new PowerScroll(
        Utility.RandomList(SkillName.AnimalTaming, SkillName.AnimalLore, SkillName.Veterinary), cap
    ));

    private static RewardItem PetScroll(int cap) => new(1, _ => new TamingPetPowerScroll(
        Utility.RandomList(SkillName.Wrestling, SkillName.Tactics, SkillName.Magery, SkillName.MagicResist,
            SkillName.Anatomy, SkillName.EvalInt), cap
    ));

    private static RewardItem Organics(int min, int max) =>
        new(1, _ => new BioOrganicMatter(Utility.RandomMinMax(min, max)));

    private static RewardItem SpecialtyLeather(CraftResource material, int min, int max) => new(1, _ =>
    {
        var type = CraftResources.GetInfo(material)?.ResourceTypes[0];
        var amount = Utility.RandomMinMax(min, max);
        if (type?.CreateEntityInstance<Item>() is { } leather)
        {
            leather.Amount = amount;
            return leather;
        }

        return new BarbedLeather(amount);
    });

    private static RewardItem Clothing(SkillName skill, int points) => new(1, _ =>
    {
        var shirt = new FancyShirt { Name = skill == SkillName.Veterinary ? "veterinary shirt" : "animal lore shirt" };
        shirt.SkillBonuses.SetValues(0, skill, points);
        return shirt;
    });

    private static Item MakeHorse(string name, int hue) => new Item(ShrinkTable.Lookup(0xCC)) { Name = name, Hue = hue };
}

[SerializationGenerator(0)]
public sealed partial class TamingPetPowerScroll : PetCareItem
{
    [SerializableField(0)]
    private SkillName _skillName;

    [SerializableField(1)]
    private int _cap;

    [Constructible(AccessLevel.GameMaster)]
    public TamingPetPowerScroll(SkillName skillName = SkillName.Wrestling, int cap = 105) : base(0x14F0)
    {
        SkillName = skillName;
        Cap = cap;
        Hue = 0x471;
    }

    public override string DefaultName => "pet power scroll";
    protected override string TargetPrompt => "Choose one of your nearby trained pets.";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"{SkillName} {"cap"}\t{Cap}");
    }

    protected override bool Apply(PlayerMobile player, BaseCreature pet)
    {
        if (!PetCare.CanReachOwnedPet(player, pet) || !pet.Alive || pet.IsDeadPet ||
            Cap is not (105 or 115 or 120 or 130) || pet.Skills[SkillName] is not { Base: > 0 } skill || skill.Cap >= Cap)
        {
            player.SendMessage("Choose an owned pet with this skill and a lower cap.");
            return false;
        }

        skill.Cap = Cap;
        TamingBulkOrders.ProtectSkillInvestment(pet);
        player.SendMessage($"{pet.Name}'s {SkillName} cap is now {Cap}.");
        return true;
    }
}
