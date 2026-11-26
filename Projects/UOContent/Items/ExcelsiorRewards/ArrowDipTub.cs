using System;
using ModernUO.Serialization;
using Server.Engines.WeaponLeveling;
using Server.Engines.WeaponLevelingServices;
using Server.Mobiles;
using Server.Spells;
using Server.Targeting;

namespace Server.Items;

public enum ArrowDipKind
{
    ArmorPiercing,
    Explosive,
    Freeze,
    Lightning,
    Poison
}

[SerializationGenerator(0)]
public sealed partial class ArrowDipTub : Item, IUsesRemaining
{
    internal const int ArrowsPerUse = 10;

    [SerializableField(0, setter: "private")]
    private ArrowDipKind _kind;

    [SerializableField(1)]
    [InvalidateProperties]
    private int _usesRemaining;

    [Constructible]
    public ArrowDipTub(ArrowDipKind kind = ArrowDipKind.ArmorPiercing, int uses = 5) : base(0xFAB)
    {
        Kind = kind;
        Hue = DippedArrow.GetHue(kind);
        UsesRemaining = Math.Clamp(uses, 1, 50);
    }

    public override string DefaultName => $"{DippedArrow.GetName(Kind)} dip tub";
    public override double DefaultWeight => 10.0;

    bool IUsesRemaining.ShowUsesRemaining
    {
        get => true;
        set { }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1060584, UsesRemaining);
        list.Add($"Converts {ArrowsPerUse} ordinary arrows per use.");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile owner || !WeaponServiceTransfer.IsInBackpack(owner, this))
        {
            from.SendMessage("Keep the dip tub in your backpack.");
            return;
        }

        owner.SendMessage($"Choose a stack of at least {ArrowsPerUse} ordinary arrows.");
        owner.BeginTarget(1, false, TargetFlags.None, (_, target) => TryDip(owner, target as Item));
    }

    internal bool TryDip(PlayerMobile owner, Item item)
    {
        if (UsesRemaining <= 0 || !WeaponServiceTransfer.IsInBackpack(owner, this) ||
            !WeaponServiceTransfer.IsInBackpack(owner, item) || item.GetType() != typeof(Arrow) ||
            item is not { Amount: >= ArrowsPerUse, Movable: true, Nontransferable: false, LootType: LootType.Regular } ||
            item.QuestItem || item.Insured || item.BlessedFor != null)
        {
            owner.SendMessage("Choose ordinary unbound arrows in your backpack.");
            return false;
        }

        var dipped = new DippedArrow(Kind, ArrowsPerUse);
        if (!owner.Backpack.TryDropItem(owner, dipped, true))
        {
            dipped.Delete();
            return false;
        }

        item.Consume(ArrowsPerUse);
        UsesRemaining--;
        if (UsesRemaining == 0)
        {
            Delete();
        }

        owner.PlaySound(0x23E);
        owner.SendMessage("Double-click the dipped arrows to ready them.");
        return true;
    }
}

[SerializationGenerator(0)]
public sealed partial class DippedArrow : Item
{
    [SerializableField(0, setter: "private")]
    private ArrowDipKind _kind;

    [SerializableField(1, setter: "private")]
    [InvalidateProperties]
    private bool _readied;

    [Constructible]
    public DippedArrow(ArrowDipKind kind = ArrowDipKind.ArmorPiercing, int amount = 1) : base(0xF3F)
    {
        Kind = kind;
        Stackable = true;
        Hue = GetHue(kind);
        Amount = amount;
    }

    public override string DefaultName => $"{GetName(Kind)} arrows";
    public override double DefaultWeight => ItemWeights.Ammunition;

    public override bool CanStackWith(Item dropped) =>
        dropped is DippedArrow arrow && arrow.Kind == Kind && arrow.Readied == Readied && base.CanStackWith(dropped);

