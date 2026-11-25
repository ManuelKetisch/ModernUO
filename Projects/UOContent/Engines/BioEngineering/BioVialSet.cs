using ModernUO.Serialization;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Engines.BioEngineering;

[SerializationGenerator(1)]
public partial class BioVialSet : Item
{
    [SerializableField(0, getter: "private", setter: "private")]
    private int[] _prowess;

    [SerializableField(1, getter: "private", setter: "private")]
    private int[] _environment;

    [SerializableField(2, getter: "private", setter: "private")]
    private int[] _mental;

    [SerializableField(3, getter: "private", setter: "private")]
    private int[] _mimic;

    [SerializableField(4, getter: "private", setter: "private")]
    [SaveFlag(nameof(HasSampleDonors))]
    private string[] _sampleDonors;

    [SerializableField(5, getter: "internal", setter: "private")]
    [SaveFlag(nameof(HasUsedSwap))]
    private bool _swapUsed;

    internal int SampleRevision { get; private set; }

    private bool HasSampleDonors() => _sampleDonors != null;
    private bool HasUsedSwap() => _swapUsed;

    [Constructible]
    public BioVialSet() : base(0x185B)
    {
        Hue = 0x59;
    }

    private void MigrateFrom(V0Content content)
    {
        _prowess = content.Prowess;
        _environment = content.Environment;
        _mental = content.Mental;
        _mimic = content.Mimic;
    }

    public override string DefaultName => "bio vial set";
    public override double DefaultWeight => 1.0;
    public bool Complete => Prowess?.Length > 0 && Environment?.Length > 0 && Mental?.Length > 0 && Mimic?.Length > 0;

