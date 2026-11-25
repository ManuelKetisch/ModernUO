using System;
using Server.Items;

namespace Server.Mobiles;

public partial class PlayerMobile
{
    private static readonly TimeSpan OffHandCombatCheckInterval = TimeSpan.FromMilliseconds(50);

    private long _nextOffHandSwing;
    private TimerExecutionToken _offHandCombatTimer;
    private BaseWeapon _offHandInSwing;

    internal bool IsSwingingOffHand => _offHandInSwing != null;

    public override IWeapon Weapon => _offHandInSwing ?? base.Weapon;

    private bool CanEquipOffHand(BaseWeapon weapon) =>
        weapon.CanUseEitherHand && FindItemOnLayer(Layer.OneHanded) is BaseWeapon { CanUseEitherHand: true } &&
        FindItemOnLayer(Layer.TwoHanded) == null;

    public override bool EquipItem(Item item) => EquipItem(item, Layer.Invalid);

    public bool EquipItem(Item item, Layer requestedLayer)
    {
        if (!Alive || item?.Deleted != false || item.Parent == this)
        {
            return base.EquipItem(item);
        }

        if (item is BaseWeapon weapon && CanEquipOffHand(weapon))
        {
            weapon.SetOffHand(true);
            try
            {
                return base.EquipItem(weapon);
            }
            finally
            {
                if (weapon.Parent != this)
                {
                    weapon.SetOffHand(false);
                }
            }
        }

        if (JewelrySlots.IsMultiSlotJewelry(item))
        {
            if (!JewelrySlots.TryResolveEquipLayer(this, item, requestedLayer, out var targetLayer))
            {
                return false;
            }

            var canonicalLayer = JewelrySlots.GetCanonicalLayer(item);
            item.Layer = targetLayer;
            try
            {
                return base.EquipItem(item);
            }
            finally
            {
                if (item.Parent != this)
                {
                    item.Layer = canonicalLayer;
                }
            }
        }

        return base.EquipItem(item);
    }

    private BaseWeapon EquippedOffHandWeapon =>
        FindItemOnLayer(Layer.TwoHanded) is BaseWeapon { EquippedOffHand: true } weapon ? weapon : null;

    public override void OnCombatantChange()
    {
        base.OnCombatantChange();
        UpdateOffHandCombat();
    }

    private void UpdateOffHandCombat(bool weaponAdded = false)
    {
        var offHand = EquippedOffHandWeapon;
        if (offHand == null)
        {
            _offHandCombatTimer.Cancel();
            _nextOffHandSwing = Core.TickCount;
            return;
        }

        var now = Core.TickCount;
        if (weaponAdded)
        {
            _nextOffHandSwing = now + (long)offHand.GetDelay(this).TotalMilliseconds;
        }

        if (Combatant == null)
        {
            _offHandCombatTimer.Cancel();
        }
        else if (!_offHandCombatTimer.Running)
        {
            // Offset the first attack so identical weapons start on different beats.
            var firstSwing = now + (long)offHand.GetDelay(this).TotalMilliseconds / 2;
            if (_nextOffHandSwing - firstSwing < 0)
            {
                _nextOffHandSwing = firstSwing;
            }

            Timer.StartTimer(OffHandCombatCheckInterval, 0, CheckOffHandCombatTime, out _offHandCombatTimer);
        }
    }

    private void CheckOffHandCombatTime() => CheckOffHandCombatTime(Core.TickCount);

    internal void CheckOffHandCombatTime(long now)
    {
        var target = Combatant;
        var offHand = EquippedOffHandWeapon;
        if (offHand == null || Deleted || !Alive || target?.Deleted != false || !target.Alive ||
            Map == Map.Internal || target.Map != Map || target.IsDeadBondedPet || !CanSee(target))
        {
            _offHandCombatTimer.Cancel();
            return;
        }

        if (now - _nextOffHandSwing < 0 || !InRange(target, offHand.MaxRange) || !InLOS(target))
        {
            return;
        }

        _offHandInSwing = offHand;
        try
        {
            offHand.OnBeforeSwing(this, target);
            RevealingAction();
            var delay = offHand.OnSwing(this, target);
            _nextOffHandSwing = now + (long)delay.TotalMilliseconds;
        }
        finally
        {
            _offHandInSwing = null;
        }
    }
}
