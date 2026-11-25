using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using ModernUO.Serialization;
using Server.Engines.PetProgression;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Systems.FeatureFlags;

namespace Server.Engines.BulkOrders;

public sealed class TamingBulkOrders : GenericPersistence
{
    private static readonly TimeSpan _offerInterval = TimeSpan.FromHours(3);
    private static readonly TimeSpan _surrenderWindow = TimeSpan.FromDays(1);
    private static readonly Dictionary<PlayerMobile, DateTime> _offers = new();
    private static readonly Dictionary<BaseCreature, DateTime> _freshTames = new();

    private readonly record struct Species(Type Type, string Name, double MinSkill);

    private static readonly Species[][] _sets =
    [
        [Animal<Sheep>("sheep", 11.1), Animal<Goat>("goat", 11.1), Animal<Pig>("pig", 11.1)],
        [Animal<BlackBear>("black bear", 35.1), Animal<BrownBear>("brown bear", 41.1),
         Animal<TimberWolf>("timber wolf", 23.1), Animal<Cougar>("cougar", 41.1)],
        [Animal<GreyWolf>("grey wolf", 53.1), Animal<Panther>("panther", 53.1),
         Animal<GrizzlyBear>("grizzly bear", 59.1), Animal<PolarBear>("polar bear", 35.1),
         Animal<DireWolf>("dire wolf", 83.1)],
        [Animal<Drake>("drake", 84.3), Animal<Dragon>("dragon", 93.9), Animal<WhiteWyrm>("white wyrm", 96.3),
         Animal<SwampDragon>("swamp dragon", 93.9), Animal<Ridgeback>("ridgeback", 83.1),
         Animal<LesserHiryu>("lesser hiryu", 98.7)]
    ];

    private static readonly Species[] _smallOnly =
    [
        Animal<Llama>("llama", 35.1), Animal<Rabbit>("rabbit", 0), Animal<Hind>("hind", 23.1),
        Animal<GreatHart>("great hart", 59.1), Animal<Alligator>("alligator", 47.1)
    ];

    private static readonly Dictionary<Type, string> _speciesNames = CollectSpecies();

    private TamingBulkOrders() : base("TamingBulkOrders", 13)
    {
    }

    public static void Configure() => _ = new TamingBulkOrders();

    public static void Initialize() => Timer.StartTimer(TimeSpan.FromHours(1), TimeSpan.FromHours(1), Prune);

    [OnEvent(nameof(PlayerMobile.PlayerDeletedEvent))]
    public static void OnPlayerDeleted(PlayerMobile player) => _offers.Remove(player);

    private static Species Animal<T>(string name, double required) where T : BaseCreature =>
        new(typeof(T), name, required);

    private static Dictionary<Type, string> CollectSpecies()
    {
        var byType = new Dictionary<Type, string>();
        foreach (var set in _sets)
        {
            foreach (var species in set)
            {
                byType.Add(species.Type, species.Name);
            }
        }

        foreach (var species in _smallOnly)
        {
            byType.Add(species.Type, species.Name);
        }

        return byType;
    }

    internal static string AnimalName(Type type) =>
        type != null && _speciesNames.TryGetValue(type, out var name) ? name : "unknown creature";

    internal static bool Supports(Mobile from) => from is PlayerMobile && from.Skills.AnimalTaming.Base > 0;

    internal static TimeSpan Remaining(Mobile from) =>
        from is PlayerMobile player && _offers.TryGetValue(player, out var next) && next > Core.Now
            ? next - Core.Now
            : TimeSpan.Zero;

    internal static void Clear(Mobile from)
    {
        if (from is PlayerMobile player)
        {
            _offers.Remove(player);
        }
    }

