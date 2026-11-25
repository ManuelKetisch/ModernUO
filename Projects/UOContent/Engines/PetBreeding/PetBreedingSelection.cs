using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Engines.PetBreeding;

internal static class PetBreedingSelection
{
    [Usage("PetBreed")]
    [Description("Select two pets to breed. Visit an animal trainer to select pets from your stables.")]
    internal static void Start(CommandEventArgs args)
    {
        if (args.Mobile is not PlayerMobile player || !player.CheckAlive() || !PetBreedingSystem.CanUseBreeding(player))
        {
            return;
        }

        Start(player, PetBreedingSystem.FindNearbyTrainer(player));
    }

    internal static void Start(PlayerMobile player, AnimalTrainer trainer, BaseCreature first = null)
    {
        if (first != null && PetBreedingSystem.CheckParent(player, first, trainer, Core.Now) is { } reason)
        {
            player.SendMessage(reason);
            return;
        }

        player.Target = null;
        player.CloseGump<StableSelectionGump>();
        player.CloseGump<ConfirmLitterGump>();
        PetBreedingOffer.CancelActiveFor(player);
        SelectParent(player, trainer, first);
    }

    private static void SelectParent(PlayerMobile player, AnimalTrainer trainer, BaseCreature first)
    {
        if (PetBreedingSystem.CanUseTrainer(player, trainer) && player.Stabled?.Count > 0)
        {
            StableSelectionGump.DisplayTo(player, trainer, first, 0);
        }
        else
        {
            TargetActivePet(player, trainer, first);
        }
    }

    private static void TargetActivePet(PlayerMobile player, AnimalTrainer trainer, BaseCreature first)
    {
        player.SendMessage(first == null ? "Choose the first parent." : "Choose the second parent.");
        player.Target = new ParentTarget(trainer, first);
    }

    private static void OnPetSelected(PlayerMobile player, AnimalTrainer trainer, BaseCreature first, BaseCreature pet)
    {
        var now = Core.Now;
        if (first == null)
        {
            if (PetBreedingSystem.CheckParent(player, pet, trainer, now) is { } failure)
            {
                player.SendMessage(failure);
                return;
            }

            SelectParent(player, trainer, pet);
        }
        else if (pet?.ControlMaster is PlayerMobile partner && partner != player)
        {
            var partnerFailure = PetBreedingSystem.CheckPartnerPair(player, first, partner, pet, trainer as PetBreeder, now);
            if (partnerFailure != null)
            {
                player.SendMessage(partnerFailure);
                return;
            }

            ConfirmLitterGump.DisplayTo(player, trainer, first, pet, partner);
        }
        else if (PetBreedingSystem.CheckPair(player, first, pet, trainer, now) is { } failure)
        {
            player.SendMessage(failure);
        }
        else
        {
            ConfirmLitterGump.DisplayTo(player, trainer, first, pet);
        }
    }

    private sealed class ParentTarget(AnimalTrainer trainer, BaseCreature first) :
        Target(PetBreedingSystem.PetRange, false, TargetFlags.None)
    {
        protected override void OnTarget(Mobile from, object targeted)
        {
            if (from is PlayerMobile player)
            {
                OnPetSelected(player, trainer, first, targeted as BaseCreature);
            }
        }
    }

    private sealed class StableSelectionGump : DynamicGump
    {
        private const int PageSize = 5;
        private const int TargetActive = 1;
        private const int PreviousPage = 2;
        private const int NextPage = 3;
        private const int PetButtonStart = 10;
        private const int LabelHue = 0x481;

        private readonly AnimalTrainer _trainer;
        private readonly BaseCreature _first;
        private readonly List<BaseCreature> _pets;
        private readonly int _page;
        private PlayerMobile _owner;

        public override bool Singleton => true;

        private StableSelectionGump(AnimalTrainer trainer, BaseCreature first, List<BaseCreature> pets, int page) :
            base(50, 50)
        {
            _trainer = trainer;
            _first = first;
            _pets = pets;
            _page = page;
        }

