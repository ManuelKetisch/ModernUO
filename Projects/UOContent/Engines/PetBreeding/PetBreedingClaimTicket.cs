using System;
using System.Reflection;
using ModernUO.Serialization;
using Server.Logging;
using Server.Mobiles;
using Server.SkillHandlers;

namespace Server.Engines.PetBreeding;

[SerializationGenerator(1)]
public sealed partial class PetBreedingClaimTicket : Item
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(PetBreedingClaimTicket));

    [SerializableField(0, setter: "private")]
    private Type _species;

    [SerializableField(1, setter: "private")]
    private string _speciesName;

    [SerializableField(2, setter: "private")]
    private int _generation;

    [SerializableField(3, setter: "private")]
    private int _maxLevel;

    [SerializableField(4, setter: "private")]
    private bool _female;

    [SerializableField(5, setter: "private")]
    private DateTime _readyAt;

    [SerializableField(6, setter: "private")]
    private int _controlSlots;

    [SerializableField(7, getter: "private", setter: "private")]
    private int[] _traits;

    [SerializableField(8, getter: "private", setter: "private")]
    private string _damName;

    [SerializableField(9, getter: "private", setter: "private")]
    private int _damLevel;

    [SerializableField(10, getter: "private", setter: "private")]
    private int _damGeneration;

    [SerializableField(11, getter: "private", setter: "private")]
    private string _sireName;

    [SerializableField(12, getter: "private", setter: "private")]
    private int _sireLevel;

    [SerializableField(13, getter: "private", setter: "private")]
    private int _sireGeneration;

    internal PetPedigree? Pedigree => _damLevel > 0 && _sireLevel > 0
        ? new PetPedigree(_damName, _damLevel, _damGeneration, _sireName, _sireLevel, _sireGeneration)
        : null;

    private void MigrateFrom(V0Content content)
    {
        _species = content.Species;
        _speciesName = content.SpeciesName;
        _generation = content.Generation;
        _maxLevel = content.MaxLevel;
        _female = content.Female;
        _readyAt = content.ReadyAt;
        _controlSlots = content.ControlSlots;
        _traits = content.Traits;
    }

    internal PetBreedingClaimTicket(
        PlayerMobile owner, BaseCreature parent, BaseCreature mate, DateTime readyAt, PetTrainingStat? focus = null
    ) : base(0x14F0)
    {
        BlessedFor = owner;
        _species = parent.GetType();
        _speciesName = parent.DefaultName ?? parent.Name ?? "Pet";
        _generation = parent.PetGeneration + 1;
        var (minLevel, maxLevel) = PetBreedingTraits.GetOffspringMaximumLevelRange(parent, mate);
        _maxLevel = Utility.RandomMinMax(minLevel, maxLevel);
        _female = parent.PetFemale;
        _readyAt = readyAt;
        _controlSlots = Math.Max(parent.ControlSlots, mate.ControlSlots);
        _traits = PetBreedingTraits.Capture(parent, mate, focus);

        var dam = parent.PetFemale ? parent : mate;
        var sire = parent.PetFemale ? mate : parent;
        _damName = dam.Name ?? dam.DefaultName ?? "Unknown";
        _damLevel = dam.PetLevel;
        _damGeneration = dam.PetGeneration;
        _sireName = sire.Name ?? sire.DefaultName ?? "Unknown";
        _sireLevel = sire.PetLevel;
        _sireGeneration = sire.PetGeneration;
        Weight = 1;
    }

    public override string DefaultName => "pet breeding claim ticket";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        var sex = _female ? "Female" : "Male";
        list.Add($"{"Pet"}\t{_speciesName}");
        list.Add($"{"Sex"}\t{sex}");
        list.Add($"{"Generation"}\t{_generation}");
        list.Add($"{"Maximum level"}\t{_maxLevel}");
        list.Add($"{"Claim from"}\t{_readyAt:u}");
        if (Pedigree is { } pedigree)
        {
            list.Add($"{"Mother"}\t{pedigree.DamName}");
            list.Add($"{"Father"}\t{pedigree.SireName}");
        }
    }

    public override void OnDoubleClick(Mobile from) => TryClaim(from, Core.Now);

    internal BaseCreature TryClaim(Mobile from, DateTime now) => Claim(from, now, false);

    internal BaseCreature TryClaimAtBreeder(PlayerMobile from, PetBreeder breeder, DateTime now)
    {
        if (!PetBreedingSystem.CanUseTrainer(from, breeder))
        {
            from.SendMessage("Move closer to the pet breeder.");
            return null;
        }

        return Claim(from, now, true);
    }

    private BaseCreature Claim(Mobile from, DateTime now, bool droppedAtBreeder)
    {
        if (Deleted || from is not PlayerMobile player || !PetBreedingSystem.CanUseBreeding(player))
        {
            return null;
        }

        var pack = player.Backpack;
        if (BlessedFor != player || pack == null || !IsChildOf(pack) &&
            !(droppedAtBreeder && Map == Map.Internal && GetBounce()?.Parent is Item source &&
              (source == pack || source.IsChildOf(pack))))
        {
            player.SendMessage("Keep your claim ticket in your backpack to redeem it.");
            return null;
        }

        if (now < _readyAt)
        {
            player.SendMessage($"You can claim this pet from {_readyAt:u}.");
            return null;
        }

        if (player.Followers + _controlSlots > player.FollowersMax)
        {
            player.SendLocalizedMessage(1049607); // You have too many followers to control that creature.
            return null;
        }

        var constructor = PetBreedingSystem.GetOffspringConstructor(_species);
        if (constructor == null || !PetBreedingTraits.IsValid(_traits))
        {
            player.SendMessage("This ticket cannot be redeemed. Keep it and contact a game master.");
            return null;
        }

        BaseCreature offspring;
        try
        {
            offspring = (BaseCreature)constructor.Invoke(null);
        }
        catch (TargetInvocationException exception)
        {
            _logger.Error(exception, "Unable to create offspring for breeding ticket {Serial}.", Serial);
            player.SendMessage("Your pet could not be delivered. Try again later.");
            return null;
        }

        offspring.ControlSlots = _controlSlots;
        if (offspring is GreaterDragon)
        {
            AnimalTaming.ScaleSkills(offspring, 0.72, 0.90);
            offspring.Skills.Magery.Base = offspring.Skills.Magery.Cap;
        }
        else
        {
            AnimalTaming.ScaleSkills(offspring, 0.90);
        }

        PetBreedingTraits.Apply(offspring, _traits);
        offspring.InitializePetProgression(_generation, _maxLevel, _female);
        if (!offspring.SetControlMaster(player))
        {
            offspring.Delete();
            return null;
        }

        offspring.Owners.Add(player);
        offspring.ControlTarget = player;
        offspring.ControlOrder = OrderType.Follow;
        offspring.MoveToWorld(player.Location, player.Map);
        if (Pedigree is { } pedigree)
        {
            PetBreedingRecords.RecordPedigree(offspring, pedigree);
        }

        Delete();
        player.SendMessage("Your new pet is here.");
        return offspring;
    }
}