    internal static BaseBOD CreateOffer(Mobile from, bool fromContextMenu)
    {
        if (!Supports(from) || Remaining(from) > TimeSpan.Zero ||
            (!fromContextMenu && Utility.RandomDouble() >= 0.2))
        {
            return null;
        }

        var skill = from.Skills.AnimalTaming.Base;
        var amount = Utility.RandomList(10, 15, 20);
        BaseBOD deed = null;

        if (skill >= 35.1 && Utility.RandomDouble() < (skill + 20) / 300.0)
        {
            var eligible = new List<Species[]>(_sets.Length);
            foreach (var set in _sets)
            {
                var fits = true;
                foreach (var species in set)
                {
                    if (species.MinSkill > skill)
                    {
                        fits = false;
                        break;
                    }
                }

                if (fits)
                {
                    eligible.Add(set);
                }
            }

            if (eligible.Count > 0)
            {
                var set = eligible.RandomElement();
                var entries = new LargeBulkEntry[set.Length];
                for (var i = 0; i < set.Length; i++)
                {
                    entries[i] = new LargeBulkEntry(null, new SmallBulkEntry(set[i].Type, 0, 0));
                }

                deed = new TamingLargeBOD(amount, entries);
            }
        }

        if (deed == null)
        {
            var eligible = new List<Species>();
            foreach (var set in _sets)
            {
                AddEligible(set, eligible, skill);
            }

            AddEligible(_smallOnly, eligible, skill);
            if (eligible.Count > 0)
            {
                deed = new TamingSmallBOD(0, amount, eligible.RandomElement().Type);
            }
        }

        if (deed != null)
        {
            _offers[(PlayerMobile)from] = Core.Now + _offerInterval;
        }

        return deed;
    }

    private static void AddEligible(Species[] set, List<Species> eligible, double skill)
    {
        foreach (var species in set)
        {
            if (species.MinSkill <= skill)
            {
                eligible.Add(species);
            }
        }
    }

    internal static void RecordFirstTame(BaseCreature creature, PlayerMobile player)
    {
        if (ContentFeatureFlags.BulkOrders && creature.ControlMaster == player &&
            _speciesNames.ContainsKey(creature.GetType()))
        {
            _freshTames[creature] = Core.Now + _surrenderWindow;
        }
    }

    internal static void ProtectSkillInvestment(BaseCreature pet) => _freshTames.Remove(pet);

    internal static bool CanSurrender(TamingSmallBOD deed, PlayerMobile owner, BaseCreature pet) =>
        ContentFeatureFlags.BulkOrders && !deed.Deleted && !deed.Complete &&
        deed.IsChildOf(owner.Backpack) && deed.GetSecureTradeCont() == null &&
        PetCare.CanReachOwnedPet(owner, pet) && pet.GetType() == deed.Type &&
        pet is { Alive: true, IsDeadPet: false, IsBonded: false, PetLevel: 1, PetGeneration: 1, Combatant: null } &&
        pet is not IMount { Rider: not null } && !PetLeash.HasCargo(pet) && pet.PetExperience == 0 &&
        pet.Owners.Count == 1 && pet.Owners[0] == owner &&
        _freshTames.TryGetValue(pet, out var until) && until > Core.Now;

    internal static bool TrySurrender(TamingSmallBOD deed, PlayerMobile owner, BaseCreature pet)
    {
        if (!CanSurrender(deed, owner, pet))
        {
            owner.SendMessage("Choose a matching animal you tamed within the last day. Keep bonded and trained pets.");
            return false;
        }

        _freshTames.Remove(pet);
        pet.Delete();
        deed.AmountCur++;
        owner.SendMessage("The animal has been surrendered for the order.");
        owner.SendGump(new SmallBODGump(deed));
        return true;
    }

