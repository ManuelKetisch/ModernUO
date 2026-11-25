using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.Spawners;
using Server.Mobiles;

namespace Server.Engines.PetBreeding;

[SerializationGenerator(0)]
public sealed partial class PetBreeder : AnimalTrainer
{
    // This is the existing west Britain stable's vendor location on both facets.
    private static readonly Point3D _britainStable = new(1388, 1655, 30);
    private static readonly Guid _feluccaSpawner = new("20e147ac-6873-4a77-9a01-b6736e5b1c20");
    private static readonly Guid _trammelSpawner = new("e9a8f00c-293b-4232-99a9-a771f28a70b7");

    [Constructible]
    public PetBreeder() => Title = "the pet breeder";

    public static void Initialize()
    {
        if (ServerConfiguration.GetSetting("petBreeding.placeBritainBreeders", true))
        {
            EnsurePlacement(Map.Felucca, _britainStable, _feluccaSpawner);
            EnsurePlacement(Map.Trammel, _britainStable, _trammelSpawner);
        }
    }

    internal static Spawner EnsurePlacement(Map map, Point3D location, Guid identity)
    {
        if (map == null)
        {
            return null;
        }

        foreach (var spawner in map.GetItemsInRange<Spawner>(location, 12))
        {
            if (spawner.Guid == identity)
            {
                return spawner;
            }
        }

        foreach (var breeder in map.GetMobilesInRange<PetBreeder>(location, 12))
        {
            if (!breeder.Deleted)
            {
                return null;
            }
        }

        var bounds = new Rectangle3D(location.X - 2, location.Y - 2, location.Z, 5, 5, 1);
        var created = new Spawner(1, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), spawnBounds: bounds,
            spawnedNames: [nameof(PetBreeder)])
        {
            Guid = identity,
            Name = "Britain pet breeder spawner",
            WalkingRange = 1
        };
        created.MoveToWorld(location, map);
        created.Spawn();
        return created;
    }

    public override void InitSBInfo()
    {
        base.InitSBInfo();
        SBInfos.Add(new BreederStock());
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile player && TryBeginBreeding(player))
        {
            return;
        }

        base.OnDoubleClick(from);
    }

    public override void OnSpeech(SpeechEventArgs e)
    {
        if (!e.Handled && e.Mobile is PlayerMobile player &&
            e.Speech.Trim().InsensitiveEquals("breed") && TryBeginBreeding(player))
        {
            e.Handled = true;
            return;
        }

        base.OnSpeech(e);
    }

    private bool TryBeginBreeding(PlayerMobile player)
    {
        if (!PetBreedingSystem.CanUseTrainer(player, this))
        {
            return false;
        }

        PetBreedingSelection.Start(player, this);
        return true;
    }

    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        if (from is PlayerMobile player && dropped is PetBreedingClaimTicket ticket)
        {
            return ticket.TryClaimAtBreeder(player, this, Core.Now) != null;
        }

        return base.OnDragDrop(from, dropped);
    }

    private sealed class BreederStock : SBInfo
    {
        public override List<GenericBuyInfo> BuyInfo { get; } =
        [
            new("Breeding delay reduction deed", typeof(PetBreedingDelayReductionDeed),
                PetBreedingDelayReductionDeed.SalePrice, 10, 0x14F0, 0x47E)
        ];

        public override IShopSellInfo SellInfo { get; } = new GenericSellInfo();
    }
}
