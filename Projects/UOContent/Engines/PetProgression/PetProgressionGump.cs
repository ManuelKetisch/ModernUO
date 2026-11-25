using Server.Engines.PetBreeding;
using Server.Gumps;
using Server.Network;

namespace Server.Mobiles;

internal sealed class PetProgressionGump(BaseCreature pet) : DynamicGump(80, 70)
{
    private const int TextHue = 0x480;
    private const int MutedHue = 0x555;
    private const int BreedButton = 101;
    private const int PauseButton = 102;
    private const int CombatRoleButton = 103;

    private static readonly (PetTrainingStat Stat, string Label)[] _stats =
    [
        (PetTrainingStat.Hits, "Hit points"),
        (PetTrainingStat.Stamina, "Stamina"),
        (PetTrainingStat.Mana, "Mana"),
        (PetTrainingStat.PhysicalResistance, "Physical resist"),
        (PetTrainingStat.FireResistance, "Fire resist"),
        (PetTrainingStat.ColdResistance, "Cold resist"),
        (PetTrainingStat.PoisonResistance, "Poison resist"),
        (PetTrainingStat.EnergyResistance, "Energy resist"),
        (PetTrainingStat.MinimumDamage, "Minimum damage"),
        (PetTrainingStat.MaximumDamage, "Maximum damage"),
        (PetTrainingStat.Armor, "Armor"),
        (PetTrainingStat.Strength, "Strength"),
        (PetTrainingStat.Dexterity, "Dexterity"),
        (PetTrainingStat.Intelligence, "Intelligence")
    ];

    public override bool Singleton => true;

    public static void DisplayTo(Mobile from, BaseCreature pet)
    {
        if (from?.NetState == null || !pet.CanManagePetProgression(from))
        {
            return;
        }

        from.SendGump(new PetProgressionGump(pet));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 650, 407, 9270);
        builder.AddAlphaRegion(12, 12, 626, 383);

        var sex = pet.PetFemale ? "Female" : "Male";
        builder.AddLabel(24, 18, TextHue, $"{pet.Name} — Pet training");
        builder.AddLabel(24, 44, TextHue, $"Level {pet.PetLevel} / {pet.PetMaxLevel}    Generation {pet.PetGeneration}    {sex}");
        builder.AddLabel(366, 44, TextHue, $"Ability points: {pet.PetAbilityPoints}");

        var next = pet.PetExperienceForNextLevel;
        if (next == 0 && pet.CanPausePetAtBreedingLimit && pet.PetBreedingPauseEnabled)
        {
            builder.AddLabel(24, 66, TextHue, $"Experience paused at level {pet.PetLevel}");
        }
        else if (next > 0)
        {
            builder.AddLabel(24, 66, TextHue, $"Experience: {pet.PetExperience} / {next}");
        }
        else
        {
            builder.AddLabel(24, 66, TextHue, "Maximum level reached");
        }

        builder.AddLabel(24, 95, TextHue, "Attribute");
        builder.AddLabel(152, 95, TextHue, "Value");
        builder.AddLabel(224, 95, TextHue, "+1");
        builder.AddLabel(259, 95, TextHue, "+10");
        builder.AddLabel(337, 95, TextHue, "Attribute");
        builder.AddLabel(465, 95, TextHue, "Value");
        builder.AddLabel(537, 95, TextHue, "+1");
        builder.AddLabel(572, 95, TextHue, "+10");

        for (var i = 0; i < _stats.Length; i++)
        {
            var (stat, label) = _stats[i];
            var (value, cap, unlock) = pet.GetPetTrainingStat(stat);
            var x = 24 + i / 7 * 313;
            var y = 118 + i % 7 * 27;
            var locked = pet.PetLevel < unlock;
            builder.AddLabel(x, y, locked ? MutedHue : TextHue, label);

            if (locked)
            {
                builder.AddLabel(x + 128, y, MutedHue, $"Level {unlock}");
                continue;
            }

            var available = value < cap && pet.PetAbilityPoints > 0;
            builder.AddLabel(x + 128, y, TextHue, $"{value}/{cap}");
            if (available)
            {
                builder.AddButton(x + 200, y, 4005, 4007, i * 2 + 1);
                builder.AddButton(x + 235, y, 4005, 4007, i * 2 + 2);
            }
        }

