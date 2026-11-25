using ModernUO.Serialization;
using Server.Engines.PetProgression;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Engines.BulkOrders;

[SerializationGenerator(0)]
public sealed partial class PetLeash : Item
{
    [SerializedIgnoreDupe]
    [SerializableField(0, setter: "private")]
    private BaseCreature _pet;

    [SerializedIgnoreDupe]
    [SerializableField(1, setter: "private")]
    private PlayerMobile _owner;

    private TimerExecutionToken _ownerCheck;

    [Constructible]
    public PetLeash() : base(0x1372)
    {
    }

    public override string DefaultName => "pet leash";
    public override bool Nontransferable => Owner != null || base.Nontransferable;
    public override double DefaultWeight => 1;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        if (Pet is { Deleted: false } pet)
        {
            list.Add($"{pet.Name}");
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile player || !CanUse(player))
        {
            return;
        }

        if (Pet != null)
        {
            TryRelease(player);
        }
        else
        {
            player.SendMessage("Choose a nearby bonded pet to shrink.");
            player.BeginTarget(PetCare.Range, false, TargetFlags.None, (_, target) =>
            {
                if (target is BaseCreature pet)
                {
                    TryShrink(player, pet);
                }
            });
        }
    }

    private bool CanUse(PlayerMobile player)
    {
        if (Deleted || !player.Alive || !IsChildOf(player.Backpack) || GetSecureTradeCont() != null ||
            player.Map == null || player.Map == Map.Internal || Owner != null && Owner != player)
        {
            return false;
        }

        if (player.Skills.AnimalTaming.Base < 75)
        {
            player.SendMessage("You need 75 Animal Taming to use this leash.");
            return false;
        }

        return true;
    }

    internal bool TryShrink(PlayerMobile player, BaseCreature pet)
    {
        if (!CanUse(player) || Pet != null || !PetCare.CanReachOwnedPet(player, pet) ||
            pet is not { Alive: true, IsBonded: true, IsDeadPet: false, Combatant: null } ||
            pet is IMount { Rider: not null } || HasCargo(pet))
        {
            player.SendMessage("Choose a living bonded pet out of combat. Dismount and remove its cargo first.");
            return false;
        }

        // An occupied leash cannot travel inside a bag that can itself be traded.
        if (Parent != player.Backpack)
        {
            player.SendMessage("Place the leash directly in your backpack first.");
            return false;
        }

        if (!pet.SetControlMaster(null))
        {
            return false;
        }

        pet.ControlTarget = null;
        pet.ControlOrder = OrderType.Stay;
        pet.IsStabled = true;
        pet.StabledBy = player;
        pet.Internalize();

        Owner = player;
        Pet = pet;
        LootType = LootType.Blessed;
        BlessedFor = player;
        ItemID = ShrinkTable.Lookup(pet);
        InvalidateProperties();
        player.SendMessage("Your pet is safe on the leash. Use it again to release the pet.");
        return true;
    }

    internal bool TryRelease(PlayerMobile player)
    {
        if (!CanUse(player) || Owner != player || Pet is not { Deleted: false, IsStabled: true } pet ||
            pet.StabledBy != player || pet.Map != Map.Internal || pet.ControlMaster != null)
        {
            return false;
        }

        if (player.Followers + pet.ControlSlots > player.FollowersMax ||
            !player.Map.CanFit(player.Location, 16, false, false) || !pet.SetControlMaster(player))
        {
            player.SendMessage("Make room for your pet and stand somewhere it can fit.");
            return false;
        }

        pet.IsStabled = false;
        pet.StabledBy = null;
        pet.ControlTarget = player;
        pet.ControlOrder = OrderType.Follow;
        pet.MoveToWorld(player.Location, player.Map);
        ResetLeash();
        player.SendMessage("Your pet has returned.");
        return true;
    }

    private void ResetLeash()
    {
        Pet = null;
        Owner = null;
        BlessedFor = null;
        LootType = LootType.Regular;
        ItemID = 0x1372;
        InvalidateProperties();
    }

    [AfterDeserialization(false)]
    private void RestoreReservation()
    {
        if (Pet == null && Owner == null)
        {
            return;
        }

        if (!TryGetReservedPet(out var owner, out var pet) || owner.Stabled?.Contains(pet) == true)
        {
            ResetLeash();
            return;
        }

        // The creature's normal stable flags are runtime state. Setting IsStabled also cancels abandonment.
        pet.IsStabled = true;
        pet.StabledBy = owner;
        RecoverIfSeparated();
    }

    public override void OnAdded(IEntity parent)
    {
        base.OnAdded(parent);
        QueueOwnerCheck();
    }

    public override void OnRemoved(IEntity parent)
    {
        base.OnRemoved(parent);
        QueueOwnerCheck();
    }

    private void QueueOwnerCheck()
    {
        if (World.Running && !Deleted && Owner != null)
        {
            _ownerCheck.Cancel();
            Timer.StartTimer(RecoverIfSeparated, out _ownerCheck);
        }
    }

    private void RecoverIfSeparated()
    {
        if (!Deleted && Owner is { } owner && RootParent != owner &&
            (Parent != null || owner.Holding != this))
        {
            ReturnToStables();
            ResetLeash();
        }
    }

    private bool TryGetReservedPet(out PlayerMobile owner, out BaseCreature pet)
    {
        owner = Owner;
        pet = Pet;
        return owner is { Deleted: false } &&
               pet is { Deleted: false, IsBonded: true, Controlled: false, Summoned: false, ControlMaster: null } &&
               pet.Map == Map.Internal && (pet.StabledBy == null || pet.StabledBy == owner);
    }

    private void ReturnToStables()
    {
        if (TryGetReservedPet(out var owner, out var pet))
        {
            pet.IsStabled = true;
            pet.StabledBy = owner;
            owner.AddStabled(pet);
        }
    }

    public override void OnDelete()
    {
        _ownerCheck.Cancel();
        ReturnToStables();
        base.OnDelete();
    }

    internal static bool HasCargo(BaseCreature pet)
    {
        if (pet.Backpack?.Items.Count > 0)
        {
            return true;
        }

        foreach (var item in pet.Items)
        {
            if (item != pet.Backpack && item.Movable)
            {
                return true;
            }
        }

        return false;
    }
}
