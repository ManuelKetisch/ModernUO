using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using ModernUO.Serialization;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.BulkOrders;

public sealed class WoodworkBulkOrders : GenericPersistence
{
    private static readonly TimeSpan _offerInterval = TimeSpan.FromHours(2);
    private static readonly Dictionary<(PlayerMobile Player, BODType Profession), DateTime> _next = new();

    private static readonly SmallBulkEntry[] _furniture =
    [
        Entry<FootStool>(0xB5E), Entry<Stool>(0xA2A), new(typeof(WoodenChair), 1044301, 0xB56)
    ];

    private static readonly SmallBulkEntry[] _carpentryGear =
    [
        Entry<WoodenShield>(0x1B7A), Entry<QuarterStaff>(0xE89),
        Entry<GnarledStaff>(0x13F8), Entry<ShepherdsCrook>(0xE81)
    ];

    private static readonly SmallBulkEntry[] _bows =
    [
        Entry<Bow>(0x13B2), Entry<CompositeBow>(0x26C2), Entry<Yumi>(0x27A5)
    ];

    private static readonly SmallBulkEntry[] _crossbows =
    [
        Entry<Crossbow>(0xF50), Entry<HeavyCrossbow>(0x13FD), Entry<RepeatingCrossbow>(0x26C3)
    ];

    private static readonly SmallBulkEntry[] _carpentryArsenal =
    [
        .._carpentryGear, new(typeof(Bokuto), 1030227, 0x27A8), new(typeof(Tetsubo), 1030225, 0x27A6)
    ];

    private static readonly SmallBulkEntry[] _allRanged = [.._bows, .._crossbows];
    private static readonly SmallBulkEntry[][] _carpentry = [_furniture, _carpentryGear, _carpentryArsenal];
    private static readonly SmallBulkEntry[][] _fletching = [_bows, _crossbows, _allRanged];

    private WoodworkBulkOrders() : base("WoodworkBulkOrders", 12)
    {
    }

    public static void Configure() => _ = new WoodworkBulkOrders();

    internal static bool IsWoodwork(BODType type) => type is BODType.Carpentry or BODType.Fletching;

    internal static SkillName SkillFor(BODType type) =>
        type == BODType.Carpentry ? SkillName.Carpentry : SkillName.Fletching;

    internal static bool Supports(Mobile from, BODType type) =>
        from is PlayerMobile && IsWoodwork(type) && from.Skills[SkillFor(type)].Base > 0;

    internal static TimeSpan Remaining(Mobile from, BODType type) =>
        from is PlayerMobile player && _next.TryGetValue((player, type), out var next) && next > Core.Now
            ? next - Core.Now
            : TimeSpan.Zero;

    internal static void Clear(Mobile from, BODType type)
    {
        if (from is PlayerMobile player)
        {
            _next.Remove((player, type));
        }
    }

    [OnEvent(nameof(PlayerMobile.PlayerDeletedEvent))]
    public static void OnPlayerDeleted(PlayerMobile player)
    {
        Clear(player, BODType.Carpentry);
        Clear(player, BODType.Fletching);
    }

    internal static BaseBOD CreateOffer(Mobile from, BODType type, bool fromContextMenu)
    {
        if (!Supports(from, type) || Remaining(from, type) > TimeSpan.Zero ||
            (!fromContextMenu && Utility.RandomDouble() >= 0.2))
        {
            return null;
        }

        var skill = from.Skills[SkillFor(type)].Base;
        var system = type == BODType.Carpentry ? DefCarpentry.CraftSystem : DefBowFletching.CraftSystem;
        var sets = type == BODType.Carpentry ? _carpentry : _fletching;
        var amount = Utility.RandomList(10, 15, 20);
        var exceptional = skill >= 70.1 && Utility.RandomDouble() < (skill + 80.0) / 200.0;
        var material = BulkOrderMaterials.SelectAdditional(from, type, SelectWood(skill));
        BaseBOD order = null;

        if (skill >= 70.1 && Utility.RandomDouble() < (skill - 40.0) / 300.0)
        {
            var validSets = new List<SmallBulkEntry[]>(sets.Length);
            foreach (var set in sets)
            {
                if (CanOfferSet(set, from, system, exceptional))
                {
                    validSets.Add(set);
                }
            }

            if (validSets.Count > 0)
            {
                var set = validSets.RandomElement();
                order = new WoodworkLargeBOD(
                    type, amount, exceptional, set == _furniture ? BulkMaterialType.None : material,
                    LargeBulkEntry.ConvertEntries(null, set)
                );
            }
        }

        if (order == null)
        {
            var eligible = GetEligibleEntries(sets, from, system, exceptional);
            if (eligible.Count == 0 && exceptional)
            {
                exceptional = false;
                eligible = GetEligibleEntries(sets, from, system, false);
            }

            if (eligible.Count > 0)
            {
                var entry = eligible.RandomElement();
                order = new WoodworkSmallBOD(
                    type, 0, amount, entry.Type, entry.Number, entry.Graphic, exceptional,
                    IsFurniture(entry.Type) ? BulkMaterialType.None : material
                );
            }
        }

        if (order != null)
        {
            _next[((PlayerMobile)from, type)] = Core.Now + _offerInterval;
        }

        return order;
    }

    internal static bool Accepts(Item item, BODType type) => item is BaseBOD deed && deed.Profession == type;