        if (pet.PetLevel >= BaseCreature.PetCombatRoleLevel)
        {
            builder.AddButton(24, 369, 4005, 4007, CombatRoleButton);
            if (pet.PetRole == PetCombatRole.None)
            {
                builder.AddLabel(59, 369, TextHue, "Choose a combat role");
            }
            else
            {
                builder.AddLabel(59, 369, TextHue, $"Combat role: {pet.PetRole}");
            }
        }
        else
        {
            builder.AddLabel(24, 369, MutedHue, $"Combat role at level {BaseCreature.PetCombatRoleLevel}");
        }

        if (!pet.Tamable)
        {
            return;
        }

        var now = Core.Now;
        if (pet.PetBreedingReadyAt > now)
        {
            builder.AddLabel(24, 311, TextHue, $"Breeding ready {pet.PetBreedingReadyAt:yyyy-MM-dd HH:mm} UTC");
        }

        if (CanBreed(pet.ControlMaster))
        {
            builder.AddButton(24, 337, 4005, 4007, BreedButton);
            builder.AddLabel(59, 337, TextHue, "Breed this pet");
        }

        if (pet.CanPausePetAtBreedingLimit)
        {
            builder.AddButton(337, 337, 4005, 4007, PauseButton);
            var pauseLabel = !pet.PetBreedingPauseEnabled ? "Pause at level 42" :
                pet.PetLevel == BaseCreature.LastBreedingLevel ? "Resume experience" : "Remove level 42 pause";
            builder.AddLabel(372, 337, TextHue, pauseLabel);
        }
    }

    private bool CanBreed(Mobile from) =>
        from is PlayerMobile player && pet.Tamable && pet.CanManagePetProgression(from) &&
        PetBreedingSystem.CheckParent(player, pet, null, Core.Now) == null;

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;
        if (!pet.CanManagePetProgression(from))
        {
            from?.SendMessage("Move closer to your pet to manage its training.");
            return;
        }

        switch (info.ButtonID)
        {
            case BreedButton when from is PlayerMobile player && CanBreed(player):
                PetBreedingSelection.Start(player, PetBreedingSystem.FindNearbyTrainer(player), pet);
                return;
            case CombatRoleButton when pet.PetLevel >= BaseCreature.PetCombatRoleLevel:
                PetCombatRoleGump.DisplayTo(from, pet);
                return;
            case PauseButton when pet.CanPausePetAtBreedingLimit:
                pet.TrySetPetBreedingPause(from, !pet.PetBreedingPauseEnabled);
                DisplayTo(from, pet);
                return;
        }

        if (info.ButtonID < 1 || info.ButtonID > _stats.Length * 2)
        {
            return;
        }

        var index = info.ButtonID - 1;
        var (stat, label) = _stats[index / 2];
        var amount = index % 2 == 0 ? 1 : 10;
        if (PetAbilitySpendQuote.TryCreate(pet, from, stat, amount, out var quote))
        {
            from.SendGump(new ConfirmSpendGump(pet, from, label, quote));
        }
        else
        {
            DisplayTo(from, pet);
        }
    }

    internal sealed class ConfirmSpendGump(
        BaseCreature pet, Mobile owner, string label, PetAbilitySpendQuote quote
    ) : DynamicGump(90, 85)
    {
        public override bool Singleton => true;

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();
            builder.AddBackground(0, 0, 355, 160, 9270);
            builder.AddAlphaRegion(12, 12, 331, 136);
            builder.AddLabel(24, 19, TextHue, $"Raise {label.ToLowerInvariant()}");
            builder.AddLabel(24, 47, TextHue, $"{quote.Current} → {quote.Result}   (cap {quote.Cap})");
            builder.AddLabel(24, 75, TextHue, $"Spend {quote.Cost} of {quote.PointsBefore} ability points");
            builder.AddButton(24, 110, 4005, 4007, 1);
            builder.AddLabel(59, 110, TextHue, "Apply");
            builder.AddButton(178, 110, 4017, 4019, 0);
            builder.AddLabel(214, 110, TextHue, "Back");
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile != owner)
            {
                return;
            }

            if (info.ButtonID == 1)
            {
                if (PetAbilitySpendQuote.TryCreate(pet, owner, quote.Stat, quote.Requested, out var current) &&
                    current == quote)
                {
                    pet.TrySpendPetAbilityPoints(owner, quote.Stat, quote.Requested);
                }
                else
                {
                    owner.SendMessage("Your pet's training changed. Review the new values.");
                }
            }

            DisplayTo(owner, pet);
        }
    }
}
