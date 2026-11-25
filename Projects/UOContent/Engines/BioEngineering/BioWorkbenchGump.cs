using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.BioEngineering;

internal sealed class BioWorkbenchGump(BioVialSet set) : DynamicGump(44, 58)
{
    private const int TextHue = 0x481;
    private const int StoreButton = 1;
    private const int SwapButton = 2;
    private const int InfuseButton = 3;

    private readonly int _sampleRevision = set.SampleRevision;

    public override bool Singleton => true;

    internal static void DisplayTo(PlayerMobile player, BioVialSet set)
    {
        if (player.NetState != null && set.CanUseWorkbench(player))
        {
            player.SendGump(new BioWorkbenchGump(set));
        }
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 495, 435, 0x13BE);
        builder.AddLabel(24, 19, TextHue, "Bio workbench");

        var y = 48;
        for (var kind = BioSampleKind.Prowess; kind <= BioSampleKind.Mimic; kind++)
        {
            builder.AddLabel(25, y, TextHue, BioVial.SampleName(kind));
            builder.AddLabelCropped(150, y, 320, 23, TextHue, set.SampleStatus(kind));
            y += 24;
        }

        if (set.TryProject(out var preview))
        {
            builder.AddLabel(24, 157, TextHue, "Final form");
            preview.AppendSummary(ref builder, 24, 182);
            AddAction(ref builder, 343, 384, InfuseButton, "Infuse");
        }
        else if (set.Complete)
        {
            builder.AddLabel(24, 157, TextHue, "One or more samples are incompatible.");
        }
        else
        {
            builder.AddLabel(24, 157, TextHue, "Store all four samples to see the final form.");
        }

        if (!set.Complete)
        {
            AddAction(ref builder, 24, 384, StoreButton, "Store sample");
        }

        if (set.HasSamples && !set.SwapUsed)
        {
            AddAction(ref builder, 174, 384, SwapButton, "Swap sample");
        }
        else if (set.SwapUsed)
        {
            builder.AddLabel(174, 384, TextHue, "Sample swap used");
        }
    }

    private static void AddAction(ref DynamicGumpBuilder builder, int x, int y, int id, string label)
    {
        builder.AddButton(x, y, 0xFA5, 0xFA7, id);
        builder.AddLabel(x + 35, y, TextHue, label);
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is not PlayerMobile player || info.ButtonID == 0 || !set.CanUseWorkbench(player))
        {
            return;
        }

        switch (info.ButtonID)
        {
            case StoreButton when !set.Complete:
                set.TargetVial(player, false);
                break;
            case SwapButton when set.HasSamples && !set.SwapUsed:
                set.TargetVial(player, true);
                break;
            case InfuseButton:
                set.TargetInfusion(player, _sampleRevision);
                break;
        }
    }
}