        internal static void DisplayTo(PlayerMobile player, AnimalTrainer trainer, BaseCreature first, int page)
        {
            if (player.NetState == null || !PetBreedingSystem.CanUseTrainer(player, trainer))
            {
                return;
            }

            var pets = new List<BaseCreature>();
            if (player.Stabled != null)
            {
                foreach (var entry in player.Stabled)
                {
                    if (entry is BaseCreature { Deleted: false, IsStabled: true } pet && pet.StabledBy == player)
                    {
                        pets.Add(pet);
                    }
                }
            }

            pets.Sort(static (left, right) => left.Serial.CompareTo(right.Serial));
            var lastPage = pets.Count == 0 ? 0 : (pets.Count - 1) / PageSize;
            var shownPage = System.Math.Clamp(page, 0, lastPage);
            var gump = new StableSelectionGump(trainer, first, pets, shownPage)
            {
                _owner = player
            };
            player.SendGump(gump);
        }

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();
            builder.AddBackground(0, 0, 465, 375, 0x13BE);
            builder.AddLabel(24, 16, LabelHue, _first == null ? "Choose the first parent" : "Choose the second parent");
            builder.AddButton(22, 46, 0xFA5, 0xFA7, TargetActive);
            builder.AddLabel(58, 46, LabelHue, "Target a nearby pet");

            var start = _page * PageSize;
            var count = System.Math.Min(PageSize, _pets.Count - start);
            if (count == 0)
            {
                builder.AddLabel(24, 83, LabelHue, "You have no pets in your stables.");
            }

            for (var i = 0; i < count; i++)
            {
                var pet = _pets[start + i];
                var sex = pet.PetFemale ? "Female" : "Male";
                var y = 78 + i * 50;
                var now = Core.Now;
                var reason = _first == null
                    ? PetBreedingSystem.CheckParent(_owner, pet, _trainer, now)
                    : PetBreedingSystem.CheckPair(_owner, _first, pet, _trainer, now);
                if (reason == null)
                {
                    builder.AddButton(22, y, 0xFA5, 0xFA7, PetButtonStart + i);
                }

                builder.AddLabelCropped(58, y, 375, 24, LabelHue,
                    $"{pet.Name}  ({sex}, level {pet.PetLevel}, generation {pet.PetGeneration})");
                if (reason != null)
                {
                    builder.AddLabelCropped(58, y + 21, 395, 24, 0x555, reason);
                }
            }

            if (_page > 0)
            {
                builder.AddButton(20, 340, 0xFAE, 0xFB0, PreviousPage);
                builder.AddLabel(58, 340, LabelHue, "Previous");
            }

            if (start + count < _pets.Count)
            {
                builder.AddButton(330, 340, 0xFA5, 0xFA7, NextPage);
                builder.AddLabel(368, 340, LabelHue, "Next");
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile is not PlayerMobile player || player != _owner || info.ButtonID == 0 ||
                !PetBreedingSystem.CanUseBreeding(player))
            {
                return;
            }

            if (info.ButtonID == TargetActive)
            {
                TargetActivePet(player, _trainer, _first);
                return;
            }

            if (!PetBreedingSystem.CanUseTrainer(player, _trainer))
            {
                player.SendMessage("Move closer to the animal trainer to choose a stabled pet.");
                return;
            }

            if (info.ButtonID is PreviousPage or NextPage)
            {
                DisplayTo(player, _trainer, _first, _page + (info.ButtonID == NextPage ? 1 : -1));
                return;
            }

