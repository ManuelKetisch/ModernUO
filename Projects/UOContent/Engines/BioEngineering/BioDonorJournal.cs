using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Engines.BioEngineering;

public static class BioDonorJournal
{
    private const int SurveyRange = 8;

    public static void Initialize() => CommandSystem.Register("BioJournal", AccessLevel.Player, OpenCommand);

    [Usage("BioJournal")]
    [Description("Survey wild bio engineering donors or view your starter contract.")]
    private static void OpenCommand(CommandEventArgs args)
    {
        if (args.Mobile is PlayerMobile player)
        {
            DisplayTo(player);
        }
    }

    internal static void DisplayTo(PlayerMobile player) => DisplayTo(player, null);

    private static void DisplayTo(PlayerMobile player, DonorReport report)
    {
        if (player.NetState != null && player.CheckAlive())
        {
            player.SendGump(new JournalGump(player, report));
        }
    }

    internal static void RecordSample(PlayerMobile player, BioSampleKind kind, BaseCreature creature)
    {
        if (kind != BioSampleKind.None)
        {
            BioStarterContract.RecordDiscovery(player, creature);
        }
    }

    internal static bool CanSurvey(PlayerMobile player, BaseCreature donor) =>
        player is { Alive: true, Deleted: false, Map: not null } && player.Map != Map.Internal &&
        BioStarterContract.HasResearchSkills(player) && BioVial.IsWildCreature(donor) && donor.Map == player.Map && player.InRange(donor, SurveyRange) &&
        player.CanSee(donor) && player.InLOS(donor);

    internal static bool TrySurvey(PlayerMobile player, BaseCreature donor)
    {
        if (player?.NetState == null || !CanSurvey(player, donor))
        {
            return false;
        }

        BioStarterContract.RecordDiscovery(player, donor);
        DisplayTo(player, new DonorReport(donor));
        return true;
    }

    private static void BeginSurvey(PlayerMobile player)
    {
        if (!BioStarterContract.HasResearchSkills(player))
        {
            player.SendMessage("You need 100 Animal Taming and 100 Tinkering to survey donors.");
            return;
        }

        player.SendMessage("Choose a nearby wild creature to survey.");
        player.BeginTarget(SurveyRange, false, TargetFlags.None, (from, target) =>
        {
            if (!TrySurvey(player, target as BaseCreature))
            {
                player.SendMessage("Choose a living wild creature you can see.");
            }
        });
    }

    internal sealed class DonorReport(BaseCreature donor)
    {
        internal string Name { get; } = donor.DefaultName ?? donor.Name ?? "Wild creature";
        internal int[] Prowess { get; } = BioVial.Capture(BioSampleKind.Prowess, donor);
        internal int[] Environment { get; } = BioVial.Capture(BioSampleKind.Environment, donor);
        internal int[] Mental { get; } = BioVial.Capture(BioSampleKind.Mental, donor);
        internal int[] Mimic { get; } = BioVial.Capture(BioSampleKind.Mimic, donor);
    }

    internal sealed class JournalGump(PlayerMobile owner, DonorReport report) : DynamicGump(70, 55)
    {
        private const int Survey = 1;
        private const int RequestContract = 2;
        public override bool Singleton => true;

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            const int hue = BioPreview.LabelHue;
            builder.AddPage();
            builder.AddBackground(0, 0, 733, 415, 9270);
            builder.AddAlphaRegion(12, 12, 709, 391);
            builder.AddLabel(24, 18, hue, "Bio engineering donor journal");

            builder.AddButton(24, 54, 4005, 4007, Survey);
            builder.AddLabel(59, 54, hue, "Survey a wild creature");
            var (started, surveyed, claimed) = BioStarterContract.GetStatus(owner);
            if (claimed)
            {
                builder.AddLabel(330, 54, hue, "Starter kit claimed");
            }
            else
            {
                var label = !started ? "Start a contract at a tinker"
                    : surveyed < BioStarterContract.RequiredFamilies ? "Starter contract"
                    : "Claim the starter kit at a tinker";
                builder.AddButton(330, 54, 4005, 4007, RequestContract);
                builder.AddLabel(365, 54, hue, label);
            }

            if (started && !claimed)
            {
                builder.AddLabel(365, 80, hue, $"Creature families surveyed: {surveyed} of {BioStarterContract.RequiredFamilies}");
            }

            if (report == null)
            {
                AddGuide(ref builder);
                return;
            }

            builder.AddLabelCropped(24, 94, 675, 24, hue, $"Donor: {report.Name}");
            AddSample(ref builder, BioSampleKind.Prowess, report.Prowess, 24, 130);
            AddSample(ref builder, BioSampleKind.Environment, report.Environment, 371, 130);
            AddSample(ref builder, BioSampleKind.Mental, report.Mental, 24, 253);
            AddSample(ref builder, BioSampleKind.Mimic, report.Mimic, 371, 253);
            builder.AddLabel(24, 376, hue, "Shown values account for the construct's stat limits.");
        }

        private static void AddGuide(ref DynamicGumpBuilder builder)
        {
            const int hue = BioPreview.LabelHue;
            builder.AddLabel(24, 111, hue, "Survey creatures to see what they can contribute to a construct.");
            builder.AddLabel(24, 162, hue, "Look for strong physical fighters for Prowess.");
            builder.AddLabel(24, 193, hue, "Look for quick, element-resistant creatures for Environment.");
            builder.AddLabel(24, 224, hue, "Look for trained spellcasters for Mental.");
            builder.AddLabel(24, 255, hue, "Choose a form and special move you like for Mimic.");
        }

        private static void AddSample(ref DynamicGumpBuilder builder, BioSampleKind kind, int[] sample, int x, int y)
        {
            builder.AddLabel(x, y, BioPreview.LabelHue, BioVial.SampleName(kind));
            if (sample == null)
            {
                builder.AddLabel(x, y + 24, BioPreview.LabelHue, "This creature cannot supply this sample.");
            }
            else
            {
                BioPreview.AppendSample(ref builder, kind, sample, x, y + 24);
                if (kind == BioSampleKind.Mimic)
                {
                    builder.AddLabel(x, y + 93, BioPreview.LabelHue, BioConstruct.TraitDescription((BioTrait)sample[3]));
                }
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile != owner || !owner.CheckAlive())
            {
                return;
            }

            if (info.ButtonID == Survey)
            {
                BeginSurvey(owner);
            }
            else if (info.ButtonID == RequestContract)
            {
                BioStarterContract.TryRequest(owner, BioStarterContract.FindNearbyTinker(owner));
                DisplayTo(owner, report);
            }
        }
    }
}
