using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.BioEngineering;

public sealed class BioStarterContract : GenericPersistence
{
    internal const int RequiredFamilies = 3;
    private const int TinkerRange = 4;
    private static readonly Dictionary<PlayerMobile, Progress> _progress = new();

    private sealed class Progress
    {
        internal bool Claimed;
        internal readonly List<string> Families = new(RequiredFamilies);
    }

    internal BioStarterContract() : base("BioStarterContract", 10)
    {
    }

    public static void Configure() => _ = new BioStarterContract();

    [OnEvent(nameof(PlayerMobile.PlayerDeletedEvent))]
    public static void OnPlayerDeleted(PlayerMobile player) => _progress.Remove(player);

    internal static (bool Started, int Surveyed, bool Claimed) GetStatus(PlayerMobile player) =>
        _progress.TryGetValue(player, out var entry) ? (true, entry.Families.Count, entry.Claimed) : default;

    internal static bool HasResearchSkills(PlayerMobile player) =>
        player.Skills.AnimalTaming.Base >= 100 && player.Skills.Tinkering.Base >= 100;

    internal static bool TryRequest(PlayerMobile player, Tinker tinker)
    {
        if (player is not { Alive: true, Deleted: false, Map: not null } || player.Map == Map.Internal)
        {
            return false;
        }

        if (tinker is not { Alive: true, Deleted: false } || tinker.Map != player.Map ||
            !player.InRange(tinker, TinkerRange) || !player.CanSee(tinker) || !player.InLOS(tinker))
        {
            player.SendMessage("Visit a tinker to start your contract or claim the kit.");
            return false;
        }

        if (!HasResearchSkills(player))
        {
            player.SendMessage("You need 100 Animal Taming and 100 Tinkering for this contract.");
            return false;
        }

        if (!_progress.TryGetValue(player, out var entry))
        {
            _progress.Add(player, new Progress());
            player.SendMessage("Survey wild creatures from three families with your donor journal.");
            return true;
        }

        if (entry.Claimed)
        {
            player.SendMessage("You have already claimed your starter kit.");
            return false;
        }

        if (entry.Families.Count < RequiredFamilies)
        {
            player.SendMessage("Finish the donor surveys before claiming your starter kit.");
            return false;
        }

        var kit = CreateKit();
        if (!player.PlaceInBackpack(kit))
        {
            kit.Delete();
            player.SendMessage("Make room in your backpack for the starter kit.");
            return false;
        }

        entry.Claimed = true;
        entry.Families.Clear();
        player.SendMessage("Your bio engineering starter kit is in your backpack.");
        return true;
    }

    internal static Tinker FindNearbyTinker(PlayerMobile player)
    {
        if (player.Map == null || player.Map == Map.Internal)
        {
            return null;
        }

        foreach (var tinker in player.GetMobilesInRange<Tinker>(TinkerRange))
        {
            if (tinker.Alive && player.CanSee(tinker) && player.InLOS(tinker))
            {
                return tinker;
            }
        }

        return null;
    }

    internal static void RecordDiscovery(PlayerMobile player, BaseCreature donor)
    {
        if (!_progress.TryGetValue(player, out var entry) || entry.Claimed ||
            entry.Families.Count >= RequiredFamilies || !BioVial.IsWildCreature(donor) ||
            BioVial.Capture(BioSampleKind.Mimic, donor) == null)
        {
            return;
        }

        var family = BaseCreature.OpponentTrainingFamily(donor);
        if (entry.Families.Contains(family))
        {
            return;
        }

        entry.Families.Add(family);
        if (entry.Families.Count == RequiredFamilies)
        {
            player.SendMessage("Your surveys are complete. Return to a tinker to claim your starter kit.");
        }
    }

    private static Bag CreateKit()
    {
        var kit = new Bag { Name = "bio engineering starter kit" };
        kit.DropItem(new BioEngineeringBook());
        kit.DropItem(new BioEngineeringTool(5));
        kit.DropItem(new BioCreationDeed());
        kit.DropItem(new BioVialSet());
        for (var i = 0; i < 12; i++)
        {
            kit.DropItem(new BioVial());
        }

        return kit;
    }

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0);
        writer.WriteEncodedInt(_progress.Count);
        foreach (var (player, entry) in _progress)
        {
            writer.Write(player);
            writer.Write(entry.Claimed);
            writer.WriteEncodedInt(entry.Families.Count);
            foreach (var family in entry.Families)
            {
                writer.Write(family);
            }
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        _ = reader.ReadEncodedInt();
        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var player = reader.ReadEntity<PlayerMobile>();
            var entry = new Progress { Claimed = reader.ReadBool() };
            var families = reader.ReadEncodedInt();
            for (var j = 0; j < families; j++)
            {
                var family = reader.ReadString();
                if (j < RequiredFamilies)
                {
                    entry.Families.Add(family);
                }
            }

            if (player != null)
            {
                _progress[player] = entry;
            }
        }
    }
}
