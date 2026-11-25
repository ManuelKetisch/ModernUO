using System;
using System.Collections.Generic;
using Server.Commands;
using Server.Engines.Spawners;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.HybridForge;

[Flags]
internal enum WyrmReward : byte
{
    None = 0,
    Scale = 1,
    PolehammerBlueprint = 2,
    PlateBlueprint = 4,
    Trophy = 8
}

internal readonly record struct WyrmRewardStatus(int Credits, int PendingRewards);

internal sealed class WyrmRewardLedger
{
    private readonly Dictionary<Guid, WyrmReceipt> _receipts = new();

    internal bool Record(Guid defeat, Serial owner, WyrmReward bonus) =>
        _receipts.TryAdd(defeat, new WyrmReceipt(owner, WyrmReward.Scale | bonus));

    internal WyrmRewardStatus Status(Serial owner)
    {
        var credits = 0;
        var pending = 0;
        foreach (var receipt in _receipts.Values)
        {
            if (receipt.Owner != owner)
            {
                continue;
            }

            if (!receipt.Spent)
            {
                credits++;
            }

            pending += System.Numerics.BitOperations.PopCount((uint)receipt.Pending);
        }

        return new WyrmRewardStatus(credits, pending);
    }

    internal void Deliver(Guid defeat, Func<WyrmReward, bool> deliver) => Deliver(_receipts[defeat], deliver);

    internal void Claim(Serial owner, Func<WyrmReward, bool> deliver)
    {
        foreach (var receipt in _receipts.Values)
        {
            if (receipt.Owner == owner)
            {
                Deliver(receipt, deliver);
            }
        }
    }

    private static void Deliver(WyrmReceipt receipt, Func<WyrmReward, bool> deliver)
    {
        for (var reward = WyrmReward.Scale; reward <= WyrmReward.Trophy; reward = (WyrmReward)((byte)reward << 1))
        {
            if ((receipt.Pending & reward) != 0 && deliver(reward))
            {
                receipt.Pending &= ~reward;
            }
        }
    }

    internal bool Spend(Serial owner, int cost, Func<bool> deliver)
    {
        var credits = new List<WyrmReceipt>(cost);
        foreach (var receipt in _receipts.Values)
        {
            if (receipt.Owner != owner || receipt.Spent)
            {
                continue;
            }

            credits.Add(receipt);
            if (credits.Count == cost)
            {
                break;
            }
        }

        if (credits.Count != cost || !deliver())
        {
            return false;
        }

        foreach (var receipt in credits)
        {
            receipt.Spent = true;
        }

        return true;
    }

    internal void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0);
        writer.WriteEncodedInt(_receipts.Count);
        foreach (var (defeat, receipt) in _receipts)
        {
            writer.Write(defeat);
            writer.Write(receipt.Owner);
            writer.Write((byte)receipt.Pending);
            writer.Write(receipt.Spent);
        }
    }

    internal void Deserialize(IGenericReader reader)
    {
        _ = reader.ReadEncodedInt(); // Version
        var count = reader.ReadEncodedInt();
        _receipts.Clear();

        for (var i = 0; i < count; i++)
        {
            var defeat = reader.ReadGuid();
            var owner = reader.ReadSerial();
            var pending = (WyrmReward)reader.ReadByte();
            _receipts.Add(defeat, new WyrmReceipt(owner, pending) { Spent = reader.ReadBool() });
        }
    }

    private sealed class WyrmReceipt(Serial owner, WyrmReward pending)
    {
        internal Serial Owner { get; } = owner;
        internal WyrmReward Pending { get; set; } = pending;
        internal bool Spent { get; set; }
    }
}

public sealed class WyrmForgeRewards : GenericPersistence
{
    internal const int BlueprintPrice = 6;

    // These are the two shipped Destard spawns, one on each facet.
    private static readonly Guid _feluccaSpawn = new("e6703676-d851-4394-b09f-51286d5d85f2");
    private static readonly Guid _trammelSpawn = new("699dfc8f-e891-4978-9c73-a74cb1762849");
    private static readonly WyrmRewardLedger _ledger = new();

    private WyrmForgeRewards() : base("WyrmForgeRewards", 11)
    {
    }

    public static void Configure() => _ = new WyrmForgeRewards();

    public static void Initialize() => CommandSystem.Register(
        "WyrmRewards", AccessLevel.Player, e =>
        {
            if (e.Mobile is PlayerMobile { Alive: true } owner)
            {
                owner.SendGump(new WyrmRewardsGump(owner));
            }
        }
    );

    public override void Serialize(IGenericWriter writer) => _ledger.Serialize(writer);
    public override void Deserialize(IGenericReader reader) => _ledger.Deserialize(reader);

