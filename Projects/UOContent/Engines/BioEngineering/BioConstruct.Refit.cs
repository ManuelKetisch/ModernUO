using System;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.BioEngineering;

internal enum BioFrameAlloy
{
    Iron,
    Bronze,
    Agapite,
    ShadowIron
}

internal enum BioRefitChange
{
    IronFrame = 1,
    BronzeFrame,
    AgapiteFrame,
    ShadowIronFrame,
    InstallCircuit,
    RemoveCircuit,
    InstallRegulator,
    RemoveRegulator
}

internal readonly record struct BioRefitState(BioFrameAlloy Frame, bool FocusedCircuit, bool StaminaRegulator);

internal readonly record struct BioRefitQuote(
    BioConstruct Construct, PlayerMobile Owner, int Revision, BioRefitChange Change, BioRefitState Result
);

public partial class BioConstruct
{
    private const int FrameIngots = 20;
    private const int ModuleIngots = 15;
    private const int BronzeHealing = 8;
    private const int AgapiteMana = 4;
    private const int ShadowIronDrain = 8;
    private const int RegulatorStamina = 5;
    private const int CircuitStaminaCost = 9;
    private const int FrameCooldownSeconds = 12;
    private const int RegulatorCooldownSeconds = 18;
    private const int CircuitCooldownSeconds = 14;
    private const int RecentCombatRange = 12;

    private static readonly TimeSpan _frameCooldown = TimeSpan.FromSeconds(FrameCooldownSeconds);
    private static readonly TimeSpan _utilityCooldown = TimeSpan.FromSeconds(RegulatorCooldownSeconds);
    private static readonly TimeSpan _circuitCooldown = TimeSpan.FromSeconds(CircuitCooldownSeconds);
    private static readonly TimeSpan _recentCombatDuration = TimeSpan.FromSeconds(10);

    private int _refitRevision;

    internal BioRefitState RefitState => new(FrameAlloy, FocusedCircuit, StaminaRegulator);

    private bool HasRefittedFrame() => _frameAlloy != BioFrameAlloy.Iron;
    private bool HasFocusedCircuit() => _focusedCircuit;
    private bool HasStaminaRegulator() => _staminaRegulator;
    private bool HasFrameCooldown() => _nextFrameAt != default;
    private bool HasUtilityCooldown() => _nextUtilityAt != default;

    internal static bool CanUseRefitBench(PlayerMobile owner)
    {
        if (owner is not { Deleted: false, Alive: true, Map: not null } || owner.Map == Map.Internal)
        {
            return false;
        }

        if (!BioEngineeringBook.CheckTraining(owner))
        {
            return false;
        }

        DefBlacksmithy.CheckAnvilAndForge(owner, 2, out var anvil, out var forge);
        if (!anvil || !forge)
        {
            owner.SendMessage("Stand by an anvil and a forge to refit a bio construct.");
            return false;
        }

        return true;
    }

    internal bool CanRefit(PlayerMobile owner)
    {
        if (!CanManage(owner))
        {
            owner?.SendMessage("Stand beside your living, assembled bio construct.");
            return false;
        }

        if (Mounted || HasActiveCombat(this) || HasActiveCombat(owner))
        {
            owner.SendMessage("Dismount and leave combat before refitting your construct.");
            return false;
        }

        return CanUseRefitBench(owner);
    }

    private static bool HasActiveCombat(Mobile mobile)
    {
        if (mobile.Combatant != null)
        {
            return true;
        }

        var cutoff = Core.Now - _recentCombatDuration;
        return mobile.Aggressors.Exists(info => info.LastCombatTime >= cutoff && IsNearbyFoe(info.Attacker, mobile)) ||
               mobile.Aggressed.Exists(info => info.LastCombatTime >= cutoff && IsNearbyFoe(info.Defender, mobile));
    }

    private static bool IsNearbyFoe(Mobile foe, Mobile mobile) =>
        foe is { Alive: true, Deleted: false } && foe.Map == mobile.Map && mobile.InRange(foe, RecentCombatRange);

    internal BioRefitQuote? QuoteRefit(PlayerMobile owner, BioRefitChange change)
    {
        var current = RefitState;
        var result = change switch
        {
            BioRefitChange.IronFrame                         => current with { Frame = BioFrameAlloy.Iron },
            BioRefitChange.BronzeFrame                       => current with { Frame = BioFrameAlloy.Bronze },
            BioRefitChange.AgapiteFrame                      => current with { Frame = BioFrameAlloy.Agapite },
            BioRefitChange.ShadowIronFrame                   => current with { Frame = BioFrameAlloy.ShadowIron },
            BioRefitChange.InstallCircuit when Trait != BioTrait.None => current with { FocusedCircuit = true },
            BioRefitChange.RemoveCircuit                    => current with { FocusedCircuit = false },
            BioRefitChange.InstallRegulator                  => current with { StaminaRegulator = true },
            BioRefitChange.RemoveRegulator                   => current with { StaminaRegulator = false },
            _                                                => current
        };

        return result == current ? null : new BioRefitQuote(this, owner, _refitRevision, change, result);
    }

