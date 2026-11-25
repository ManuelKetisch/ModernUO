using System;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using Server.Targeting;

namespace Server.Engines.BioEngineering;

public partial class BioConstruct
{
    private const int OwnerInteractionRange = 2;
    private const int BandageIntervalMs = 5000;
    private long _nextBandageTick = Core.TickCount;

    public bool HasHumanForm => Assembled && Body.IsHuman;

    private bool CanManage(Mobile from) =>
        Assembled && !Deleted && Alive && !IsDeadPet && !IsStabled && Controlled && from == ControlMaster &&
        from is PlayerMobile { Deleted: false, Alive: true } && Map is not null && Map != Map.Internal &&
        from.Map == Map && from.InRange(this, OwnerInteractionRange) && from.CanSee(this) && from.InLOS(this);

    private bool CanManageHuman(Mobile from) => HasHumanForm && CanManage(from);

    private static bool IsEquipment(Item item) => item is BaseWeapon or BaseArmor or BaseClothing or BaseJewel;

    private static bool IsWearable(Item item) =>
        IsEquipment(item) && item.Movable && !item.Nontransferable && !item.QuestItem;

    private bool HasHumanInventory()
    {
        if (Backpack?.TotalItems > 0)
        {
            return true;
        }

        foreach (var item in Items)
        {
            if (IsWearable(item))
            {
                return true;
            }
        }

        return false;
    }

    public override bool IsHumanInTown() => false;

    public override bool AllowEquipFrom(Mobile from) => CanManageHuman(from) || base.AllowEquipFrom(from);

    public override bool OnEquip(Item item)
    {
        if (ControlMaster is not PlayerMobile owner || !CanManageHuman(owner) || !IsWearable(item))
        {
            return false;
        }

        if (!base.OnEquip(item))
        {
            return false;
        }

        RememberEquipmentOwner();
        return true;
    }

    public override bool IsSnoop(Mobile from) => !CanManageHuman(from) && base.IsSnoop(from);

    public override bool CheckNonlocalLift(Mobile from, Item item) =>
        CanManageHuman(from) && (item.IsChildOf(Backpack) || item.Parent == this && IsWearable(item)) ||
        base.CheckNonlocalLift(from, item);

    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        if (CanManageHuman(from) && dropped is Bandage { Nontransferable: false, QuestItem: false })
        {
            RememberEquipmentOwner();
            return EnsureHumanPack().TryDropItem(from, dropped, true);
        }