    internal static void OnDeath(AncientWyrm wyrm)
    {
        if (wyrm is { Controlled: true } or { Summoned: true } or { NoKillAwards: true } or { LastOwner: not null } or { IsBonded: true } ||
            wyrm.Spawner is not BaseSpawner { Deleted: false } spawner ||
            spawner.Map != wyrm.Map ||
            (spawner.Map != Map.Felucca || spawner.Guid != _feluccaSpawn) &&
            (spawner.Map != Map.Trammel || spawner.Guid != _trammelSpawn))
        {
            return;
        }

        var owner = ChooseRecipient(BaseCreature.GetLootingRights(wyrm.DamageEntries, wyrm.HitsMax));
        if (owner == null)
        {
            return;
        }

        if (wyrm.ForgeDefeatId == Guid.Empty)
        {
            wyrm.ForgeDefeatId = Guid.NewGuid();
        }

        var defeat = wyrm.ForgeDefeatId;
        if (_ledger.Record(defeat, owner.Serial, RollBonus(Utility.Random(1000))))
        {
            _ledger.Deliver(defeat, reward => TryDeliver(owner, reward));
            owner.SendMessage("You earned one Ancient Wyrm credit. Use [WyrmRewards to see your rewards.");
        }
    }

    internal static PlayerMobile ChooseRecipient(IEnumerable<DamageStore> rights)
    {
        PlayerMobile winner = null;
        var best = 0;
        foreach (var right in rights)
        {
            if (right is not { m_HasRight: true, m_Mobile: PlayerMobile { Deleted: false, AccessLevel: AccessLevel.Player } player } ||
                right.m_Damage < best || right.m_Damage == best && winner != null && (uint)player.Serial >= (uint)winner.Serial)
            {
                continue;
            }

            winner = player;
            best = right.m_Damage;
        }

        return winner;
    }

    internal static WyrmReward RollBonus(int roll) => roll switch
    {
        < 40 => WyrmReward.PolehammerBlueprint,
        < 80 => WyrmReward.PlateBlueprint,
        < 100 => WyrmReward.Trophy,
        _ => WyrmReward.None
    };

    internal static WyrmRewardStatus Status(PlayerMobile owner) => _ledger.Status(owner.Serial);

    internal static void Claim(PlayerMobile owner) => _ledger.Claim(owner.Serial, reward => TryDeliver(owner, reward));

    internal static bool Exchange(PlayerMobile owner, HybridForgeRecipe recipe)
    {
        var reward = recipe == HybridForgeRecipe.Polehammer ? WyrmReward.PolehammerBlueprint : WyrmReward.PlateBlueprint;
        return !owner.HasRecipe(HybridForge.RecipeId(recipe)) &&
               _ledger.Spend(owner.Serial, BlueprintPrice, () => TryDeliver(owner, reward));
    }

    private static bool TryDeliver(PlayerMobile owner, WyrmReward reward)
    {
        if (owner is not { Alive: true, Deleted: false, Backpack: { } pack })
        {
            return false;
        }

        Item item = reward switch
        {
            WyrmReward.Scale => new AncientWyrmScale(),
            WyrmReward.PolehammerBlueprint => new WyrmForgeBlueprint(),
            WyrmReward.PlateBlueprint => new WyrmForgeBlueprint(true),
            WyrmReward.Trophy => new WyrmForgeTrophy(),
            _ => throw new ArgumentOutOfRangeException(nameof(reward))
        };

        if (pack.TryDropItem(owner, item, false))
        {
            owner.SendMessage($"Your backpack received {item.DefaultName}.");
            return true;
        }

        item.Delete();
        return false;
    }
}

internal sealed class WyrmRewardsGump(PlayerMobile owner) : DynamicGump(50, 50)
{
    public override bool Singleton => true;

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var status = WyrmForgeRewards.Status(owner);
        builder.AddPage();
        builder.AddBackground(0, 0, 425, 242, 0x13BE);
        builder.AddLabel(22, 19, 0x481, "Ancient Wyrm rewards");
        builder.AddLabel(22, 50, 0x481, $"Credits: {status.Credits}");
        builder.AddLabel(22, 75, 0x481, $"Unclaimed rewards: {status.PendingRewards}");
        builder.AddButton(22, 105, 0xFA5, 0xFA7, 1);
        builder.AddLabel(55, 105, 0x481, "Claim rewards");
        builder.AddLabel(22, 139, 0x481, $"Each blueprint costs {WyrmForgeRewards.BlueprintPrice} credits.");
        builder.AddButton(22, 166, 0xFA5, 0xFA7, 2);
        builder.AddLabel(55, 166, 0x481, "Choose polehammer blueprint");
        builder.AddButton(22, 196, 0xFA5, 0xFA7, 3);
        builder.AddLabel(55, 196, 0x481, "Choose scale-lined plate blueprint");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender != owner.NetState || sender.Mobile != owner || !owner.Alive ||
            info.ButtonID is < 1 or > 3)
        {
            return;
        }

        if (info.ButtonID == 1)
        {
            WyrmForgeRewards.Claim(owner);
        }
        else if (WyrmForgeRewards.Status(owner).Credits < WyrmForgeRewards.BlueprintPrice)
        {
            owner.SendMessage("You need six unspent Ancient Wyrm credits.");
        }
        else
        {
            var recipe = info.ButtonID == 2 ? HybridForgeRecipe.Polehammer : HybridForgeRecipe.ScaleLinedPlate;
            if (owner.HasRecipe(HybridForge.RecipeId(recipe)))
            {
                owner.SendMessage("You already know that recipe.");
            }
            else if (!WyrmForgeRewards.Exchange(owner, recipe))
            {
                owner.SendMessage("Make room in your backpack before exchanging.");
            }
        }

        owner.SendGump(new WyrmRewardsGump(owner));
    }
}
