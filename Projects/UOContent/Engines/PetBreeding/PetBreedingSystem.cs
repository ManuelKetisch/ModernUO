using System;
using System.Collections.Generic;
using System.Reflection;
using ModernUO.CodeGeneratedEvents;
using Server.Mobiles;

namespace Server.Engines.PetBreeding;

public sealed class PetBreedingSystem : GenericPersistence
{
    internal const int PetRange = BaseCreature.PetInteractionRange;
    private const int TrainerRange = 4;
    private static readonly TimeSpan _litterWindow = TimeSpan.FromHours(12);
    private static readonly TimeSpan _claimDelay = TimeSpan.FromDays(3);
    private static readonly TimeSpan _parentDelay = TimeSpan.FromDays(6);
    private static readonly Dictionary<PlayerMobile, LitterHistory> _history = new();

    private readonly record struct LitterHistory(DateTime Earlier, DateTime Latest)
    {
        public bool AtLimit(DateTime now) => Earlier + _litterWindow > now;
    }

    internal PetBreedingSystem() : base("PetBreeding", 10) { }

    public static void Configure() => _ = new PetBreedingSystem();

    public static void Initialize() => CommandSystem.Register("PetBreed", AccessLevel.Player, PetBreedingSelection.Start);

    [OnEvent(nameof(PlayerMobile.PlayerDeletedEvent))]
    public static void OnPlayerDeleted(PlayerMobile player) => _history.Remove(player);

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0);
        writer.WriteEncodedInt(_history.Count);
        foreach (var (player, history) in _history)
        {
            writer.Write(player);
            writer.Write(history.Earlier);
            writer.Write(history.Latest);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        _ = reader.ReadEncodedInt(); // version
        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var player = reader.ReadEntity<PlayerMobile>();
            var history = new LitterHistory(reader.ReadDateTime(), reader.ReadDateTime());
            if (player != null && history.Latest + _litterWindow > Core.Now)
            {
                _history[player] = history;
            }
        }
    }

    internal static DateTime ClaimReadyAt(DateTime now) => now + _claimDelay;

    internal static bool CanUseBreeding(PlayerMobile player) =>
        player is { Deleted: false, Alive: true, Map: not null } && player.Map != Map.Internal;

    internal static bool CanUseTrainer(PlayerMobile player, AnimalTrainer trainer) =>
        CanUseBreeding(player) && trainer is { Deleted: false, Alive: true } && trainer.Map == player.Map &&
        player.InRange(trainer, TrainerRange) && player.CanSee(trainer) && player.InLOS(trainer);

    internal static AnimalTrainer FindNearbyTrainer(PlayerMobile player)
    {
        AnimalTrainer fallback = null;
        foreach (var trainer in player.GetMobilesInRange<AnimalTrainer>(TrainerRange))
        {
            if (CanUseTrainer(player, trainer))
            {
                if (trainer is PetBreeder)
                {
                    return trainer;
                }

                fallback ??= trainer;
            }
        }

        return fallback;
    }

    internal static ConstructorInfo GetOffspringConstructor(Type species) =>
        species is { IsAbstract: false } && species.IsAssignableTo(typeof(BaseCreature)) &&
        species.GetConstructor(Type.EmptyTypes) is { } constructor &&
        constructor.GetCustomAttribute<ConstructibleAttribute>()?.AccessLevel == AccessLevel.Player
            ? constructor : null;

    internal static string CheckParent(PlayerMobile player, BaseCreature pet, AnimalTrainer trainer, DateTime now,
        bool requireReady = true)
    {
        if (!CanUseBreeding(player))
        {
            return "You must be alive to breed pets.";
        }

        if (pet is not { Deleted: false, Alive: true, IsDeadPet: false, Tamable: true, Summoned: false })
        {
            return "Choose a living, tamed pet.";
        }

        if (pet.IsStabled)
        {
            if (!CanUseTrainer(player, trainer) || pet.Map != Map.Internal || pet.StabledBy != player ||
                player.Stabled?.Contains(pet) != true)
            {
                return "Visit an animal trainer to select a pet from your stables.";
            }
        }
        else if (!pet.Controlled || pet.ControlMaster != player || pet.Map != player.Map ||
                 !player.InRange(pet, PetRange) || !player.CanSee(pet) || !player.InLOS(pet))
        {
            return "Choose one of your pets within reach.";
        }

        if (pet.PetLevel is < BaseCreature.FirstBreedingLevel or > BaseCreature.LastBreedingLevel ||
            pet.PetGeneration is < 1 or > BaseCreature.LastBreedingGeneration)
        {
            return "Breeding requires a pet of level 9 to 42 and generation 1 to 9.";
        }

        if (requireReady && pet.PetBreedingReadyAt > now)
        {
            return $"Breeding rest ends {pet.PetBreedingReadyAt:u}.";
        }

        return GetOffspringConstructor(pet.GetType()) == null ? "This kind of pet cannot breed." : null;
    }

    private static string CheckMatch(BaseCreature first, BaseCreature second) =>
        first == second || first.GetType() != second.GetType() || first.PetFemale == second.PetFemale
            ? "Choose a male and a female of the same species." : null;

    internal static string CheckPair(PlayerMobile player, BaseCreature first, BaseCreature second,
        AnimalTrainer trainer, DateTime now) =>
        CheckParent(player, first, trainer, now) ?? CheckParent(player, second, trainer, now) ?? CheckMatch(first, second);

    internal static string CheckPartnerPair(PlayerMobile player, BaseCreature first, PlayerMobile partner,
        BaseCreature second, PetBreeder breeder, DateTime now)
    {
        if (player == partner || player?.NetState == null || partner?.NetState == null ||
            !CanUseTrainer(player, breeder) || !CanUseTrainer(partner, breeder))
        {
            return "Both owners need to be present at the same pet breeder.";
        }

        return CheckParent(player, first, breeder, now) ?? CheckParent(partner, second, breeder, now) ??
               CheckMatch(first, second);
    }

    internal static bool TryBreed(PlayerMobile player, BaseCreature first, BaseCreature second,
        AnimalTrainer trainer, DateTime now, PetTrainingStat? focus = null)
    {
        if (CheckPair(player, first, second, trainer, now) is { } failure)
        {
            player?.SendMessage(failure);
            return false;
        }

        return first.PetFemale
            ? IssueTickets(player, first, player, second, now, focus)
            : IssueTickets(player, second, player, first, now, focus);
    }

    internal static bool TryPartnerBreed(PlayerMobile player, BaseCreature first, PlayerMobile partner,
        BaseCreature second, PetBreeder breeder, DateTime now)
    {
        if (CheckPartnerPair(player, first, partner, second, breeder, now) is { } failure)
        {
            SendToOwners(player, partner, failure);
            return false;
        }

        return IssueTickets(player, first, partner, second, now);
    }

    private static bool IssueTickets(PlayerMobile firstOwner, BaseCreature first, PlayerMobile secondOwner,
        BaseCreature second, DateTime now, PetTrainingStat? focus = null)
    {
        var sameOwner = firstOwner == secondOwner;
        _history.TryGetValue(firstOwner, out var firstHistory);
        _history.TryGetValue(secondOwner, out var secondHistory);
        if (firstHistory.AtLimit(now) || secondHistory.AtLimit(now))
        {
            SendToOwners(firstOwner, secondOwner, sameOwner ? "You can breed two pairs every 12 hours." :
                "An owner has already bred two pairs in the last 12 hours.");
            return false;
        }

        var firstPack = firstOwner.Backpack;
        var secondPack = secondOwner.Backpack;
        if (firstPack == null || secondPack == null)
        {
            SendToOwners(firstOwner, secondOwner, "Each owner needs a backpack for their claim ticket.");
            return false;
        }

        var claimAt = ClaimReadyAt(now);
        var firstTicket = new PetBreedingClaimTicket(firstOwner, first, second, claimAt, focus);
        var secondTicket = new PetBreedingClaimTicket(secondOwner, second, first, claimAt, focus);
        var roomForTickets = sameOwner
            ? firstPack.CheckHold(firstOwner, firstTicket, false, true, 1, secondTicket.PileWeight)
            : firstPack.CheckHold(firstOwner, firstTicket, false) && secondPack.CheckHold(secondOwner, secondTicket, false);
        if (!roomForTickets)
        {
            firstTicket.Delete();
            secondTicket.Delete();
            SendToOwners(firstOwner, secondOwner, sameOwner ? "Make room for both claim tickets." :
                "Each owner needs room for one claim ticket.");
            return false;
        }

        firstPack.DropItem(firstTicket);
        secondPack.DropItem(secondTicket);
        first.PetBreedingReadyAt = now + _parentDelay;
        second.PetBreedingReadyAt = now + _parentDelay;
        PetBreedingOuting.RecordLitter(first, second);
        _history[firstOwner] = new LitterHistory(firstHistory.Latest, now);
        if (!sameOwner)
        {
            _history[secondOwner] = new LitterHistory(secondHistory.Latest, now);
        }

        SendToOwners(firstOwner, secondOwner, sameOwner ? "Two claim tickets are in your backpack. Redeem them in three days." :
            "Your claim ticket is in your backpack. Redeem it in three days.");
        return true;
    }

    private static void SendToOwners(PlayerMobile first, PlayerMobile second, string message)
    {
        first?.SendMessage(message);
        if (second != first)
        {
            second?.SendMessage(message);
        }
    }
}
