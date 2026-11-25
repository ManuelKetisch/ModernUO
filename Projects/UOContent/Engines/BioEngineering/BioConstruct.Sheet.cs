using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Engines.BioEngineering;

public static class BioConstructSheet
{
    public static void Initialize() => CommandSystem.Register("BioSheet", AccessLevel.Player, OpenCommand);

    [Usage("BioSheet")]
    [Description("Inspect a nearby bio construct you own.")]
    private static void OpenCommand(CommandEventArgs args)
    {
        if (args.Mobile is not PlayerMobile { Alive: true, NetState: not null } owner)
        {
            return;
        }

        owner.SendMessage("Target your bio construct.");
        owner.BeginTarget(2, false, TargetFlags.None, (_, target) =>
        {
            if (target is not BioConstruct bio || !bio.TryOpenSheet(owner))
            {
                owner.SendMessage("Choose your own nearby living, assembled bio construct.");
            }
        });
    }
}

public partial class BioConstruct
{
    internal bool TryOpenSheet(Mobile from)
    {
        if (!CanManage(from) || from.NetState is not { Running: true } state || state.Mobile != from)
        {
            return false;
        }

        from.SendGump(new BioSheetGump(this));
        return true;
    }

    internal sealed class BioSheetGump(BioConstruct bio) : DynamicGump(62, 54)
    {
        private const int TextHue = 0x481;
        private const int TitleHue = 0x34;
        private const int DonorPage = 1;
        private const int EquipmentPage = 2;
        private const int ModulePage = 3;
        private const int EquipmentColumnRows = 12;

        public override bool Singleton => true;

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();
            builder.AddBackground(0, 0, 548, 432, 0x13BE);
            builder.AddLabel(24, 18, TitleHue, "Bio construct sheet");
            builder.AddLabelCropped(24, 45, 500, 24, TextHue, bio.Name);
            AddTab(ref builder, 24, DonorPage, "Donors");
            AddTab(ref builder, 170, EquipmentPage, "Equipment");
            AddTab(ref builder, 340, ModulePage, "Refits");

            AddDonors(ref builder);
            AddEquipment(ref builder);
            AddModules(ref builder);
        }

        private static void AddTab(ref DynamicGumpBuilder builder, int x, int page, string label)
        {
            builder.AddButton(x, 78, 0xFA5, 0xFA7, 0, GumpButtonType.Page, page);
            builder.AddLabel(x + 35, 78, TextHue, label);
        }

        private void AddDonors(ref DynamicGumpBuilder builder)
        {
            builder.AddPage(DonorPage);
            var y = 126;
            for (var kind = BioSampleKind.Prowess; kind <= BioSampleKind.Mimic; kind++)
            {
                builder.AddLabel(24, y, TitleHue, BioVial.SampleName(kind));
                builder.AddLabelCropped(164, y, 358, 24, TextHue, bio.DonorName(kind));
                y += 30;
            }

            builder.AddLabel(24, 263, TitleHue, "Current stats");
            builder.AddLabel(24, 293, TextHue, $"Strength {bio.Str}   Dexterity {bio.Dex}   Intelligence {bio.Int}");
            builder.AddLabel(24, 322, TextHue,
                $"Health {bio.Hits}/{bio.HitsMax}   Stamina {bio.Stam}/{bio.StamMax}   Mana {bio.Mana}/{bio.ManaMax}");
        }

        private void AddEquipment(ref DynamicGumpBuilder builder)
        {
            builder.AddPage(EquipmentPage);
            if (!bio.HasHumanForm)
            {
                builder.AddLabel(24, 126, TextHue, "This form cannot wear equipment.");
                return;
            }

            var count = 0;
            for (var layer = Layer.FirstValid; layer <= Layer.LastUserValid; layer++)
            {
                if (bio.FindItemOnLayer(layer) is not { Deleted: false } item || !IsEquipment(item))
                {
                    continue;
                }

                var x = count < EquipmentColumnRows ? 24 : 278;
                var y = 118 + count % EquipmentColumnRows * 22;
                if (string.IsNullOrEmpty(item.Name))
                {
                    builder.AddHtmlLocalized(x, y, 240, 22, item.LabelNumber, 0xFFFFFF);
                }
                else
                {
                    builder.AddLabelCropped(x, y, 240, 22, TextHue, item.Name);
                }

                builder.AddItemProperty(item.Serial);
                count++;
            }

            if (count == 0)
            {
                builder.AddLabel(24, 126, TextHue, "No equipment worn.");
            }

            if (bio.Weapon is BaseWeapon { IsWeaponLevelingEnabled: true } weapon && weapon.Parent == bio)
            {
                builder.AddLabel(24, 386, TextHue,
                    $"Weapon level {weapon.WeaponLevel}   {weapon.WeaponExperience:N0} XP   Unspent points {weapon.WeaponLevelingPointsAvailable}");
            }
        }

        private void AddModules(ref DynamicGumpBuilder builder)
        {
            var installed = bio.RefitState;
            builder.AddPage(ModulePage);
            builder.AddLabel(24, 121, TitleHue, $"Frame   {FrameName(installed.Frame)}");
            builder.AddLabelCropped(24, 146, 500, 24, TextHue, FrameEffect(installed.Frame));
            if (installed.Frame != BioFrameAlloy.Iron)
            {
                builder.AddLabel(24, 170, TextHue, FrameTiming(installed.Frame));
            }

            builder.AddLabel(24, 210, TitleHue,
                installed.FocusedCircuit ? "Combat   Focused Circuit" : "Combat   Empty");
            if (bio.Trait != BioTrait.None)
            {
                builder.AddLabel(24, 236, TextHue, TraitName(bio.Trait));
                builder.AddLabelCropped(24, 260, 500, 24, TextHue, TraitDescription(bio.Trait));
                builder.AddLabel(24, 284, TextHue, CircuitTiming(installed.FocusedCircuit));
            }
            else
            {
                builder.AddLabel(24, 236, TextHue, "No donor move.");
            }

            builder.AddLabel(24, 324, TitleHue,
                installed.StaminaRegulator ? "Utility   Stamina Regulator" : "Utility   Empty");
            if (installed.StaminaRegulator)
            {
                builder.AddLabel(24, 350, TextHue, RegulatorEffect);
                builder.AddLabel(24, 374, TextHue, RegulatorTiming);
            }
        }
    }
}