    internal bool TryRefit(PlayerMobile owner, BioRefitQuote quote)
    {
        if (!CanRefit(owner))
        {
            return false;
        }

        if (QuoteRefit(owner, quote.Change) != quote)
        {
            owner.SendMessage("The refit choice has changed. Review it again.");
            return false;
        }

        var (type, amount, label) = RefitCost(quote.Change);
        if (amount > 0 && owner.Backpack?.ConsumeTotal(type, amount) != true)
        {
            owner.SendMessage($"Put {amount} {label} in your backpack.");
            return false;
        }

        FrameAlloy = quote.Result.Frame;
        FocusedCircuit = quote.Result.FocusedCircuit;
        StaminaRegulator = quote.Result.StaminaRegulator;
        _refitRevision++;
        InvalidateProperties();
        return true;
    }

    internal static (Type Type, int Amount, string Label) RefitCost(BioRefitChange change) => change switch
    {
        BioRefitChange.BronzeFrame        => (typeof(BronzeIngot), FrameIngots, "bronze ingots"),
        BioRefitChange.AgapiteFrame       => (typeof(AgapiteIngot), FrameIngots, "agapite ingots"),
        BioRefitChange.ShadowIronFrame    => (typeof(ShadowIronIngot), FrameIngots, "shadow iron ingots"),
        BioRefitChange.InstallCircuit     => (typeof(ShadowIronIngot), ModuleIngots, "shadow iron ingots"),
        BioRefitChange.InstallRegulator   => (typeof(BronzeIngot), ModuleIngots, "bronze ingots"),
        _                               => (null, 0, null)
    };

    internal static string FrameName(BioFrameAlloy frame) => frame switch
    {
        BioFrameAlloy.Bronze     => "Bronze",
        BioFrameAlloy.Agapite    => "Agapite",
        BioFrameAlloy.ShadowIron => "Shadow Iron",
        _                      => "Iron"
    };

    internal static string FrameEffect(BioFrameAlloy frame) => frame switch
    {
        BioFrameAlloy.Bronze =>
            $"Recover {BronzeHealing} health after a melee hit from a wild foe.",
        BioFrameAlloy.Agapite =>
            $"Recover {AgapiteMana} mana after landing or taking a melee hit against a wild foe.",
        BioFrameAlloy.ShadowIron =>
            $"A landed melee attack drains {ShadowIronDrain} stamina from a wild foe.",
        _ => "No additional ability."
    };

    internal static string FrameTiming(BioFrameAlloy frame) =>
        frame == BioFrameAlloy.Iron ? "" : $"Once every {FrameCooldownSeconds} seconds.";

    internal static string CircuitTiming(bool installed) =>
        installed
            ? $"{CircuitStaminaCost} stamina, {CircuitCooldownSeconds}-second cooldown."
            : $"{TraitStaminaCost} stamina, {_traitCooldown.TotalSeconds:0}-second cooldown.";

    internal static string RegulatorEffect =>
        $"Recover {RegulatorStamina} stamina after a melee hit from a wild foe.";

    internal static string RegulatorTiming => $"Once every {RegulatorCooldownSeconds} seconds.";

    private void AddRefitProperties(IPropertyList list)
    {
        list.Add($"{"Frame"}\t{FrameName(FrameAlloy)}");
        if (FocusedCircuit)
        {
            list.Add("Focused Circuit");
        }

        if (StaminaRegulator)
        {
            list.Add("Stamina Regulator");
        }
    }

    private bool CanApplyRefitEffects(BaseCreature foe) =>
        Assembled && Controlled && Alive && !IsDeadPet && !foe.NoKillAwards && !foe.IsBonded && foe.LastOwner == null;

    private void ApplyFrameEffect(BaseCreature foe, bool landedHit)
    {
        var now = Core.Now;
        if (now < NextFrameAt)
        {
            return;
        }

        switch (FrameAlloy)
        {
            case BioFrameAlloy.Bronze when !landedHit && Hits < HitsMax && !Poisoned &&
                                           !MortalStrike.IsWounded(this):
                NextFrameAt = now + _frameCooldown;
                Hits += BronzeHealing;
                break;
            case BioFrameAlloy.Agapite when Mana < ManaMax:
                NextFrameAt = now + _frameCooldown;
                Mana += AgapiteMana;
                break;
            case BioFrameAlloy.ShadowIron when landedHit && foe.Stam > 0:
                NextFrameAt = now + _frameCooldown;
                foe.Stam -= ShadowIronDrain;
                break;
        }
    }

    private void ApplyUtilityEffect()
    {
        var now = Core.Now;
        if (StaminaRegulator && Stam < StamMax && now >= NextUtilityAt)
        {
            NextUtilityAt = now + _utilityCooldown;
            Stam += RegulatorStamina;
        }
    }
}
