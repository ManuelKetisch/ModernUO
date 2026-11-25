using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Engines.PetBreeding;

internal readonly record struct PetNurseryForecast(
    int FirstGeneration, int SecondGeneration, int MinimumLevel, int MaximumLevel, int FollowerSlots
)
{
    internal static PetNurseryForecast Create(BaseCreature first, BaseCreature second)
    {
        var (minimum, maximum) = PetBreedingTraits.GetOffspringMaximumLevelRange(first, second);
        return new PetNurseryForecast(first.PetGeneration + 1, second.PetGeneration + 1,
            minimum, maximum, Math.Max(first.ControlSlots, second.ControlSlots));
    }

    internal static (int Min, int Max) GetStatRange(BaseCreature first, BaseCreature second, PetTrainingStat stat)
    {
        var range = PetBreedingTraits.GetStatInheritanceRange(first, second, stat);
        if (stat == PetTrainingStat.MaximumDamage)
        {
            var minimumDamage = PetBreedingTraits.GetStatInheritanceRange(first, second, PetTrainingStat.MinimumDamage);
            return (Math.Max(range.Min, minimumDamage.Min), Math.Max(range.Max, minimumDamage.Max));
        }

        return range;
    }
}

public static class PetNursery
{
    public static void Initialize() => CommandSystem.Register("PetNursery", AccessLevel.Player, OpenCommand);

    [Usage("PetNursery")]
    [Description("View parent information or start a breeding outing.")]
    private static void OpenCommand(CommandEventArgs args)
    {
        if (args.Mobile is PlayerMobile player)
        {
            DisplayTo(player);
        }
    }

    internal static void DisplayTo(PlayerMobile player, AnimalTrainer trainer = null)
    {
        NurseryGump.DisplayTo(player, trainer ?? PetBreedingSystem.FindNearbyTrainer(player), null, 0);
    }

