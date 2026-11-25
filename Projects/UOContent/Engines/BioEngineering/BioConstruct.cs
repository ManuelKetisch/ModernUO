using System;
using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Engines.BioEngineering;

[SerializationGenerator(0)]
public partial class BioCreationDeed : Item
{
    [Constructible]
    public BioCreationDeed() : base(0x14F0)
    {
        Hue = 0x59;
    }

    public override string DefaultName => "bio creation deed";
    public override double DefaultWeight => 1.0;

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile player)
        {
            TryCreate(player);
        }
    }

    internal BioConstruct TryCreate(PlayerMobile player)
    {
        if (!BioEngineeringBook.CanUseItem(player, this) || !BioEngineeringBook.CheckTraining(player))
        {
            return null;
        }

        if (player.Map == null || player.Map == Map.Internal)
        {
            player.SendMessage("You cannot create a construct here.");
            return null;
        }

        if (player.Followers + BioConstruct.RequiredSlots > player.FollowersMax)
        {
            player.SendLocalizedMessage(1049607); // You have too many followers to control that creature.
            return null;
        }

        var construct = new BioConstruct(player);
        if (!construct.SetControlMaster(player))
        {
            construct.Delete();
            return null;
        }

        construct.MoveToWorld(player.Location, player.Map);
        Delete();
        player.SendMessage("Use a full bio vial set on your construct.");
        return construct;
    }
}

[SerializationGenerator(2)]
public partial class BioConstruct : BaseCreature
{
    internal const int RequiredSlots = 3;

    [SerializableField(0, getter: "private", setter: "private")]
    private PlayerMobile _creator;

    [SerializableField(1, setter: "private")]
    [InvalidateProperties]
    private bool _assembled;

    [SerializableField(2, getter: "internal", setter: "private")]
    private BioTrait _trait;

    [SerializableField(3, getter: "private", setter: "private")]
    private int[] _mimicSounds;

    [SerializableField(4, getter: "private", setter: "private")]
    private DateTime _nextTraitAt;

    [SerializableField(5, getter: "private", setter: "private")]
    [SaveFlag(nameof(HasRefittedFrame))]
    private BioFrameAlloy _frameAlloy;

    [SerializableField(6, getter: "private", setter: "private")]
    [SaveFlag(nameof(HasFocusedCircuit))]
    private bool _focusedCircuit;

    [SerializableField(7, getter: "private", setter: "private")]
    [SaveFlag(nameof(HasStaminaRegulator))]
    private bool _staminaRegulator;

    [SerializableField(8, getter: "private", setter: "private")]
    [SaveFlag(nameof(HasFrameCooldown))]
    private DateTime _nextFrameAt;

    [SerializableField(9, getter: "private", setter: "private")]
    [SaveFlag(nameof(HasUtilityCooldown))]
    private DateTime _nextUtilityAt;

    [SerializableField(10, getter: "private", setter: "private")]
    [SaveFlag(nameof(HasDonorNames))]
    private string[] _donorNames;

    private bool HasDonorNames() => _donorNames != null;

    private void MigrateFrom(V0Content content)
    {
        _creator = content.Creator;
        _assembled = content.Assembled;
        _trait = content.Trait;
        _mimicSounds = content.MimicSounds;
        _nextTraitAt = content.NextTraitAt;
    }

    private void MigrateFrom(V1Content content)
    {
        _creator = content.Creator;
        _assembled = content.Assembled;
        _trait = content.Trait;
        _mimicSounds = content.MimicSounds;
        _nextTraitAt = content.NextTraitAt;
        _frameAlloy = content.FrameAlloy ?? BioFrameAlloy.Iron;
        _focusedCircuit = content.FocusedCircuit;
        _staminaRegulator = content.StaminaRegulator;
        _nextFrameAt = content.NextFrameAt ?? default;
        _nextUtilityAt = content.NextUtilityAt ?? default;
    }

    internal BioConstruct(PlayerMobile creator) : base(AIType.AI_Melee, FightMode.None)
    {
        Creator = creator;
        Body = 51;
        BaseSoundID = 456;
        Hue = 0x59;

        SetStr(20);
        SetDex(20);
        SetInt(20);
        SetDamage(1, 2);

        ControlSlots = RequiredSlots;
        MinTameSkill = 100;
        RemoveIfUntamed = true;
    }

    public override string DefaultName => Assembled ? "a bio construct" : "an unfinished bio construct";
    public override string CorpseName => "a construct corpse";
    public override bool IsInvulnerable => !Assembled;
    public override bool IsBondable => Assembled && base.IsBondable;
    public override bool DeleteOnRelease => true;
    public override FoodType FavoriteFood => FoodType.Meat | FoodType.FruitsAndVeggies;

