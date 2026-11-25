using ModernUO.Serialization;
using Server.Engines.WeaponLeveling;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Items;

public abstract class BaseWeaponLevelDeed : Item
{
    protected BaseWeaponLevelDeed(int hue) : base(0x14F0)
    {
        Hue = hue;
    }

    protected BaseWeaponLevelDeed(Serial serial) : base(serial)
    {
    }

    public override double DefaultWeight => 1.0;

    protected abstract string Prompt { get; }
    protected abstract string Rejection { get; }
    protected abstract bool Apply(PlayerMobile player, BaseWeapon weapon);

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile player || !CanUse(player))
        {
            return;
        }

        player.SendMessage(Prompt);
        player.BeginTarget(2, false, TargetFlags.None, (mobile, target) =>
        {
            if (mobile is PlayerMobile owner)
            {
                TryUse(owner, target as BaseWeapon);
            }
        });
    }

    internal bool TryUse(PlayerMobile player, BaseWeapon weapon)
    {
        if (!CanUse(player))
        {
            return false;
        }

        if (weapon == null || !Apply(player, weapon))
        {
            player.SendMessage(Rejection);
            return false;
        }

        Delete();
        WeaponLevelingGump.DisplayTo(player, weapon);
        return true;
    }

    private bool CanUse(PlayerMobile player)
    {
        if (Deleted || player is not { Alive: true, Deleted: false })
        {
            return false;
        }

        if (player.Backpack == null || !IsChildOf(player.Backpack) || GetSecureTradeCont() != null ||
            !IsAccessibleTo(player))
        {
            player.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return false;
        }

        return true;
    }
}

[SerializationGenerator(0)]
public sealed partial class WeaponLevelDeed : BaseWeaponLevelDeed
{
    [Constructible]
    public WeaponLevelDeed() : base(0x482)
    {
    }

    public override string DefaultName => "weapon level deed";
    protected override string Prompt => "Choose a weapon to begin leveling.";
    protected override string Rejection => "Choose a weapon you own that has not started leveling.";
    protected override bool Apply(PlayerMobile player, BaseWeapon weapon) => weapon.TryEnableWeaponLeveling(player);
}

[SerializationGenerator(0)]
public sealed partial class WeaponLevelBoostDeed : BaseWeaponLevelDeed
{
    [Constructible]
    public WeaponLevelBoostDeed() : base(0x47E)
    {
    }

    public override string DefaultName => "weapon level boost deed";
    protected override string Prompt => "Choose a levelable weapon to raise by up to ten levels.";
    protected override string Rejection => "Choose a levelable weapon you own below level 100.";
    protected override bool Apply(PlayerMobile player, BaseWeapon weapon) => weapon.TryApplyWeaponLevelBoost(player);
}
