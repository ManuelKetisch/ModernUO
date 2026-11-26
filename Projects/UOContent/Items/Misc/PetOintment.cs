using ModernUO.Serialization;
using Server.Engines.PetProgression;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Items;

public enum PetOintmentKind
{
    Unmixed,
    Hits,
    Stamina,
    Mana,
    Strength,
    Dexterity,
    Intelligence,
    PhysicalResistance,
    FireResistance,
    ColdResistance,
    PoisonResistance,
    EnergyResistance,
    Armor
}

[SerializationGenerator(0)]
public partial class PetOintment : Item
{
    [SerializableField(0, setter: "private")]
    [InvalidateProperties]
    private PetOintmentKind _kind;

    [Constructible]
    public PetOintment() : base(0x0E24) => Hue = 0x48E;

    public override string DefaultName => "pet ointment";
    public override double DefaultWeight => ItemWeights.SmallSupply;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        if (Kind != PetOintmentKind.Unmixed)
        {
            list.Add($"{"Boost"}\t{StatName(Kind)}");
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile player || !CanUse(player))
        {
            return;
        }

        player.SendMessage(Kind == PetOintmentKind.Unmixed
            ? "Choose an ingredient in your backpack or a pet to heal."
            : "Choose a pet to apply the ointment.");
        player.BeginTarget(PetCare.Range, false, TargetFlags.None, (mobile, target) =>
        {
            if (mobile is PlayerMobile owner)
            {
                if (target is Item ingredient)
                {
                    TryMix(owner, ingredient);
                }
                else
                {
                    TryApply(owner, target as BaseCreature);
                }
            }
        });
    }

    internal bool TryMix(PlayerMobile player, Item ingredient)
    {
        if (!CanUse(player) || Kind != PetOintmentKind.Unmixed ||
            ingredient is not { Deleted: false } || !ingredient.IsChildOf(player.Backpack) ||
            !ingredient.IsAccessibleTo(player))
        {
            return false;
        }

        var (kind, amount) = ingredient switch
        {
            Ginseng => (PetOintmentKind.Hits, 140),
            BlackPearl => (PetOintmentKind.Stamina, 140),
            EtherealPowder or PowderOfTranslocation => (PetOintmentKind.Mana, 1),
            MandrakeRoot => (PetOintmentKind.Strength, 140),
            Bloodmoss => (PetOintmentKind.Dexterity, 90),
            SpidersSilk => (PetOintmentKind.Intelligence, 120),
            PigIron => (PetOintmentKind.PhysicalResistance, 100),
            BaseBeverage { Content: BeverageType.Water, IsEmpty: false } => (PetOintmentKind.FireResistance, 1),
            Beeswax => (PetOintmentKind.ColdResistance, 40),
            OrangePetals => (PetOintmentKind.PoisonResistance, 1),
            NoxCrystal => (PetOintmentKind.EnergyResistance, 110),
            GreenThorns => (PetOintmentKind.Armor, 1),
            _ => (PetOintmentKind.Unmixed, 0)
        };

        if (kind == PetOintmentKind.Unmixed)
        {
            player.SendMessage("That ingredient cannot be mixed with pet ointment.");
            return false;
        }

        if (ingredient is BaseBeverage beverage)
        {
            beverage.Quantity--;
        }
        else if (!player.Backpack.ConsumeTotal(ingredient.GetType(), amount))
        {
            player.SendMessage($"You need {amount} of that ingredient.");
            return false;
        }

        Kind = kind;
        player.SendMessage($"You mix ointment for {StatName(kind):L}.");
        return true;
    }

    internal bool TryApply(PlayerMobile player, BaseCreature pet)
    {
        if (!CanUse(player) || !PetCare.CanReachOwnedPet(player, pet) || !pet.Alive || pet.IsDeadPet)
        {
            player.SendMessage("Choose a living pet of yours within reach.");
            return false;
        }

        if (Kind == PetOintmentKind.Unmixed)
        {
            if (pet.Hits >= pet.HitsMax)
            {
                player.SendMessage("Your pet is already at full health.");
                return false;
            }

            var previousHits = pet.Hits;
            pet.Heal(System.Math.Max(1, pet.HitsMax / 10), player);
            if (pet.Hits <= previousHits)
            {
                return false;
            }

            player.SendMessage("The ointment heals some of your pet's wounds.");
        }
        else if (!pet.TryApplyMixedOintment(Kind))
        {
            player.SendMessage("That ointment cannot improve this pet.");
            return false;
        }
        else
        {
            player.SendMessage($"The ointment improves your pet's {StatName(Kind):L}.");
        }

        Delete();
        return true;
    }

    private bool CanUse(PlayerMobile player) =>
        !Deleted && player is { Alive: true, Deleted: false, Backpack: not null } &&
        IsChildOf(player.Backpack) && IsAccessibleTo(player);

    private static string StatName(PetOintmentKind kind) => kind switch
    {
        PetOintmentKind.Hits => "Hit points",
        PetOintmentKind.Stamina => "Stamina",
        PetOintmentKind.Mana => "Mana",
        PetOintmentKind.Strength => "Strength",
        PetOintmentKind.Dexterity => "Dexterity",
        PetOintmentKind.Intelligence => "Intelligence",
        PetOintmentKind.PhysicalResistance => "Physical resistance",
        PetOintmentKind.FireResistance => "Fire resistance",
        PetOintmentKind.ColdResistance => "Cold resistance",
        PetOintmentKind.PoisonResistance => "Poison resistance",
        PetOintmentKind.EnergyResistance => "Energy resistance",
        PetOintmentKind.Armor => "Armor",
        _ => string.Empty
    };
}