    public override bool CanBeControlledBy(Mobile master) =>
        master != null && (Assembled || master == Creator) &&
        (master == ControlMaster || !HasHumanForm || !Mounted && !HasHumanInventory()) &&
        base.CanBeControlledBy(master);

    internal bool CanReceiveSamplesFrom(PlayerMobile player) =>
        !Deleted && Alive && !IsDeadPet && !IsStabled && !Assembled && Creator == player &&
        ControlMaster == player && Map == player.Map && player.InRange(this, 2) && player.CanSee(this) &&
        player.InLOS(this);

    internal bool ApplySamples(int[] prowess, int[] environment, int[] mental, int[] mimic, string[] donorNames = null)
    {
        if (Assembled || IsBodyMod || !BioPreview.TryProject(prowess, environment, mental, mimic, out var projection))
        {
            return false;
        }

        Body = projection.Body;
        Hue = mimic[1];
        Female = mimic[2] != 0;

        SetStr(projection.Strength);
        SetDex(projection.Dexterity);
        SetInt(projection.Intelligence);
        HitsMaxSeed = projection.Hits;
        Hits = HitsMax;
        SetStam(projection.Stamina);
        SetMana(projection.Mana);
        SetDamage(projection.MinimumDamage, projection.MaximumDamage);
        VirtualArmor = projection.Armor;

        SetResistance(ResistanceType.Physical, projection.PhysicalResistance);
        SetResistance(ResistanceType.Fire, projection.FireResistance);
        SetResistance(ResistanceType.Cold, projection.ColdResistance);
        SetResistance(ResistanceType.Poison, projection.PoisonResistance);
        SetResistance(ResistanceType.Energy, projection.EnergyResistance);

        SetSampledSkill(SkillName.Wrestling, prowess[6]);
        SetSampledSkill(SkillName.Tactics, prowess[7]);
        SetSampledSkill(SkillName.Anatomy, prowess[8]);
        SetSampledSkill(SkillName.Poisoning, environment[4]);
        SetSampledSkill(SkillName.Magery, mental[4]);
        SetSampledSkill(SkillName.EvalInt, mental[5]);
        SetSampledSkill(SkillName.Meditation, mental[6]);
        SetSampledSkill(SkillName.MagicResist, mental[7]);

        if (projection.Body.IsHuman && prowess.Length == 15)
        {
            for (var i = 0; i < HumanSkills.Length; i++)
            {
                SetSampledSkill(HumanSkills[i], prowess[9 + i]);
            }
        }

        AI = Skills.Magery.Base >= 50 ? AIType.AI_Mage : AIType.AI_Melee;
        FightMode = FightMode.Aggressor;
        Trait = projection.Trait;
        MimicSounds = mimic[4..];
        DonorNames = donorNames == null ? null : (string[])donorNames.Clone();
        Creator = null;
        Assembled = true;
        return true;
    }

    private void SetSampledSkill(SkillName name, int fixedPoint) =>
        Skills[name].BaseFixedPoint = BioPreview.CapSkill(fixedPoint);

    internal string DonorName(BioSampleKind kind)
    {
        var index = (int)kind - 1;
        var donors = DonorNames;
        return donors != null && (uint)index < (uint)donors.Length && !string.IsNullOrWhiteSpace(donors[index])
            ? donors[index]
            : "Donor unrecorded";
    }

    public override bool CanBeHarmful(Mobile target, bool message, bool ignoreOurBlessedness) =>
        Assembled && base.CanBeHarmful(target, message, ignoreOurBlessedness);

    public override int GetAngerSound() => Assembled ? MimicSounds[0] : base.GetAngerSound();
    public override int GetAttackSound() => Assembled ? MimicSounds[1] : base.GetAttackSound();
    public override int GetHurtSound() => Assembled ? MimicSounds[2] : base.GetHurtSound();
    public override int GetDeathSound() => Assembled ? MimicSounds[3] : base.GetDeathSound();
    public override int GetIdleSound() => Assembled ? MimicSounds[4] : base.GetIdleSound();

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        if (Trait != BioTrait.None)
        {
            list.Add($"{"Special move"}\t{TraitName(Trait)}");
        }

        if (Assembled)
        {
            AddRefitProperties(list);
        }
    }

    public override void OnAfterDelete()
    {
        Creator = null;
        base.OnAfterDelete();
    }
}
