using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Systems.FeatureFlags;

namespace Server.Engines.BulkOrders;

public sealed class ExcelsiorOrderRules : GenericPersistence
{
    private static readonly HashSet<PlayerMobile> _selected = new();
    private static readonly TimeSpan _offerInterval = TimeSpan.FromHours(2);

    private ExcelsiorOrderRules() : base("ExcelsiorOrderRules", 14)
    {
    }

    public static void Configure()
    {
        _ = new ExcelsiorOrderRules();
        CommandSystem.Register("BODRules", AccessLevel.Player, args =>
        {
            if (ContentFeatureFlags.BulkOrders && args.Mobile is PlayerMobile { Alive: true } player)
            {
                player.SendGump(new RulesGump(player));
            }
        });
    }

    [OnEvent(nameof(PlayerMobile.PlayerDeletedEvent))]
    public static void OnPlayerDeleted(PlayerMobile player) => _selected.Remove(player);

    internal static bool Selected(Mobile from) => from is PlayerMobile player && _selected.Contains(player);

    internal static void Select(PlayerMobile player, bool excelsior)
    {
        if (excelsior)
        {
            _selected.Add(player);
        }
        else
        {
            _selected.Remove(player);
        }
    }

    internal static BaseBOD Offer(PlayerMobile player, BODType profession)
    {
        var smith = profession == BODType.Smith;
        var skill = player.Skills[smith ? SkillName.Blacksmith : SkillName.Tailoring].Base;
        BaseBOD original = skill >= 70.1 && (skill - 40.0) / 300.0 > Utility.RandomDouble()
            ? smith ? BulkOrderMaterials.UpgradeLargeOffer(player, new LargeSmithBOD())
                : BulkOrderMaterials.UpgradeLargeOffer(player, new LargeTailorBOD())
            : smith ? SmallSmithBOD.CreateRandomFor(player) : SmallTailorBOD.CreateRandomFor(player);
        if (original == null)
        {
            return null;
        }

        BaseBOD offer = original switch
        {
            SmallBOD small => NewSmall(
                smith ? BODType.ExcelsiorSmith : BODType.ExcelsiorTailor,
                small.AmountCur, small.AmountMax, small.Type, small.Number, small.Graphic, small.RequireExceptional,
                small.Material
            ),
            LargeBOD large => NewLarge(
                smith ? BODType.ExcelsiorSmith : BODType.ExcelsiorTailor,
                large.AmountMax, large.RequireExceptional, large.Material, large.Entries
            ),
            _ => null
        };
        original.Delete();
        if (offer == null)
        {
            return null;
        }

        if (smith)
        {
            player.NextSmithBulkOrder = _offerInterval;
        }
        else
        {
            player.NextTailorBulkOrder = _offerInterval;
        }

        return offer;
    }

    internal static SmallBOD NewSmall(
        BODType kind, int current, int amount, Type type, int number, int graphic, bool exceptional, BulkMaterialType material
    ) => kind == BODType.ExcelsiorSmith
        ? new ExcelsiorSmallSmithBOD(current, amount, type, number, graphic, exceptional, material)
        : new ExcelsiorSmallTailorBOD(current, amount, type, number, graphic, exceptional, material);

    internal static LargeBOD NewLarge(
        BODType kind, int amount, bool exceptional, BulkMaterialType material, LargeBulkEntry[] entries
    )
    {
        LargeBOD deed = kind == BODType.ExcelsiorSmith
            ? new ExcelsiorLargeSmithBOD(amount, exceptional, material, entries)
            : new ExcelsiorLargeTailorBOD(amount, exceptional, material, entries);

        foreach (var entry in entries)
        {
            entry.Owner = deed;
        }

        return deed;
    }

    internal static BODType BookKind(BaseBOD deed) => deed switch
    {
        ExcelsiorSmallSmithBOD or ExcelsiorLargeSmithBOD => BODType.ExcelsiorSmith,
        ExcelsiorSmallTailorBOD or ExcelsiorLargeTailorBOD => BODType.ExcelsiorTailor,
        _ => deed.Profession
    };