    public override void OnAfterDuped(Item newItem)
    {
        base.OnAfterDuped(newItem);
        if (newItem is DippedArrow arrow)
        {
            arrow.Kind = Kind;
            arrow.Readied = Readied;
            arrow.Hue = Hue;
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(Readied ? "Readied for your bow." : "Double-click to ready for your bow.");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile owner || !WeaponServiceTransfer.IsInBackpack(owner, this))
        {
            return;
        }

        if (Readied)
        {
            Readied = false;
            owner.SendMessage("You put away the dipped arrows.");
            return;
        }

        foreach (var item in owner.Backpack.FindItemsByType<DippedArrow>())
        {
            if (item.Readied && WeaponServiceTransfer.IsInBackpack(owner, item))
            {
                item.Readied = false;
            }
        }

        Readied = true;
        owner.SendMessage($"You ready your {GetName(Kind)} arrows.");
    }

    internal static bool TryFire(Mobile from, out ArrowDipKind kind)
    {
        kind = default;
        if (from is not PlayerMobile { Player: true, Backpack: { } pack } owner)
        {
            return false;
        }

        var arrow = pack.FindItemByType<DippedArrow>(predicate: candidate =>
            candidate.Readied && candidate.Amount > 0 && WeaponServiceTransfer.IsInBackpack(owner, candidate));
        if (arrow == null)
        {
            return false;
        }

        kind = arrow.Kind;
        arrow.Consume();
        return true;
    }

    internal static string GetName(ArrowDipKind kind) => kind switch
    {
        ArrowDipKind.Explosive => "explosive",
        ArrowDipKind.Freeze    => "freeze",
        ArrowDipKind.Lightning => "lightning",
        ArrowDipKind.Poison    => "poison",
        _                     => "armor-piercing"
    };

    internal static int GetHue(ArrowDipKind kind) => kind switch
    {
        ArrowDipKind.Explosive => 0x489,
        ArrowDipKind.Freeze    => 0x480,
        ArrowDipKind.Lightning => 0x4A4,
        ArrowDipKind.Poison    => 0x5F1,
        _                     => 0x482
    };
}

internal static class DippedArrowEffects
{
    internal static void Apply(BaseRanged weapon, Mobile attacker, Mobile defender, ArrowDipKind kind)
    {
        if (attacker.Deleted || defender.Deleted || attacker.Map == null || attacker.Map != defender.Map)
        {
            return;
        }

        using var hit = WeaponLevelingCombat.BeginWeaponHit(weapon, attacker, defender as BaseCreature);
        switch (kind)
        {
            case ArrowDipKind.ArmorPiercing:
                AOS.Damage(defender, attacker, 5, true, 100, 0, 0, 0, 0);
                break;
            case ArrowDipKind.Explosive:
                AOS.Damage(defender, attacker, 7, 0, 100, 0, 0, 0);
                SprayFire(attacker, defender);
                break;
            case ArrowDipKind.Freeze when defender.Alive:
                defender.Paralyze(TimeSpan.FromSeconds(1));
                break;
            case ArrowDipKind.Lightning:
                AOS.Damage(defender, attacker, 8, 0, 0, 0, 0, 100);
                break;
            case ArrowDipKind.Poison when defender.Alive:
                defender.ApplyPoison(attacker, Poison.Regular);
                break;
        }
    }

    private static void SprayFire(Mobile attacker, Mobile defender)
    {
        BaseCreature first = null;
        BaseCreature second = null;
        foreach (var target in defender.GetMobilesInRange<BaseCreature>(1))
        {
            if (target == defender || target is not { Alive: true, Controlled: false, Summoned: false } ||
                !SpellHelper.ValidIndirectTarget(attacker, target) || !attacker.CanBeHarmful(target, false) ||
                !attacker.CanSee(target) || !attacker.InLOS(target))
            {
                continue;
            }

            if (first == null)
            {
                first = target;
            }
            else
            {
                second = target;
                break;
            }
        }

        Splash(attacker, first);
        Splash(attacker, second);
    }

    private static void Splash(Mobile attacker, BaseCreature target)
    {
        if (target is not { Alive: true, Deleted: false })
        {
            return;
        }

        attacker.DoHarmful(target, indirect: true);
        AOS.Damage(target, attacker, 4, 0, 100, 0, 0, 0);
    }
}