    private static void Prune()
    {
        var expired = new List<BaseCreature>();
        foreach (var (creature, until) in _freshTames)
        {
            if (creature.Deleted || until <= Core.Now)
            {
                expired.Add(creature);
            }
        }

        foreach (var creature in expired)
        {
            _freshTames.Remove(creature);
        }
    }

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0);
        writer.WriteEncodedInt(_offers.Count);
        foreach (var (player, next) in _offers)
        {
            writer.Write(player);
            writer.Write(next);
        }

        writer.WriteEncodedInt(_freshTames.Count);
        foreach (var (creature, until) in _freshTames)
        {
            writer.Write(creature);
            writer.Write(until);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        _ = reader.ReadEncodedInt();
        var offerCount = reader.ReadEncodedInt();
        for (var i = 0; i < offerCount; i++)
        {
            var player = reader.ReadEntity<PlayerMobile>();
            var next = reader.ReadDateTime();
            if (player != null && next > Core.Now)
            {
                _offers[player] = next;
            }
        }

        var tameCount = reader.ReadEncodedInt();
        for (var i = 0; i < tameCount; i++)
        {
            var creature = reader.ReadEntity<BaseCreature>();
            var until = reader.ReadDateTime();
            if (creature != null && until > Core.Now)
            {
                _freshTames[creature] = until;
            }
        }
    }
}

[SerializationGenerator(0)]
public partial class TamingSmallBOD : SmallBOD
{
    [Constructible(AccessLevel.GameMaster)]
    public TamingSmallBOD() : this(0, 10, typeof(Sheep))
    {
    }

    internal TamingSmallBOD(int current, int amount, Type type)
        : base(0x471, current, amount, type, 0, 0, false, BulkMaterialType.None)
    {
    }

    public override BODType Profession => BODType.Taming;
    public override string DefaultName => "animal taming bulk order deed";
    public override int ComputeFame() => 15 * AmountMax;
    public override int ComputeGold() => 45 * AmountMax;
    public override RewardGroup GetRewardGroup() => TamingBulkOrderRewards.GetSmall(AmountMax);

    internal void ConfirmSurrender(PlayerMobile from, BaseCreature creature)
    {
        if (TamingBulkOrders.CanSurrender(this, from, creature))
        {
            from.SendGump(new TamingSurrenderGump(this, from, creature));
        }
        else
        {
            from.SendMessage("Choose a matching animal you tamed within the last day. Keep bonded and trained pets.");
        }
    }
}

[SerializationGenerator(0)]
public partial class TamingLargeBOD : LargeBOD
{
    [Constructible(AccessLevel.GameMaster)]
    public TamingLargeBOD() : this(10,
        [new(null, new SmallBulkEntry(typeof(Sheep), 0, 0)), new(null, new SmallBulkEntry(typeof(Goat), 0, 0)),
         new(null, new SmallBulkEntry(typeof(Pig), 0, 0))])
    {
    }

    internal TamingLargeBOD(int amount, LargeBulkEntry[] entries)
        : base(0x471, amount, false, BulkMaterialType.None, entries)
    {
        foreach (var entry in entries)
        {
            entry.Owner = this;
        }
    }

    public override BODType Profession => BODType.Taming;
    public override string DefaultName => "large animal taming bulk order deed";
    public override int ComputeFame() => 18 * AmountMax * Entries.Length;
    public override int ComputeGold() => 75 * AmountMax * Entries.Length;
    public override RewardGroup GetRewardGroup() => TamingBulkOrderRewards.GetLarge(Entries.Length, AmountMax);
}

internal sealed class TamingSurrenderGump(TamingSmallBOD deed, PlayerMobile owner, BaseCreature pet) : DynamicGump(45, 45)
{
    public override bool Singleton => true;

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 430, 135, 5054);
        builder.AddLabel(24, 24, 0x481, $"Surrender {pet.Name ?? TamingBulkOrders.AnimalName(pet.GetType())} for this order?");
        builder.AddLabel(24, 50, 0x481, "The trainer will take the animal permanently.");
        builder.AddButton(26, 86, 4005, 4007, 1);
        builder.AddLabel(60, 86, 0x481, "Surrender animal");
        builder.AddButton(241, 86, 4005, 4007, 0);
        builder.AddLabel(275, 86, 0x481, "Cancel");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile == owner && owner.NetState == sender && info.ButtonID == 1)
        {
            TamingBulkOrders.TrySurrender(deed, owner, pet);
        }
    }
}