    internal static BODType Profession(BODType kind) => kind switch
    {
        BODType.ExcelsiorSmith => BODType.Smith,
        BODType.ExcelsiorTailor => BODType.Tailor,
        _ => kind
    };

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0);
        writer.WriteEncodedInt(_selected.Count);
        foreach (var player in _selected)
        {
            writer.Write(player);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        _ = reader.ReadEncodedInt();
        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            if (reader.ReadEntity<PlayerMobile>() is { } player)
            {
                _selected.Add(player);
            }
        }
    }

    private sealed class RulesGump(PlayerMobile player) : DynamicGump(45, 45)
    {
        public override bool Singleton => true;

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();
            builder.AddBackground(0, 0, 410, 175, 5054);
            builder.AddLabel(25, 22, 0x481, "Smith and tailor order rules");
            builder.AddLabel(25, 49, 0x481, Selected(player) ? "Using Excelsior rules" : "Using classic rules");
            builder.AddButton(25, 80, 4005, 4007, 1);
            builder.AddLabel(65, 80, 0x481, "Classic offers and rewards");
            builder.AddButton(25, 108, 4005, 4007, 2);
            builder.AddLabel(65, 108, 0x481, "Excelsior offers and rewards");
            builder.AddLabel(25, 145, 0x481, "Current orders and wait times stay in place.");
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile == player && player.NetState == sender && player.Alive && info.ButtonID is 1 or 2)
            {
                Select(player, info.ButtonID == 2);
                player.SendMessage(info.ButtonID == 2 ? "Future orders use Excelsior rules." : "Future orders use classic rules.");
            }
        }
    }
}

[SerializationGenerator(0)]
public sealed partial class ExcelsiorSmallSmithBOD : SmallSmithBOD
{
    [Constructible(AccessLevel.GameMaster)]
    public ExcelsiorSmallSmithBOD()
    {
    }

    internal ExcelsiorSmallSmithBOD(
        int current, int amount, Type type, int number, int graphic, bool exceptional, BulkMaterialType material
    ) : base(current, amount, type, number, graphic, exceptional, material)
    {
    }

    public override string DefaultName => "Excelsior smith bulk order deed";
    public override RewardGroup GetRewardGroup() => ExcelsiorBodRewardGroups.Smith(SmithRewardCalculator.Instance.ComputePoints(this));
}

[SerializationGenerator(0)]
public sealed partial class ExcelsiorLargeSmithBOD : LargeSmithBOD
{
    [Constructible(AccessLevel.GameMaster)]
    public ExcelsiorLargeSmithBOD()
    {
    }

    internal ExcelsiorLargeSmithBOD(int amount, bool exceptional, BulkMaterialType material, LargeBulkEntry[] entries)
        : base(amount, exceptional, material, entries)
    {
    }

    public override string DefaultName => "large Excelsior smith bulk order deed";
    public override RewardGroup GetRewardGroup() => ExcelsiorBodRewardGroups.Smith(SmithRewardCalculator.Instance.ComputePoints(this));
}

[SerializationGenerator(0)]
public sealed partial class ExcelsiorSmallTailorBOD : SmallTailorBOD
{
    [Constructible(AccessLevel.GameMaster)]
    public ExcelsiorSmallTailorBOD()
    {
    }

    internal ExcelsiorSmallTailorBOD(
        int current, int amount, Type type, int number, int graphic, bool exceptional, BulkMaterialType material
    ) : base(current, amount, type, number, graphic, exceptional, material)
    {
    }

    public override string DefaultName => "Excelsior tailor bulk order deed";
    public override RewardGroup GetRewardGroup() => ExcelsiorBodRewardGroups.Tailor(TailorRewardCalculator.Instance.ComputePoints(this));
}

[SerializationGenerator(0)]
public sealed partial class ExcelsiorLargeTailorBOD : LargeTailorBOD
{
    [Constructible(AccessLevel.GameMaster)]
    public ExcelsiorLargeTailorBOD()
    {
    }

    internal ExcelsiorLargeTailorBOD(int amount, bool exceptional, BulkMaterialType material, LargeBulkEntry[] entries)
        : base(amount, exceptional, material, entries)
    {
    }

    public override string DefaultName => "large Excelsior tailor bulk order deed";
    public override RewardGroup GetRewardGroup() => ExcelsiorBodRewardGroups.Tailor(TailorRewardCalculator.Instance.ComputePoints(this));
}