    internal bool HasSamples => Prowess?.Length > 0 || Environment?.Length > 0 || Mental?.Length > 0 || Mimic?.Length > 0;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        AddSampleStatus(list, BioSampleKind.Prowess);
        AddSampleStatus(list, BioSampleKind.Environment);
        AddSampleStatus(list, BioSampleKind.Mental);
        AddSampleStatus(list, BioSampleKind.Mimic);
    }

    private void AddSampleStatus(IPropertyList list, BioSampleKind kind)
    {
        var label = BioVial.SampleName(kind);
        var status = SampleStatus(kind);
        list.Add($"{label}\t{status}");
    }

    internal string SampleStatus(BioSampleKind kind) =>
        GetSample(kind)?.Length > 0 ? GetDonor(kind) ?? "Donor unrecorded" : "Empty";

    private string GetDonor(BioSampleKind kind)
    {
        var index = (int)kind - 1;
        return _sampleDonors != null && (uint)index < (uint)_sampleDonors.Length ? _sampleDonors[index] : null;
    }

    private int[] GetSample(BioSampleKind kind) => kind switch
    {
        BioSampleKind.Prowess     => Prowess,
        BioSampleKind.Environment => Environment,
        BioSampleKind.Mental      => Mental,
        BioSampleKind.Mimic       => Mimic,
        _                         => null
    };

    internal bool TryProject(out BioProjection projection) =>
        BioPreview.TryProject(Prowess, Environment, Mental, Mimic, out projection);

    internal bool CanUseWorkbench(PlayerMobile player) =>
        BioEngineeringBook.CanUseItem(player, this) && BioEngineeringBook.CheckTraining(player);

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile player && CanUseWorkbench(player))
        {
            BioWorkbenchGump.DisplayTo(player, this);
        }
    }

    internal void TargetVial(PlayerMobile player, bool replace)
    {
        player.SendMessage(replace
            ? "Target a filled vial to swap with a stored sample of the same type."
            : "Target a filled sample vial in your backpack.");
        player.BeginTarget(2, false, TargetFlags.None, (mobile, target) =>
        {
            if (TryStore((PlayerMobile)mobile, target as BioVial, replace))
            {
                BioWorkbenchGump.DisplayTo((PlayerMobile)mobile, this);
            }
        });
    }

    internal void TargetInfusion(PlayerMobile player, int expectedRevision)
    {
        if (!IsCurrentPreview(player, expectedRevision))
        {
            return;
        }

        if (!TryProject(out _))
        {
            player.SendMessage("Collect four compatible samples before infusion.");
            return;
        }

        player.SendMessage("Target your unfinished bio construct.");
        player.BeginTarget(2, false, TargetFlags.None, (mobile, target) =>
        {
            if (mobile is PlayerMobile engineer && CanUseWorkbench(engineer) &&
                IsCurrentPreview(engineer, expectedRevision))
            {
                TryInfuse(engineer, target as BioConstruct);
            }
        });
    }

    private bool IsCurrentPreview(PlayerMobile player, int expectedRevision)
    {
        if (SampleRevision == expectedRevision)
        {
            return true;
        }

        player.SendMessage("The samples changed. Review the final form before infusing.");
        BioWorkbenchGump.DisplayTo(player, this);
        return false;
    }

    internal bool TryAdd(PlayerMobile player, BioVial vial) => TryStore(player, vial, false);

    internal bool TrySwap(PlayerMobile player, BioVial vial) => TryStore(player, vial, true);

    private bool TryStore(PlayerMobile player, BioVial vial, bool replace)
    {
        if (!CanUseWorkbench(player))
        {
            return false;
        }

        if (vial is not { Filled: true } || !BioEngineeringBook.CanUseItem(player, vial) ||
            vial.Kind is < BioSampleKind.Prowess or > BioSampleKind.Mimic)
        {
            player.SendMessage("Choose a filled sample vial in your backpack.");
            return false;
        }

        if (replace && SwapUsed)
        {
            player.SendMessage("You have already used this set's sample swap.");
            return false;
        }

        var previous = GetSample(vial.Kind);
        var previousDonor = GetDonor(vial.Kind);
        if (replace && previous?.Length is not > 0)
        {
            player.SendMessage("Use Store sample to fill an empty slot.");
            return false;
        }

        if (!replace && previous?.Length > 0)
        {
            player.SendMessage("This set already holds that sample. Use Swap sample to exchange it.");
            return false;
        }

        SetSample(vial.Kind, vial.SampleValues, vial.DonorName);
        SampleRevision++;
        var sampleName = BioVial.SampleName(vial.Kind);
        if (replace)
        {
            vial.ExchangeSample(previous, previousDonor);
            SwapUsed = true;
            player.SendMessage($"You swap the {sampleName} sample. The previous sample is in your vial.");
        }
        else
        {
            vial.Delete();
            player.SendMessage($"You store the {sampleName} sample.");
        }

        ItemID = Complete ? 0x185D : 0x185B;
        InvalidateProperties();
        return true;
    }

    private void SetSample(BioSampleKind kind, int[] values, string donor)
    {
        switch (kind)
        {
            case BioSampleKind.Prowess:
                Prowess = values;
                break;
            case BioSampleKind.Environment:
                Environment = values;
                break;
            case BioSampleKind.Mental:
                Mental = values;
                break;
            case BioSampleKind.Mimic:
                Mimic = values;
                break;
        }

        SampleDonors ??= new string[4];
        SampleDonors[(int)kind - 1] = donor;
        this.MarkDirty();
    }

    internal bool TryInfuse(PlayerMobile player, BioConstruct creature)
    {
        if (!CanUseWorkbench(player))
        {
            return false;
        }

        if (!Complete)
        {
            player.SendMessage("Collect all four samples first.");
            return false;
        }

        if (creature == null || !creature.CanReceiveSamplesFrom(player))
        {
            player.SendMessage("Choose your own nearby unfinished construct.");
            return false;
        }

        if (!creature.ApplySamples(Prowess, Environment, Mental, Mimic, SampleDonors))
        {
            player.SendMessage("These samples cannot be used together.");
            return false;
        }

        Delete();
        player.SendMessage("The construct takes its final form.");
        player.SendMessage($"Say '{creature.Name} sheet' to inspect it.");
        player.SendMessage($"Say '{creature.Name} skills' to manage its training.");
        return true;
    }
}