        return base.OnDragDrop(from, dropped);
    }

    private Container EnsureHumanPack()
    {
        var pack = Backpack;
        if (pack == null)
        {
            pack = new Backpack { Movable = false };
            AddItem(pack);
        }

        return pack;
    }

    private void RememberEquipmentOwner()
    {
        if (ControlMaster is PlayerMobile owner && LastOwner != owner)
        {
            Owners.Add(owner);
            this.MarkDirty();
        }
    }

    internal bool TryUndress(Mobile from)
    {
        if (!CanManageHuman(from))
        {
            return false;
        }

        var pack = EnsureHumanPack();
        var addedItems = 0;
        var addedWeight = 0;
        foreach (var item in Items)
        {
            if (!IsWearable(item))
            {
                continue;
            }

            if (!pack.CheckHold(from, item, true, true, addedItems, addedWeight))
            {
                return false;
            }

            addedItems += 1 + item.TotalItems;
            addedWeight += item.TotalWeight + item.PileWeight;
        }

        for (var i = Items.Count - 1; i >= 0; i--)
        {
            var item = Items[i];
            if (IsWearable(item))
            {
                pack.DropItem(item);
            }
        }

        from.SendMessage(addedItems > 0 ? "The equipment is in your construct's backpack." : "Your construct has no equipment to remove.");
        return true;
    }

    public override bool HandlesOnSpeech(Mobile from) => CanManage(from) || base.HandlesOnSpeech(from);

    public override void OnSpeech(SpeechEventArgs e)
    {
        var name = Name.AsSpan();
        var speech = e.Speech.AsSpan().Trim();
        if (e.Handled || e.Blocked || name.IsEmpty || !CanManage(e.Mobile) || speech.Length <= name.Length ||
            !speech.StartsWith(name, StringComparison.OrdinalIgnoreCase) || !char.IsWhiteSpace(speech[name.Length]))
        {
            base.OnSpeech(e);
            return;
        }

        var command = speech[name.Length..].Trim();
        if (command.Equals("skills", StringComparison.OrdinalIgnoreCase))
        {
            BioSkillsGump.DisplayTo(e.Mobile, this);
        }
        else if (command.Equals("sheet", StringComparison.OrdinalIgnoreCase))
        {
            TryOpenSheet(e.Mobile);
        }
        else if (HasHumanForm && command.Equals("undress", StringComparison.OrdinalIgnoreCase))
        {
            TryUndress(e.Mobile);
        }
        else if (HasHumanForm && command.Equals("ride", StringComparison.OrdinalIgnoreCase))
        {
            e.Mobile.SendMessage("Target an unmounted creature you own next to your construct.");
            e.Mobile.BeginTarget(
                3, false, TargetFlags.None,
                (from, target, bio) => bio.TryRide(from, target as BaseMount), this
            );
        }
        else if (HasHumanForm && command.Equals("dismount", StringComparison.OrdinalIgnoreCase))
        {
            TryDismount(e.Mobile);
        }
        else
        {
            base.OnSpeech(e);
            return;
        }

        e.Handled = true;
    }

    internal bool TryRide(Mobile from, BaseMount mount)
    {
        if (!CanManageHuman(from))
        {
            return false;
        }

        if (Mounted || mount is not { Deleted: false, Alive: true, IsDeadPet: false, IsStabled: false,
                Summoned: false, Controlled: true, Rider: null, Poisoned: false, InternalItem: not null } ||
            mount.ControlMaster != from || mount.Map != Map || !InRange(mount, 1) || !from.InRange(mount, 3) ||
            !from.CanSee(mount) || !from.InLOS(mount) || !InLOS(mount) ||
            Region is BaseRegion { MountsAllowed: false } || mount.Region is BaseRegion { MountsAllowed: false } ||
            (Female ? !mount.AllowFemaleRider : !mount.AllowMaleRider))
        {
            from.SendMessage("Your construct cannot ride that mount here.");
            return false;
        }

        mount.Rider = this;
        return true;
    }

    internal bool TryDismount(Mobile from)
    {
        if (!CanManageHuman(from) || !Mounted)
        {
            return false;
        }

        BaseMount.Dismount(this);
        return true;
    }

    public override void OnThink()
    {
        base.OnThink();

        if (HasHumanForm && Controlled && Alive && !IsDeadPet && !IsStabled && Map != Map.Internal &&
            (HitsMax - Hits >= 25 || Poisoned))
        {
            TryBandageSelf();
        }
    }

    internal bool TryBandageSelf()
    {
        var now = Core.TickCount;
        if (!HasHumanForm || !Controlled || !Alive || IsDeadPet || IsStabled || Map == null || Map == Map.Internal ||
            now - _nextBandageTick < 0 || BandageContext.GetContext(this) != null ||
            Backpack?.FindItemByType<Bandage>() is not { } bandage || BandageContext.BeginHeal(this, this) == null)
        {
            return false;
        }

        bandage.Consume();
        _nextBandageTick = now + BandageIntervalMs;
        return true;
    }

    protected override void OnMapChange(Map oldMap)
    {
        if (Map == Map.Internal)
        {
            BandageContext.GetContext(this)?.StopHeal();
            if (oldMap != null && oldMap != Map.Internal && Mount is BaseMount mount)
            {
                var location = Location;
                BaseMount.Dismount(this);
                mount.MoveToWorld(location, oldMap);
            }
        }

        base.OnMapChange(oldMap);
    }

    public override bool OnBeforeDeath()
    {
        if (!base.OnBeforeDeath())
        {
            return false;
        }

        BaseMount.Dismount(this);
        BandageContext.GetContext(this)?.StopHeal();
        return true;
    }

    public override void OnDelete()
    {
        BaseMount.Dismount(this);
        BandageContext.GetContext(this)?.StopHeal();

        if (Assembled && HasHumanInventory() && (ControlMaster ?? LastOwner) is PlayerMobile { Deleted: false } owner)
        {
            for (var i = Items.Count - 1; i >= 0; i--)
            {
                var item = Items[i];
                if (IsWearable(item))
                {
                    ReturnItem(owner, item);
                }
            }

            if (Backpack is { } pack)
            {
                for (var i = pack.Items.Count - 1; i >= 0; i--)
                {
                    ReturnItem(owner, pack.Items[i]);
                }
            }

            owner.SendMessage("Your construct's remaining belongings were returned to you.");
        }

        base.OnDelete();
    }

    private static void ReturnItem(PlayerMobile owner, Item item)
    {
        if (!owner.PlaceInBackpack(item))
        {
            // Deleting the construct must leave an owner-only destination even when both containers are full.
            owner.BankBox.DropItem(item);
        }
    }

    private static readonly SkillName[] _sharedSkills =
    [
        SkillName.Wrestling, SkillName.Tactics, SkillName.Anatomy, SkillName.Poisoning, SkillName.Magery,
        SkillName.EvalInt, SkillName.Meditation, SkillName.MagicResist
    ];

    internal static readonly SkillName[] HumanSkills =
    [
        SkillName.Swords, SkillName.Fencing, SkillName.Macing, SkillName.Archery, SkillName.Parry, SkillName.Healing
    ];

    private const int SkillDecreaseFixed = 10;

    private bool CanManageSkill(Mobile from, SkillName skillName) =>
        CanManage(from) && (Array.IndexOf(_sharedSkills, skillName) >= 0 ||
                            HasHumanForm && Array.IndexOf(HumanSkills, skillName) >= 0);

    internal bool TrySetSkillLock(Mobile from, SkillName skillName, SkillLock skillLock)
    {
        if (skillLock is < SkillLock.Up or > SkillLock.Locked || !CanManageSkill(from, skillName))
        {
            return false;
        }

        Skills[skillName].SetLockNoRelay(skillLock);
        this.MarkDirty();
        return true;
    }

    private bool TryDecreaseSkill(Mobile from, SkillName skillName, int expectedBase)
    {
        if (!CanManageSkill(from, skillName) || expectedBase <= 0)
        {
            return false;
        }

        var skill = Skills[skillName];
        if (skill.Lock != SkillLock.Down || skill.BaseFixedPoint != expectedBase)
        {
            return false;
        }

        skill.BaseFixedPoint = Math.Max(0, expectedBase - SkillDecreaseFixed);
        this.MarkDirty();
        return true;
    }

    internal sealed class BioSkillsGump(BioConstruct bio) : DynamicGump(80, 60)
    {
        private const int LabelHue = 0x480;
        private const int SheetButton = 100;
        private const int DecreaseStart = 101;
        public override bool Singleton => true;

        internal static void DisplayTo(Mobile from, BioConstruct bio)
        {
            if (from.NetState != null && bio.CanManage(from))
            {
                from.SendGump(new BioSkillsGump(bio));
            }
        }

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            var humanCount = bio.HasHumanForm ? HumanSkills.Length : 0;
            var count = _sharedSkills.Length + humanCount;
            builder.AddPage();
            builder.AddBackground(0, 0, 468, 103 + count * 23, 3500);
            builder.AddLabel(20, 15, 0x34, "Construct skills");
            builder.AddButton(347, 15, 4005, 4007, SheetButton);
            builder.AddLabel(382, 15, LabelHue, "Sheet");
            builder.AddLabel(20, 42, LabelHue, "Skill");
            builder.AddLabel(174, 42, LabelHue, "Base");
            builder.AddLabel(245, 42, LabelHue, "Training");

            for (var i = 0; i < count; i++)
            {
                var skill = bio.Skills[GetSkill(i)];
                var y = 69 + i * 23;
                builder.AddLabel(20, y, LabelHue, skill.Name);
                builder.AddLabel(174, y, LabelHue, $"{skill.Base:F1}");
                builder.AddButton(245, y, 4005, 4007, i + 1);
                builder.AddLabel(280, y, LabelHue, LockLabel(skill.Lock));
                if (skill.Lock == SkillLock.Down && skill.BaseFixedPoint > 0)
                {
                    builder.AddButton(338, y, 4005, 4007, DecreaseStart + i);
                    builder.AddLabel(372, y, LabelHue, "Decrease");
                }
            }

            if (bio.HasHumanForm)
            {
                builder.AddLabel(20, 77 + count * 23, LabelHue, "Say its name, then Undress, Ride, or Dismount.");
            }
        }

        private static string LockLabel(SkillLock skillLock) => skillLock switch
        {
            SkillLock.Up     => "Raise",
            SkillLock.Down   => "Lower",
            SkillLock.Locked => "Lock",
            _                => ""
        };

        private static SkillName GetSkill(int row) =>
            row < _sharedSkills.Length ? _sharedSkills[row] : HumanSkills[row - _sharedSkills.Length];

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (info.ButtonID == SheetButton)
            {
                bio.TryOpenSheet(sender.Mobile);
                return;
            }

            var decrease = info.ButtonID >= DecreaseStart;
            var row = info.ButtonID - (decrease ? DecreaseStart : 1);
            if (row < 0 || row >= _sharedSkills.Length + (bio.HasHumanForm ? HumanSkills.Length : 0))
            {
                return;
            }

            var owner = sender.Mobile;
            var skill = bio.Skills[GetSkill(row)];
            if (decrease)
            {
                if (bio.CanManageSkill(owner, skill.SkillName) && skill.Lock == SkillLock.Down && skill.BaseFixedPoint > 0)
                {
                    owner.SendGump(new BioSkillDecreaseGump(bio, owner, skill.SkillName, skill.BaseFixedPoint));
                }

                return;
            }

            var next = (SkillLock)(((int)skill.Lock + 1) % 3);
            if (bio.TrySetSkillLock(owner, skill.SkillName, next))
            {
                DisplayTo(owner, bio);
            }
        }
    }

    internal sealed class BioSkillDecreaseGump(
        BioConstruct bio, Mobile owner, SkillName skillName, int previousBase
    ) : DynamicGump(90, 70)
    {
        private const int LabelHue = 0x480;
        public override bool Singleton => true;

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            var nextBase = Math.Max(0, previousBase - SkillDecreaseFixed);
            builder.AddPage();
            builder.AddBackground(0, 0, 314, 132, 3500);
            builder.AddLabel(22, 17, 0x34, $"Decrease {bio.Skills[skillName].Name}");
            builder.AddLabel(22, 46, LabelHue, $"Base skill {previousBase / 10.0:F1} to {nextBase / 10.0:F1}");
            builder.AddButton(22, 86, 4005, 4007, 1);
            builder.AddLabel(57, 86, LabelHue, "Apply");
            builder.AddButton(158, 86, 4017, 4019, 0);
            builder.AddLabel(193, 86, LabelHue, "Back");
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile != owner)
            {
                return;
            }

            if (info.ButtonID == 1 && !bio.TryDecreaseSkill(owner, skillName, previousBase))
            {
                owner.SendMessage("Your construct or skill changed. Review the current training settings.");
            }

            BioSkillsGump.DisplayTo(owner, bio);
        }
    }

}
