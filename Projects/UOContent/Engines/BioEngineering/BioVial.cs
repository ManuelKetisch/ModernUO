using System;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Engines.BioEngineering;

public enum BioSampleKind
{
    None,
    Prowess,
    Environment,
    Mental,
    Mimic
}

[SerializationGenerator(1)]
public partial class BioVial : Item
{
    private const int SampleRange = 2;

    [SerializableField(0, setter: "private")]
    [InvalidateProperties]
    private BioSampleKind _kind;

    [SerializableField(1, getter: "private", setter: "private")]
    private int[] _sample;

    [SerializableField(2, getter: "internal", setter: "private")]
    private string _donorName;

    [Constructible]
    public BioVial() : base(0x0E24)
    {
        Hue = 0x59;
    }

    public override string DefaultName => "bio sample vial";
    public override double DefaultWeight => 0.1;
    public bool Filled => Kind != BioSampleKind.None && Sample?.Length > 0;
    internal int[] SampleValues => Sample;

    private void MigrateFrom(V0Content content)
    {
        _kind = content.Kind;
        _sample = content.Sample;
    }

    internal void ExchangeSample(int[] sample, string donorName)
    {
        Sample = sample;
        DonorName = donorName;
        InvalidateProperties();
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        if (Filled)
        {
            list.Add($"{"Sample"}\t{SampleName(Kind)}");
            if (!string.IsNullOrEmpty(DonorName))
            {
                list.Add($"{"Donor"}\t{DonorName}");
            }

            if (Kind == BioSampleKind.Mimic && Sample.Length >= 4)
            {
                list.Add($"{"Special move"}\t{BioConstruct.TraitName((BioTrait)Sample[3])}");
            }
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile player || !BioEngineeringBook.CanUseItem(player, this) ||
            !BioEngineeringBook.CheckTraining(player))
        {
            return;
        }

        if (Filled)
        {
            player.SendMessage("Use a vial set to store this sample.");
            return;
        }

        player.SendGump(new SampleGump(this, player));
    }

    private void OnChooseSample(PlayerMobile player, BioSampleKind kind)
    {
        if (!Filled && BioEngineeringBook.CanUseItem(player, this) && BioEngineeringBook.CheckTraining(player))
        {
            player.SendMessage("Target a nearby wild creature.");
            player.BeginTarget(SampleRange, false, TargetFlags.None, (from, target) =>
            {
                var owner = (PlayerMobile)from;
                var creature = target as BaseCreature;
                if (TryPrepareSample(owner, kind, creature, out var sample))
                {
                    SamplePreviewGump.DisplayTo(owner, this, kind, creature, sample);
                }
            });
        }
    }

    internal static string SampleName(BioSampleKind kind) => kind switch
    {
        BioSampleKind.Prowess     => "Prowess",
        BioSampleKind.Environment => "Environment",
        BioSampleKind.Mental      => "Mental",
        BioSampleKind.Mimic       => "Mimic",
        _                         => "Unknown"
    };

    // At 4,000 Luck the success chance reaches its ceiling.
    internal static double SampleChance(int luck) => 0.35 + 0.55 * (Math.Clamp(luck, 0, 4000) / 4000.0);

    internal static string FormatSampleChance(int luck) => SampleChance(luck).ToString("0.#####%");

    internal static bool IsWildCreature(BaseCreature creature) =>
        creature is { Deleted: false, Alive: true, Player: false, Controlled: false, Summoned: false, IsDeadPet: false }
        && creature.GetMaster() == null && creature is not BaseVendor && creature.CanBeDamaged();

    private static bool CanReach(PlayerMobile player, BaseCreature creature) =>
        creature.Map != null && creature.Map != Map.Internal && creature.Map == player.Map &&
        player.InRange(creature, SampleRange) && player.CanSee(creature) && player.InLOS(creature);

    internal bool TrySample(PlayerMobile player, BioSampleKind kind, BaseCreature creature, double roll) =>
        TryPrepareSample(player, kind, creature, out var sample) && CommitSample(player, kind, creature, sample, roll);

    private bool TryPrepareSample(PlayerMobile player, BioSampleKind kind, BaseCreature creature, out int[] sample)
    {
        sample = null;
        if (Filled || !BioEngineeringBook.CanUseItem(player, this) || !BioEngineeringBook.CheckTraining(player))
        {
            return false;
        }

        if (!IsWildCreature(creature) || !CanReach(player, creature))
        {
            player.SendMessage("Choose a nearby living wild creature you can see.");
            return false;
        }

        sample = Capture(kind, creature);
        if (sample == null)
        {
            player.SendMessage("This creature cannot provide that sample.");
            return false;
        }

        return true;
    }

    private bool CommitSample(PlayerMobile player, BioSampleKind kind, BaseCreature creature, int[] sample, double roll)
    {
        if (roll >= SampleChance(player.Luck))
        {
            Delete();
            player.SendMessage("The sample spoils. The vial is lost.");
            return false;
        }

        Sample = sample;
        DonorName = creature.Name;
        Kind = kind;
        BioDonorJournal.RecordSample(player, kind, creature);
        player.SendMessage($"You gather a {SampleName(kind)} sample.");
        return true;
    }