            var row = info.ButtonID - PetButtonStart;
            var index = _page * PageSize + row;
            if (row is >= 0 and < PageSize && index < _pets.Count)
            {
                var pet = _pets[index];
                if (!pet.IsStabled)
                {
                    player.SendMessage("That pet is no longer in your stables.");
                    return;
                }

                OnPetSelected(player, _trainer, _first, pet);
            }
        }
    }

    private sealed class ConfirmLitterGump(
        PlayerMobile owner, AnimalTrainer trainer, BaseCreature first, BaseCreature second, PlayerMobile partner,
        PetTrainingStat? focus, bool chooseFocus
    ) : DynamicGump(50, 50)
    {
        private const int LabelHue = 0x481;
        private const int BreedButton = 1;
        private const int FocusButton = 2;
        private const int ClearFocusButton = 3;
        private const int BackButton = 4;
        private const int FirstStatButton = 10;

        private readonly bool _firstWasStabled = first.IsStabled;
        private readonly bool _secondWasStabled = second.IsStabled;
        private readonly PetBreedingPreview _preview = PetBreedingPreview.Create(first, second, focus);
        private readonly DateTime _projectedClaim = PetBreedingSystem.ClaimReadyAt(Core.Now);

        public override bool Singleton => true;

        internal static void DisplayTo(PlayerMobile player, AnimalTrainer trainer, BaseCreature first,
            BaseCreature second, PlayerMobile partner = null, PetTrainingStat? focus = null, bool chooseFocus = false)
        {
            if (player.NetState != null)
            {
                player.SendGump(new ConfirmLitterGump(player, trainer, first, second, partner, focus, chooseFocus));
            }
        }

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();
            if (chooseFocus && partner == null)
            {
                BuildFocusPicker(ref builder);
                return;
            }

            builder.AddBackground(0, 0, 455, partner == null ? 321 : 271, 0x13BE);
            if (partner == null)
            {
                builder.AddLabel(24, 16, LabelHue, "Breed these pets?");
            }
            else
            {
                builder.AddLabelCropped(24, 16, 348, 23, LabelHue, $"Offer to breed with {partner.Name}?");
            }

            builder.AddLabelCropped(24, 42, 348, 23, LabelHue, $"{_preview.FirstName} and {_preview.SecondName}");
            builder.AddLabel(24, 68, LabelHue, $"Projected claim: {_projectedClaim:yyyy-MM-dd HH:mm:ss} UTC");
            builder.AddLabel(24, 94, LabelHue, "Each parent then rests for six days.");
            var forecast = _preview.Forecast;
            builder.AddLabel(24, 123, LabelHue,
                $"Offspring generations {forecast.FirstGeneration} and {forecast.SecondGeneration}");
            builder.AddLabel(24, 146, LabelHue,
                $"Maximum level {forecast.MinimumLevel}–{forecast.MaximumLevel}  •  Follower slots {forecast.FollowerSlots}");
            if (_preview.Focus is { } preview)
            {
                builder.AddLabel(24, 169, LabelHue, $"Selected stat on each parent: {preview.First} and {preview.Second}");
            }
            else
            {
                var (hitsLow, hitsHigh) = _preview.Hits;
                var (strLow, strHigh) = _preview.Strength;
                builder.AddLabel(24, 169, LabelHue, $"Hit points {hitsLow}–{hitsHigh}  •  Strength {strLow}–{strHigh}");
            }

            if (partner == null)
            {
                builder.AddButton(22, 200, 0xFA5, 0xFA7, FocusButton);
                if (_preview.Focus is { } selected)
                {
                    var name = PetBreedingFocus.Stats[PetBreedingFocus.IndexOf(focus)].Label;
                    builder.AddLabel(58, 200, LabelHue, $"Favor {name.ToLowerInvariant()}   {selected.Minimum}–{selected.Maximum}");
                }
                else
                {
                    builder.AddLabel(58, 200, LabelHue, "Favor one inherited stat");
                }

                builder.AddLabel(24, 233, LabelHue, "A focus moves bonus points between stats.");
            }

            var breedY = partner == null ? 267 : 224;
            builder.AddButton(22, breedY, 0xFA5, 0xFA7, BreedButton);
            builder.AddLabel(58, breedY, LabelHue, partner == null ? "Breed" : "Send offer");
        }

        private void BuildFocusPicker(ref DynamicGumpBuilder builder)
        {
            builder.AddBackground(0, 0, 535, 480, 0x13BE);
            builder.AddLabel(24, 17, LabelHue, "Choose one stat to favor");
            builder.AddLabel(24, 44, LabelHue,
                $"Up to {PetBreedingFocus.MaxTransferredBonus} rolled bonus points can move to the favored stat.");
            builder.AddLabel(57, 80, LabelHue, "Stat");
            builder.AddLabelCropped(233, 80, 86, 24, LabelHue, first.Name ?? "First parent");
            builder.AddLabelCropped(323, 80, 86, 24, LabelHue, second.Name ?? "Second parent");
            builder.AddLabel(421, 80, LabelHue, "Range if favored");

            var stats = PetBreedingFocus.Stats;
            for (var i = 0; i < stats.Length; i++)
            {
                var (stat, name) = stats[i];
                var preview = PetBreedingFocus.GetPreview(first, second, stat);
                var y = 106 + i * 24;
                builder.AddButton(22, y, 0xFA5, 0xFA7, FirstStatButton + i);
                builder.AddLabel(57, y, LabelHue, name);
                builder.AddLabel(233, y, LabelHue, $"{preview.First}");
                builder.AddLabel(323, y, LabelHue, $"{preview.Second}");
                builder.AddLabel(421, y, LabelHue, $"{preview.Minimum}–{preview.Maximum}");
            }

            builder.AddButton(22, 432, 0xFA5, 0xFA7, ClearFocusButton);
            builder.AddLabel(57, 432, LabelHue, "No favored stat");
            builder.AddButton(302, 432, 0xFAE, 0xFB0, BackButton);
            builder.AddLabel(337, 432, LabelHue, "Back");
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (info.ButtonID == 0 || sender.Mobile != owner)
            {
                return;
            }

            if (_firstWasStabled && !first.IsStabled || _secondWasStabled && !second.IsStabled)
            {
                owner.SendMessage("A selected pet left your stables. Choose the parents again.");
                return;
            }

            if (partner != null)
            {
                if (info.ButtonID == BreedButton)
                {
                    var breeder = (PetBreeder)trainer;
                    if (PetBreedingSystem.CheckPartnerPair(owner, first, partner, second, breeder, Core.Now) is { } reason)
                    {
                        owner.SendMessage(reason);
                    }
                    else if (PreviewStillMatches())
                    {
                        PetBreedingOffer.Begin(owner, first, partner, second, breeder);
                    }
                }

                return;
            }

            if (PetBreedingSystem.CheckPair(owner, first, second, trainer, Core.Now) is { } failure)
            {
                owner.SendMessage(failure);
                return;
            }

            if (chooseFocus)
            {
                var index = info.ButtonID - FirstStatButton;
                var stats = PetBreedingFocus.Stats;
                if (index >= 0 && index < stats.Length)
                {
                    DisplayTo(owner, trainer, first, second, focus: stats[index].Stat);
                }
                else if (info.ButtonID is ClearFocusButton or BackButton)
                {
                    DisplayTo(owner, trainer, first, second, focus: info.ButtonID == BackButton ? focus : null);
                }

                return;
            }

            if (info.ButtonID == FocusButton)
            {
                DisplayTo(owner, trainer, first, second, focus: focus, chooseFocus: true);
            }
            else if (info.ButtonID == BreedButton && PreviewStillMatches())
            {
                PetBreedingSystem.TryBreed(owner, first, second, trainer, Core.Now, focus);
            }
        }

        private bool PreviewStillMatches()
        {
            if (_preview == PetBreedingPreview.Create(first, second, focus))
            {
                return true;
            }

            owner.SendMessage("A parent changed. Review the updated breeding preview.");
            DisplayTo(owner, trainer, first, second, partner, focus);
            return false;
        }
    }
}

internal readonly record struct PetBreedingPreview(
    string FirstName, string SecondName, PetNurseryForecast Forecast, (int Min, int Max) Hits,
    (int Min, int Max) Strength, PetBreedingFocus.Preview? Focus
)
{
    internal static PetBreedingPreview Create(BaseCreature first, BaseCreature second, PetTrainingStat? focus = null)
    {
        var forecast = PetNurseryForecast.Create(first, second);
        return focus is { } stat
            ? new PetBreedingPreview(first.Name, second.Name, forecast, default, default,
                PetBreedingFocus.GetPreview(first, second, stat))
            : new PetBreedingPreview(first.Name, second.Name, forecast,
                PetNurseryForecast.GetStatRange(first, second, PetTrainingStat.Hits),
                PetNurseryForecast.GetStatRange(first, second, PetTrainingStat.Strength), null);
    }
}