    internal static bool IsFurniture(Type type) =>
        type == typeof(FootStool) || type == typeof(Stool) || type == typeof(WoodenChair);

    private static List<SmallBulkEntry> GetEligibleEntries(
        SmallBulkEntry[][] sets, Mobile from, CraftSystem system, bool exceptional
    )
    {
        var eligible = new List<SmallBulkEntry>();
        foreach (var set in sets)
        {
            foreach (var entry in set)
            {
                if (!eligible.Contains(entry) && CanOffer(entry, from, system, exceptional))
                {
                    eligible.Add(entry);
                }
            }
        }

        return eligible;
    }

    private static bool CanOfferSet(SmallBulkEntry[] entries, Mobile from, CraftSystem system, bool exceptional)
    {
        foreach (var entry in entries)
        {
            if (!CanOffer(entry, from, system, exceptional))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanOffer(SmallBulkEntry entry, Mobile from, CraftSystem system, bool exceptional)
    {
        if ((exceptional && IsFurniture(entry.Type)) || system.CraftItems.SearchFor(entry.Type) is not { } recipe)
        {
            return false;
        }

        var chance = recipe.GetSuccessChance(from, null, system, false, out var skills);
        return skills && chance > 0 && (!exceptional || recipe.GetExceptionalChance(system, chance, from) > 0);
    }

    private static BulkMaterialType SelectWood(double skill)
    {
        var maximum = skill switch
        {
            >= 100 => BulkMaterialType.Frostwood,
            >= 95  => BulkMaterialType.Yew,
            >= 80  => BulkMaterialType.Ash,
            >= 65  => BulkMaterialType.Oak,
            _      => BulkMaterialType.NaturalWood
        };

        return Utility.RandomDouble() < 0.5
            ? BulkMaterialType.NaturalWood
            : (BulkMaterialType)Utility.RandomMinMax((int)BulkMaterialType.NaturalWood, (int)maximum);
    }

    private static SmallBulkEntry Entry<T>(int graphic) where T : Item =>
        new(typeof(T), graphic < 0x4000 ? 1020000 + graphic : 1078872 + graphic, graphic);

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0);
        writer.WriteEncodedInt(_next.Count);
        foreach (var ((player, profession), next) in _next)
        {
            writer.Write(player);
            writer.Write((byte)profession);
            writer.Write(next);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        _ = reader.ReadEncodedInt();
        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; ++i)
        {
            var player = reader.ReadEntity<PlayerMobile>();
            var profession = (BODType)reader.ReadByte();
            var next = reader.ReadDateTime();
            if (player != null && IsWoodwork(profession) && next > Core.Now)
            {
                _next[(player, profession)] = next;
            }
        }
    }
}

[SerializationGenerator(0)]
public partial class WoodworkSmallBOD : SmallBOD
{
    [SerializableField(0, getter: "private", setter: "private")]
    private BODType _orderProfession;

    public override BODType Profession => _orderProfession;
    public override string DefaultName => Profession == BODType.Carpentry ? "carpentry bulk order deed" : "fletching bulk order deed";

    [Constructible(AccessLevel.GameMaster)]
    public WoodworkSmallBOD() : this(BODType.Carpentry, 0, 10, typeof(Stool), 1022602, 0xA2A, false, BulkMaterialType.None)
    {
    }

    internal WoodworkSmallBOD(
        BODType profession, int amountCur, int amountMax, Type type, int number, int graphic, bool exceptional,
        BulkMaterialType material
    ) : base(profession == BODType.Carpentry ? 0x59A : 0x5D2, amountCur, amountMax, type, number, graphic, exceptional, material)
    {
        _orderProfession = profession;
    }

    public override int ComputeFame() => WoodworkRewardCalculator.For(Profession).ComputeFame(this);
    public override int ComputeGold() => WoodworkRewardCalculator.For(Profession).ComputeGold(this);
    public override RewardGroup GetRewardGroup() => WoodworkRewardCalculator.For(Profession).GetRewards(this);
}

[SerializationGenerator(0)]
public partial class WoodworkLargeBOD : LargeBOD
{
    [SerializableField(0, getter: "private", setter: "private")]
    private BODType _orderProfession;

    public override BODType Profession => _orderProfession;
    public override string DefaultName => Profession == BODType.Carpentry ? "large carpentry bulk order deed" : "large fletching bulk order deed";

    [Constructible(AccessLevel.GameMaster)]
    public WoodworkLargeBOD() : this(
        BODType.Carpentry, 10, false, BulkMaterialType.None,
        [new(null, new SmallBulkEntry(typeof(Stool), 1022602, 0xA2A)),
         new(null, new SmallBulkEntry(typeof(FootStool), 1022910, 0xB5E))]
    )
    {
    }

    internal WoodworkLargeBOD(BODType profession, int amount, bool exceptional, BulkMaterialType material, LargeBulkEntry[] entries)
        : base(profession == BODType.Carpentry ? 0x59A : 0x5D2, amount, exceptional, material, entries)
    {
        _orderProfession = profession;
        foreach (var entry in entries)
        {
            entry.Owner = this;
        }
    }

    public override int ComputeFame() => WoodworkRewardCalculator.For(Profession).ComputeFame(this);
    public override int ComputeGold() => WoodworkRewardCalculator.For(Profession).ComputeGold(this);
    public override RewardGroup GetRewardGroup() => WoodworkRewardCalculator.For(Profession).GetRewards(this);
}