    internal static int[] Capture(BioSampleKind kind, BaseCreature creature) => kind switch
    {
        BioSampleKind.Prowess =>
        [
            creature.RawStr, creature.HitsMaxSeed > 0 ? creature.HitsMaxSeed : creature.RawStr,
            creature.DamageMin, creature.DamageMax, creature.PhysicalResistanceSeed, creature.VirtualArmor,
            creature.Skills.Wrestling.BaseFixedPoint, creature.Skills.Tactics.BaseFixedPoint,
            creature.Skills.Anatomy.BaseFixedPoint,
            creature.Skills.Swords.BaseFixedPoint, creature.Skills.Fencing.BaseFixedPoint,
            creature.Skills.Macing.BaseFixedPoint, creature.Skills.Archery.BaseFixedPoint,
            creature.Skills.Parry.BaseFixedPoint, creature.Skills.Healing.BaseFixedPoint
        ],
        BioSampleKind.Environment =>
        [
            creature.RawDex, creature.StamMaxSeed > 0 ? creature.StamMaxSeed : creature.RawDex,
            creature.FireResistSeed, creature.ColdResistSeed, creature.Skills.Poisoning.BaseFixedPoint
        ],
        BioSampleKind.Mental =>
        [
            creature.RawInt, creature.ManaMaxSeed > 0 ? creature.ManaMaxSeed : creature.RawInt,
            creature.EnergyResistSeed, creature.PoisonResistSeed, creature.Skills.Magery.BaseFixedPoint,
            creature.Skills.EvalInt.BaseFixedPoint, creature.Skills.Meditation.BaseFixedPoint,
            creature.Skills.MagicResist.BaseFixedPoint
        ],
        BioSampleKind.Mimic when !creature.IsBodyMod &&
                                 (creature.Body.IsHuman || creature.Body.IsMonster || creature.Body.IsAnimal) =>
        [
            creature.Body.BodyID, creature.Hue, creature.Female ? 1 : 0,
            (int)BioConstruct.ChooseTrait(creature),
            creature.GetAngerSound(), creature.GetAttackSound(), creature.GetHurtSound(),
            creature.GetDeathSound(), creature.GetIdleSound()
        ],
        _ => null
    };

    private sealed class SampleGump(BioVial vial, PlayerMobile player) : DynamicGump(60, 55)
    {
        public override bool Singleton => true;

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();
            builder.AddBackground(0, 0, 337, 186, 3500);
            builder.AddLabel(22, 17, 0x34, "Choose a sample");
            AddOption(ref builder, 45, BioSampleKind.Prowess, "Prowess (power and combat)");
            AddOption(ref builder, 71, BioSampleKind.Environment, "Environment (speed and defense)");
            AddOption(ref builder, 97, BioSampleKind.Mental, "Mental (magic and defense)");
            AddOption(ref builder, 123, BioSampleKind.Mimic, "Mimic (form, voice, and special move)");
            builder.AddLabel(22, 153, BioPreview.LabelHue, $"Luck {player.Luck:N0}   Success {FormatSampleChance(player.Luck)}");
        }

        private static void AddOption(ref DynamicGumpBuilder builder, int y, BioSampleKind kind, string text)
        {
            builder.AddButton(22, y, 4005, 4007, (int)kind);
            builder.AddLabel(61, y, 0, text);
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile == player && info.ButtonID >= (int)BioSampleKind.Prowess &&
                info.ButtonID <= (int)BioSampleKind.Mimic)
            {
                vial.OnChooseSample(player, (BioSampleKind)info.ButtonID);
            }
        }
    }

    internal sealed class SamplePreviewGump(
        PlayerMobile player, BioVial vial, BioSampleKind kind, BaseCreature creature, int[] sample
    ) : DynamicGump(60, 55)
    {
        private readonly double _chance = SampleChance(player.Luck);
        private readonly int _luck = player.Luck;

        public override bool Singleton => true;

        internal static void DisplayTo(
            PlayerMobile player, BioVial vial, BioSampleKind kind, BaseCreature creature, int[] sample
        )
        {
            if (player.NetState != null)
            {
                player.SendGump(new SamplePreviewGump(player, vial, kind, creature, sample));
            }
        }

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();
            builder.AddBackground(0, 0, 548, 223, 3500);
            builder.AddLabelCropped(22, 16, 504, 23, 0x34, $"{SampleName(kind)} from {creature.Name}");
            builder.AddLabel(22, 43, BioPreview.LabelHue, $"Luck {_luck:N0}   Success {FormatSampleChance(_luck)}");
            BioPreview.AppendSample(ref builder, kind, sample, 22, 72);
            builder.AddLabel(22, 157, BioPreview.LabelHue, "The vial breaks if the sample spoils.");
            builder.AddButton(22, 184, 4005, 4007, 1);
            builder.AddLabel(61, 184, BioPreview.LabelHue, "Collect sample");
            builder.AddButton(257, 184, 4017, 4019, 0);
            builder.AddLabel(295, 184, BioPreview.LabelHue, "Cancel");
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile != player || info.ButtonID != 1 ||
                !vial.TryPrepareSample(player, kind, creature, out var current))
            {
                return;
            }

            if (SampleChance(player.Luck) != _chance || !sample.AsSpan().SequenceEqual(current))
            {
                player.SendMessage("The sample conditions changed. Review the new result.");
                DisplayTo(player, vial, kind, creature, current);
                return;
            }

            vial.CommitSample(player, kind, creature, current, Utility.RandomDouble());
        }
    }

}
