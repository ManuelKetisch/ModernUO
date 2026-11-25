using ModernUO.Serialization;
using Server.Engines.WeaponLevelingServices;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Items;

public abstract class WeaponServiceDeed : Item
{
    protected WeaponServiceDeed(int hue) : base(0x14F0) => Hue = hue;

    protected WeaponServiceDeed(Serial serial) : base(serial)
    {
    }

    public override double DefaultWeight => 1.0;

    internal abstract bool IsRelayer { get; }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile player || !CanUse(player))
        {
            return;
        }

        player.SendMessage("Choose the weapon you want to change.");
        player.BeginTarget(-1, false, TargetFlags.None, (mobile, target) =>
        {
            if (mobile is PlayerMobile owner && CanUse(owner))
            {
                ChooseSource(owner, target as BaseWeapon);
            }
        });
    }

    internal bool CanUse(PlayerMobile player)
    {
        if (WeaponServiceTransfer.IsInBackpack(player, this))
        {
            return true;
        }

        player?.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
        return false;
    }

    private void ChooseSource(PlayerMobile player, BaseWeapon source)
    {
        if (source?.CanManageWeaponLeveling(player) != true ||
            (!source.IsWeaponLevelingEnabled && (IsRelayer || !source.HasWeaponLevelingArtifactTag)))
        {
            player.SendMessage(IsRelayer
                ? "Choose a levelable weapon you own."
                : "Choose a levelable weapon or a tagged artifact you own.");
            return;
        }

        if (!IsRelayer && source.HasWeaponLevelingArtifactTag)
        {
            WeaponServiceGump.Show(player, this, source, null, WeaponServiceOperation.WaiveTag);
            return;
        }

        ChooseTemplate(player, source);
    }

    internal void ChooseTemplate(PlayerMobile player, BaseWeapon source)
    {
        if (!CanUse(player) || !WeaponServiceTransfer.CanConsume(player, source))
        {
            player.SendMessage("Put an unbound weapon in your backpack first.");
            return;
        }

        player.SendMessage(IsRelayer
            ? "Choose blank equipment or a blank one-handed weapon in your backpack."
            : "Choose a blank weapon of the new type in your backpack.");

        player.BeginTarget(-1, false, TargetFlags.None, (mobile, target) =>
        {
            if (mobile is not PlayerMobile owner || !CanUse(owner))
            {
                return;
            }

            var template = target as Item;
            var operation = IsRelayer
                ? template is BaseWeapon ? WeaponServiceOperation.OneHanded : WeaponServiceOperation.Relayer
                : WeaponServiceOperation.ChangeType;
            if (!WeaponServiceTransfer.CanPerform(owner, source, template, operation))
            {
                owner.SendMessage(IsRelayer
                    ? "Choose compatible blank equipment in your backpack."
                    : "Choose a different blank weapon type in your backpack.");
                return;
            }

            WeaponServiceGump.Show(owner, this, source, template, operation);
        });
    }
}

[SerializationGenerator(0)]
public sealed partial class WeaponChangeDeed : WeaponServiceDeed
{
    [Constructible]
    public WeaponChangeDeed() : base(0x482)
    {
    }

    public override string DefaultName => "weapon change deed";
    internal override bool IsRelayer => false;
}

[SerializationGenerator(0)]
public sealed partial class WeaponRelayerDeed : WeaponServiceDeed
{
    [Constructible]
    public WeaponRelayerDeed() : base(0x47E)
    {
    }

    public override string DefaultName => "weapon re-layer deed";
    internal override bool IsRelayer => true;
}