    private sealed class NurseryGump(
        PlayerMobile owner, AnimalTrainer trainer, BaseCreature pet, List<BaseCreature> stabled, int page
    ) : DynamicGump(55, 55)
    {
        private const int LabelHue = 0x481;
        private const int ActivePet = 1;
        private const int BeginOuting = 2;
        private const int BreedPets = 3;
        private const int Previous = 4;
        private const int Next = 5;
        private const int StableButton = 10;
        private const int PageSize = 5;

        public override bool Singleton => true;

        internal static void DisplayTo(PlayerMobile player, AnimalTrainer currentTrainer, BaseCreature pet, int page)
        {
            if (player.NetState == null || !PetBreedingSystem.CanUseBreeding(player))
            {
                return;
            }

            if (pet != null && !CanInspect(player, currentTrainer, pet))
            {
                pet = null;
            }

            var stabled = new List<BaseCreature>();
            if (PetBreedingSystem.CanUseTrainer(player, currentTrainer) && player.Stabled != null)
            {
                foreach (var candidate in player.Stabled)
                {
                    if (candidate is BaseCreature creature && CanInspect(player, currentTrainer, creature))
                    {
                        stabled.Add(creature);
                    }
                }
            }

            var lastPage = stabled.Count == 0 ? 0 : (stabled.Count - 1) / PageSize;
            player.SendGump(new NurseryGump(player, currentTrainer, pet, stabled, Math.Clamp(page, 0, lastPage)));
        }

        private static bool CanInspect(PlayerMobile player, AnimalTrainer trainer, BaseCreature candidate)
        {
            if (candidate is not { Alive: true, Deleted: false, Tamable: true, Summoned: false, IsDeadPet: false })
            {
                return false;
            }

            return candidate.IsStabled
                ? PetBreedingSystem.CanUseTrainer(player, trainer) && candidate.Map == Map.Internal &&
                  candidate.StabledBy == player && player.Stabled?.Contains(candidate) == true
                : candidate.CanManagePetProgression(player);
        }

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();
            builder.AddBackground(0, 0, 430, 432, 0x13BE);
            builder.AddLabel(22, 14, LabelHue, "Pet nursery");
            AddAction(ref builder, 22, 43, ActivePet, "Inspect a nearby pet");
            AddAction(ref builder, 229, 43, BreedPets, "Choose parents");
            builder.AddLabel(22, 78, LabelHue, "Stabled pets");
            var start = page * PageSize;
            var count = Math.Min(PageSize, stabled.Count - start);
            for (var i = 0; i < count; i++)
            {
                var candidate = stabled[start + i];
                var y = 103 + i * 25;
                builder.AddButton(22, y, 0xFA5, 0xFA7, StableButton + i);
                builder.AddLabelCropped(56, y, 345, 23, LabelHue,
                    $"{candidate.Name}  Level {candidate.PetLevel}  Generation {candidate.PetGeneration}");
            }

            if (count == 0)
            {
                builder.AddLabel(22, 104, LabelHue, "Visit an animal trainer to inspect stabled pets.");
            }

            if (page > 0)
            {
                AddAction(ref builder, 22, 228, Previous, "Previous");
            }

            if (start + count < stabled.Count)
            {
                AddAction(ref builder, 230, 228, Next, "Next");
            }

            if (pet == null)
            {
                builder.AddLabel(22, 276, LabelHue, "Inspect a pet to see when it can breed again.");
                return;
            }

            var now = Core.Now;
            var sex = pet.PetFemale ? "Female" : "Male";
            builder.AddLabelCropped(22, 260, 370, 24, LabelHue, $"{pet.Name}  {sex}  Generation {pet.PetGeneration}");
            builder.AddLabel(22, 284, LabelHue, $"Level {pet.PetLevel} / {pet.PetMaxLevel}    Follower slots: {pet.ControlSlots}");

            if (pet.PetBreedingReadyAt > now)
            {
                builder.AddLabel(22, 308, LabelHue, $"Can breed after {pet.PetBreedingReadyAt:u}");
                var (completed, required, active, completedForLitter) = PetBreedingOuting.GetStatus(owner, pet, now);
                if (active)
                {
                    builder.AddLabel(22, 335, LabelHue, $"Outing: {completed} of {required} creature families defeated");
                }
                else if (completedForLitter)
                {
                    builder.AddLabel(22, 335, LabelHue, "Outing complete for this litter.");
                }
                else if (!pet.IsStabled && PetBreedingSystem.CanUseTrainer(owner, trainer))
                {
                    AddAction(ref builder, 22, 333, BeginOuting, "Begin a breeding outing");
                }
            }
            else
            {
                builder.AddLabel(22, 308, LabelHue, "No breeding recovery time remaining.");
            }

            if (PetBreedingRecords.GetPedigree(pet) is { } pedigree)
            {
                builder.AddLabelCropped(22, 367, 380, 24, LabelHue,
                    $"Dam {pedigree.DamName}  Level {pedigree.DamLevel}  Generation {pedigree.DamGeneration}");
                builder.AddLabelCropped(22, 391, 380, 24, LabelHue,
                    $"Sire {pedigree.SireName}  Level {pedigree.SireLevel}  Generation {pedigree.SireGeneration}");
            }
        }

        private static void AddAction(ref DynamicGumpBuilder builder, int x, int y, int id, string label)
        {
            builder.AddButton(x, y, 0xFA5, 0xFA7, id);
            builder.AddLabel(x + 35, y, LabelHue, label);
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile != owner || !PetBreedingSystem.CanUseBreeding(owner))
            {
                return;
            }

            switch (info.ButtonID)
            {
                case ActivePet:
                    owner.SendMessage("Choose a nearby pet of yours.");
                    owner.BeginTarget(BaseCreature.PetInteractionRange, false, TargetFlags.None,
                        (from, target) => Inspect(target as BaseCreature));
                    break;
                case BeginOuting:
                    if (pet != null && CanInspect(owner, trainer, pet) &&
                        PetBreedingOuting.TryStart(owner, pet, trainer, Core.Now))
                    {
                        DisplayTo(owner, trainer, pet, page);
                    }

                    break;
                case BreedPets:
                    PetBreedingSelection.Start(owner, PetBreedingSystem.FindNearbyTrainer(owner));
                    break;
                case Previous:
                case Next:
                    DisplayTo(owner, trainer, pet, page + (info.ButtonID == Next ? 1 : -1));
                    break;
                case >= StableButton and < StableButton + PageSize:
                    var index = page * PageSize + info.ButtonID - StableButton;
                    if (index < stabled.Count)
                    {
                        Inspect(stabled[index]);
                    }

                    break;
            }
        }

        private void Inspect(BaseCreature candidate)
        {
            if (!CanInspect(owner, trainer, candidate))
            {
                owner.SendMessage("Choose one of your living pets or visit an animal trainer.");
                return;
            }

            DisplayTo(owner, trainer, candidate, page);
        }
    }
}
