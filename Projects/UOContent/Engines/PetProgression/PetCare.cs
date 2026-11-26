using System;
using ModernUO.Serialization;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Engines.PetProgression;

internal static class PetCare
{
    internal const int Range = 3;

    internal static bool CanReachOwnedPet(PlayerMobile player, BaseCreature pet) =>
        player is { Alive: true, Deleted: false, Map: not null } && player.Map != Map.Internal &&
        pet is { Deleted: false, Controlled: true, Summoned: false, IsStabled: false } &&
        pet.ControlMaster == player && pet.Map == player.Map && player.InRange(pet, Range) &&
        player.CanSee(pet) && player.InLOS(pet);

    internal static bool TryBond(PlayerMobile player, BaseCreature pet)
    {
        if (!CanReachOwnedPet(player, pet) || !pet.Alive || pet.IsDeadPet || pet.IsBonded || !pet.IsBondable)
        {
            player.SendMessage("Choose a living pet of yours that can bond.");
            return false;
        }

        pet.IsBonded = true;
        pet.BondingBegin = DateTime.MinValue;
        player.SendLocalizedMessage(1049666); // Your pet has bonded with you!
        return true;
    }

    internal static bool TryResurrect(PlayerMobile player, BaseCreature pet)
    {
        if (!CanReachOwnedPet(player, pet) || !pet.IsBonded || !pet.IsDeadPet)
        {
            player.SendMessage("Choose a nearby bonded pet of yours that has died.");
            return false;
        }

        if (pet.Map.CanFit(pet.Location, 16, false, false) != true)
        {
            player.SendLocalizedMessage(501042); // Target can not be resurrected at that location.
            return false;
        }

        if (pet.Region?.IsPartOf("Khaldun") == true)
        {
            player.SendLocalizedMessage(1010395);
            return false;
        }

        pet.ResurrectPet();
        if (pet.IsDeadPet)
        {
            return false;
        }

        pet.PlaySound(0x214);
        pet.FixedEffect(0x376A, 10, 16);
        player.SendMessage("Your pet has returned to life.");
        return true;
    }

    internal static bool TryRestore(PlayerMobile player, BaseCreature pet)
    {
        if (!CanReachOwnedPet(player, pet) || !pet.Alive || pet.IsDeadPet || !pet.IsBonded ||
            !pet.TryRestorePetDeathStats())
        {
            player.SendMessage("Choose a living bonded pet of yours with stats lost on death.");
            return false;
        }

        player.SendMessage("The ointment restores your pet's lost stats.");
        return true;
    }
}

public abstract class PetCareItem : Item
{
    protected PetCareItem(int itemId) : base(itemId)
    {
    }

    protected PetCareItem(Serial serial) : base(serial)
    {
    }

    public override double DefaultWeight => Server.Items.ItemWeights.SmallSupply;

    protected abstract string TargetPrompt { get; }

    protected abstract bool Apply(PlayerMobile player, BaseCreature pet);

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile player || !CanUse(player))
        {
            return;
        }

        player.SendMessage(TargetPrompt);
        player.BeginTarget(PetCare.Range, false, TargetFlags.None, (mobile, target) =>
        {
            if (mobile is PlayerMobile owner)
            {
                TryUse(owner, target as BaseCreature);
            }
        });
    }

    internal bool TryUse(PlayerMobile player, BaseCreature pet)
    {
        if (!CanUse(player) || !Apply(player, pet))
        {
            return false;
        }

        Delete();
        return true;
    }

    private bool CanUse(PlayerMobile player)
    {
        if (Deleted || !player.Alive || player.Deleted)
        {
            return false;
        }

        if (player.Backpack == null || !IsChildOf(player.Backpack))
        {
            player.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return false;
        }

        return true;
    }
}

[SerializationGenerator(0)]
public sealed partial class PetBondingDeed : PetCareItem
{
    [Constructible]
    public PetBondingDeed() : base(0x14F0) => Hue = 0x59;

    public override string DefaultName => "pet bonding deed";
    protected override string TargetPrompt => "Choose one of your pets to bond.";
    protected override bool Apply(PlayerMobile player, BaseCreature pet) => PetCare.TryBond(player, pet);
}

[SerializationGenerator(0)]
public sealed partial class PetResurrectionDraught : PetCareItem
{
    [Constructible]
    public PetResurrectionDraught() : base(0x0F0C) => Hue = 0x59;

    public override string DefaultName => "pet resurrection draught";
    protected override string TargetPrompt => "Choose one of your bonded pets that has died.";
    protected override bool Apply(PlayerMobile player, BaseCreature pet) => PetCare.TryResurrect(player, pet);
}

[SerializationGenerator(0)]
public sealed partial class RestorativePetOintment : PetCareItem
{
    [Constructible]
    public RestorativePetOintment() : base(0x0E24) => Hue = 0x59;

    public override string DefaultName => "restorative pet ointment";
    protected override string TargetPrompt => "Choose a living bonded pet with stats lost on death.";
    protected override bool Apply(PlayerMobile player, BaseCreature pet) => PetCare.TryRestore(player, pet);
}
