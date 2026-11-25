using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Engines.BioEngineering;

public static class BioRefit
{
    public static void Initialize() => CommandSystem.Register("BioRefit", AccessLevel.Player, OpenCommand);

    [Usage("BioRefit")]
    [Description("Refit your bio construct's frame or modules at an anvil and forge.")]
    private static void OpenCommand(CommandEventArgs args)
    {
        if (args.Mobile is not PlayerMobile player || !BioConstruct.CanUseRefitBench(player))
        {
            return;
        }

        player.SendMessage("Target your bio construct.");
        player.BeginTarget(2, false, TargetFlags.None, (_, target) =>
        {
            if (target is BioConstruct construct)
            {
                DisplayTo(player, construct);
            }
            else
            {
                player.SendMessage("Target your bio construct.");
            }
        });
    }

    internal static void DisplayTo(PlayerMobile player, BioConstruct construct)
    {
        if (player.NetState != null && construct.CanRefit(player))
        {
            player.SendGump(new RefitGump(player, construct));
        }
    }

    internal sealed class RefitGump(PlayerMobile owner, BioConstruct construct, BioRefitQuote? quote = null)
        : DynamicGump(55, 52)
    {
        private const int Hue = 0x481;
        private const int ConfirmButton = 20;
        private const int BackButton = 21;

        public override bool Singleton => true;

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();
            builder.AddBackground(0, 0, 600, 390, 0x13BE);
            builder.AddLabel(24, 18, Hue, "Bio construct refit");
            if (quote is { } offer)
            {
                Preview(ref builder, offer);
                return;
            }

            var state = construct.RefitState;
            builder.AddLabel(24, 51, Hue, "Frame alloy");
            AddFrame(ref builder, 76, BioRefitChange.IronFrame, BioFrameAlloy.Iron, state.Frame);
            AddFrame(ref builder, 102, BioRefitChange.BronzeFrame, BioFrameAlloy.Bronze, state.Frame);
            AddFrame(ref builder, 128, BioRefitChange.AgapiteFrame, BioFrameAlloy.Agapite, state.Frame);
            AddFrame(ref builder, 154, BioRefitChange.ShadowIronFrame, BioFrameAlloy.ShadowIron, state.Frame);

            builder.AddLabel(24, 195, Hue, "Combat module");
            if (construct.Trait == BioTrait.None)
            {
                builder.AddLabel(24, 220, Hue, "This donor has no special move for a circuit.");
            }
            else
            {
                var circuit = state.FocusedCircuit ? BioRefitChange.RemoveCircuit : BioRefitChange.InstallCircuit;
                AddAction(ref builder, 24, 220, (int)circuit, ChangeName(circuit));
                builder.AddLabel(284, 220, Hue, BioConstruct.CircuitTiming(state.FocusedCircuit));
            }

            builder.AddLabel(24, 262, Hue, "Utility module");
            var regulator = state.StaminaRegulator ? BioRefitChange.RemoveRegulator : BioRefitChange.InstallRegulator;
            AddAction(ref builder, 24, 287, (int)regulator, ChangeName(regulator));
            builder.AddLabel(284, 287, Hue, state.StaminaRegulator ? "Installed" : CostLabel(regulator));
            builder.AddLabel(24, 344, Hue, "Changes take effect on eligible wild creatures.");
        }

        private static void Preview(ref DynamicGumpBuilder builder, BioRefitQuote offer)
        {
            builder.AddLabel(24, 59, Hue, ChangeName(offer.Change));
            builder.AddLabel(24, 92, Hue, CostLabel(offer.Change));
            var result = offer.Result;
            switch (offer.Change)
            {
                case >= BioRefitChange.IronFrame and <= BioRefitChange.ShadowIronFrame:
                    builder.AddLabel(24, 134, Hue, BioConstruct.FrameEffect(result.Frame));
                    builder.AddLabel(24, 158, Hue, BioConstruct.FrameTiming(result.Frame));
                    break;
                case BioRefitChange.InstallCircuit:
                case BioRefitChange.RemoveCircuit:
                    builder.AddLabel(24, 134, Hue, BioConstruct.TraitName(offer.Construct.Trait));
                    builder.AddLabel(24, 158, Hue, $"Now  {BioConstruct.CircuitTiming(!result.FocusedCircuit)}");
                    builder.AddLabel(24, 182, Hue, $"After refit  {BioConstruct.CircuitTiming(result.FocusedCircuit)}");
                    break;
                case BioRefitChange.InstallRegulator:
                    builder.AddLabel(24, 134, Hue, BioConstruct.RegulatorEffect);
                    builder.AddLabel(24, 158, Hue, BioConstruct.RegulatorTiming);
                    break;
                case BioRefitChange.RemoveRegulator:
                    builder.AddLabel(24, 134, Hue, "Removes the stamina recovery ability.");
                    break;
            }

            builder.AddLabel(24, 227, Hue, "Removed parts yield no ingots.");
            AddAction(ref builder, 24, 339, BackButton, "Back");
            AddAction(ref builder, 376, 339, ConfirmButton, "Confirm refit");
        }

        private static void AddFrame(
            ref DynamicGumpBuilder builder, int y, BioRefitChange change, BioFrameAlloy frame, BioFrameAlloy current
        )
        {
            var name = BioConstruct.FrameName(frame);
            if (frame == current)
            {
                builder.AddLabel(59, y, Hue, name);
                builder.AddLabel(284, y, Hue, "Installed");
            }
            else
            {
                AddAction(ref builder, 24, y, (int)change, name);
                builder.AddLabel(284, y, Hue, CostLabel(change));
            }
        }

        private static string ChangeName(BioRefitChange change) => change switch
        {
            BioRefitChange.IronFrame       => "Fit Iron frame",
            BioRefitChange.BronzeFrame     => "Fit Bronze frame",
            BioRefitChange.AgapiteFrame    => "Fit Agapite frame",
            BioRefitChange.ShadowIronFrame => "Fit Shadow Iron frame",
            BioRefitChange.InstallCircuit  => "Install Focused Circuit",
            BioRefitChange.RemoveCircuit   => "Remove Focused Circuit",
            BioRefitChange.InstallRegulator => "Install Stamina Regulator",
            BioRefitChange.RemoveRegulator  => "Remove Stamina Regulator",
            _                              => ""
        };

        private static string CostLabel(BioRefitChange change)
        {
            var (_, count, material) = BioConstruct.RefitCost(change);
            return count == 0 ? "Free" : $"{count} {material}";
        }

        private static void AddAction(ref DynamicGumpBuilder builder, int x, int y, int id, string text)
        {
            builder.AddButton(x, y, 0xFA5, 0xFA7, id);
            builder.AddLabel(x + 35, y, Hue, text);
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile != owner || info.ButtonID == 0)
            {
                return;
            }

            if (quote is { } offer)
            {
                if (info.ButtonID == ConfirmButton)
                {
                    if (construct.TryRefit(owner, offer))
                    {
                        owner.SendMessage("Your construct has been refitted.");
                    }
                    DisplayTo(owner, construct);
                }
                else if (info.ButtonID == BackButton)
                {
                    DisplayTo(owner, construct);
                }
                return;
            }

            if (construct.CanRefit(owner) && construct.QuoteRefit(owner, (BioRefitChange)info.ButtonID) is { } next)
            {
                owner.SendGump(new RefitGump(owner, construct, next));
            }
        }
    }
}
