using System;
using ModernUO.Serialization;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Engines.PetBreeding;

[SerializationGenerator(0)]
public sealed partial class PetBreedingDelayReductionDeed : Item
{
    private static readonly TimeSpan _reduction = TimeSpan.FromHours(24);
    internal const int SalePrice = 5000;

    [Constructible]
    public PetBreedingDelayReductionDeed() : base(0x14F0)
    {
        Hue = 0x47E;
    }

    public override string DefaultName => "breeding delay reduction deed";
    public override double DefaultWeight => 1.0;

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile player || !CanUse(player))
        {
            return;
        }

        player.SendMessage("Choose your pet. Its breeding rest will shorten by up to 24 hours.");
        player.BeginTarget(PetBreedingSystem.PetRange, false, TargetFlags.None, (mobile, target) =>
        {
            TryReduce((PlayerMobile)mobile, target as BaseCreature, Core.Now);
        });
    }

    internal bool TryReduce(PlayerMobile player, BaseCreature pet, DateTime now)
    {
        if (!CanUse(player))
        {
            return false;
        }

        if (PetBreedingSystem.CheckParent(player, pet, null, now, requireReady: false) is { } failure)
        {
            player.SendMessage(failure);
            return false;
        }

        if (pet.PetBreedingReadyAt <= now)
        {
            player.SendMessage("This pet is already ready to breed.");
            return false;
        }

        var remaining = pet.PetBreedingReadyAt - now;
        pet.PetBreedingReadyAt = remaining <= _reduction ? now : pet.PetBreedingReadyAt - _reduction;
        Delete();
        player.SendMessage("You shortened the pet's breeding rest.");
        return true;
    }

    private bool CanUse(PlayerMobile player)
    {
        if (Deleted || !PetBreedingSystem.CanUseBreeding(player))
        {
            return false;
        }

        if (player.Backpack == null || !IsChildOf(player.Backpack))
        {
            player.SendMessage("Keep the deed in your backpack to use it.");
            return false;
        }

        return true;
    }
}
