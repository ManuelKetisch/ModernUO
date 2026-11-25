using System;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Targeting;

namespace Server.Engines.PetProgression;

[SerializationGenerator(0)]
public sealed partial class PersonalPetTrainerDeed : BaseAddonDeed
{
    [Constructible]
    public PersonalPetTrainerDeed() => Hue = 0x59;

    public override string DefaultName => "personal pet trainer deed";

    public override BaseAddon Addon => new PersonalPetTrainerAddon();
}

[SerializationGenerator(0)]
public sealed partial class PersonalPetTrainerAddon : BaseAddon
{
    private static readonly (int X, int Y)[] _trainerOffsets = [(1, 0), (0, 1), (-1, 0), (0, -1)];
    private PersonalPetTrainer _trainer;

    [Constructible]
    public PersonalPetTrainerAddon()
    {
        AddComponent(new AddonComponent(0x1070) { Name = "personal pet trainer" }, 0, 0, 0);
    }

    public override BaseAddonDeed Deed => new PersonalPetTrainerDeed();

    public override void OnComponentUsed(AddonComponent component, Mobile from)
    {
        if (from is not PlayerMobile player || GetOwnerHouse(player) == null)
        {
            from.SendMessage("Only the house owner can use this trainer.");
            return;
        }

        if (_trainer != null)
        {
            _trainer.Delete();
            player.SendMessage("Your pet stops sparring.");
            return;
        }

        player.SendMessage("Choose a pet to spar for five minutes.");
        player.BeginTarget(BaseCreature.PetInteractionRange, false, TargetFlags.None, (mobile, target) =>
        {
            if (mobile is PlayerMobile owner)
            {
                TryStartSparring(owner, target as BaseCreature);
            }
        });
    }

    internal PersonalPetTrainer TryStartSparring(PlayerMobile owner, BaseCreature pet)
    {
        var house = GetOwnerHouse(owner);
        if (_trainer != null || house == null)
        {
            return null;
        }

        if (pet?.CanManagePetProgression(owner) != true || !house.IsInside(pet))
        {
            owner.SendMessage("Choose one of your living pets inside this house.");
            return null;
        }

        foreach (var (x, y) in _trainerOffsets)
        {
            var location = new Point3D(X + x, Y + y, Z);
            if (!house.IsInside(location, 16) || !Map.CanFit(location, 16) || !pet.InLOS(location))
            {
                continue;
            }

            _trainer = new PersonalPetTrainer(this, house, owner, pet);
            _trainer.MoveToWorld(location, Map);
            pet.IssueOrder(OrderType.Attack, owner, _trainer);
            owner.SendMessage("Sparring has started. Use the trainer again to stop.");
            return _trainer;
        }

        owner.SendMessage("Clear a space beside the trainer for sparring.");
        return null;
    }

    private BaseHouse GetOwnerHouse(PlayerMobile owner)
    {
        if (Deleted || Parent != null || owner is not { Deleted: false, Alive: true } || Map == Map.Internal ||
            owner.Map != Map || !owner.InRange(this, 3) || !owner.InLOS(this))
        {
            return null;
        }

        var house = BaseHouse.FindHouseAt(this);
        return house is { Deleted: false } && house.Owner == owner && house.IsInside(owner) && house.Addons.Contains(this)
            ? house
            : null;
    }

    internal bool KeepsTrainer(PersonalPetTrainer trainer) => !Deleted && _trainer == trainer && trainer.Map == Map;

    internal void ForgetTrainer(PersonalPetTrainer trainer)
    {
        if (_trainer == trainer)
        {
            _trainer = null;
        }
    }

    public override void OnLocationChange(Point3D oldLocation)
    {
        base.OnLocationChange(oldLocation);
        _trainer?.Delete();
    }

    public override void OnMapChange()
    {
        base.OnMapChange();
        _trainer?.Delete();
    }

    public override void OnAfterDelete()
    {
        _trainer?.Delete();
        base.OnAfterDelete();
    }
}

[SerializationGenerator(0)]
public sealed partial class PersonalPetTrainer : BaseCreature
{
    internal const int PracticeHits = 50;
    private static readonly TimeSpan _sessionLength = TimeSpan.FromMinutes(5);

    private PersonalPetTrainerAddon _addon;
    private BaseHouse _house;
    private PlayerMobile _owner;
    private BaseCreature _pet;
    private TimerExecutionToken _endSession;

    internal PersonalPetTrainer(PersonalPetTrainerAddon addon, BaseHouse house, PlayerMobile owner, BaseCreature pet)
        : base(AIType.AI_Melee, FightMode.None)
    {
        _addon = addon;
        _house = house;
        _owner = owner;
        _pet = pet;

        Name = "a pet sparring trainer";
        Body = 0x190;
        Hue = 0x83E8;
        CantWalk = true;
        NoKillAwards = true;
        SetStr(50);
        SetDex(50);
        SetInt(50);
        HitsMaxSeed = PracticeHits;
        Hits = HitsMax;
        Timer.StartTimer(_sessionLength, Delete, out _endSession);
    }

    public override bool IsInvulnerable => true;
    public override bool ClickTitle => false;
    public override bool BardImmune => true;
    public override bool NoHouseRestrictions => true;
    public override bool CanRegenHits => false;

    public override void GetSpeeds(out double activeSpeed, out double passiveSpeed)
    {
        activeSpeed = 0.5;
        passiveSpeed = 1.0;
    }

    internal bool CanSparWith(BaseCreature pet) =>
        !Deleted && pet != null && pet == _pet && _addon?.KeepsTrainer(this) == true &&
        _house is { Deleted: false } && _house.Owner == _owner && _house.Addons.Contains(_addon) && _house.IsInside(this) &&
        _owner is { Deleted: false, Alive: true } && _house.IsInside(_owner) && _house.IsInside(pet) &&
        _owner.InRange(this, BaseCreature.PetInteractionRange) &&
        pet.CanManagePetProgression(_owner);

    public override bool CanBeHarmful(Mobile target, bool message, bool ignoreOurBlessedness) => false;

    public override void Damage(int amount, Mobile from = null, bool informMount = true, bool ignoreEvilOmen = false)
    {
        if (amount <= 0 || from is not BaseCreature pet || !CanSparWith(pet))
        {
            return;
        }

        // Record a landed hit without triggering ordinary damage, loot or death.
        Hits = (Hits <= 1 ? HitsMax : Hits) - 1;
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        // Sparring sessions stop when the shard shuts down.
        Timer.StartTimer(TimeSpan.Zero, Delete);
    }

    public override void OnAfterDelete()
    {
        _endSession.Cancel();
        _addon?.ForgetTrainer(this);

        if (_pet is { Deleted: false } pet && _owner != null && pet.ControlMaster == _owner &&
            pet.ControlTarget == this)
        {
            pet.IssueOrder(OrderType.Follow, null, _owner);
        }

        _addon = null;
        _house = null;
        _owner = null;
        _pet = null;
        base.OnAfterDelete();
    }
}
