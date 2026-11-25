using System;
using Server.Engines.BioEngineering;
using Server.Engines.PetProgression;
using Server.Gumps;
using Server.Items;
using Server.Network;

namespace Server.Engines.Playtesting;

internal sealed class PlaytestGump(PlaytestGrant grant) : DynamicGump(70, 60)
{
    private const int Text = 0x481;
    private const int Check = 1;
    private const int Stats = 2;
    private const int Skill = 3;
    private const int FirstItem = 20;

    private static readonly (string Label, Func<Item> Create)[] _items =
    [
        ("Longsword", () => new Longsword()),
        ("Halberd", () => new Halberd()),
        ("War hammer", () => new WarHammer()),
        ("Plate chest", () => new PlateChest()),
        ("Bow", () => new Bow()),
        ("100 arrows", () => new Arrow(100)),
        ("Weapon level deed", () => new WeaponLevelDeed()),
        ("Ten-level weapon deed", () => new WeaponLevelBoostDeed()),
        ("Weapon change deed", () => new WeaponChangeDeed()),
        ("Weapon relayer deed", () => new WeaponRelayerDeed()),
        ("Bio handbook", () => new BioEngineeringBook()),
        ("Bio tool", () => new BioEngineeringTool()),
        ("Organic matter sack", () => new BioOrganicMatterSack()),
        ("Bio sample vial", () => new BioVial()),
        ("Bio vial set", () => new BioVialSet()),
        ("Bio creation deed", () => new BioCreationDeed()),
        ("Pet bonding deed", () => new PetBondingDeed()),
        ("Pet resurrection draught", () => new PetResurrectionDraught()),
        ("120 Blacksmithing scroll", () => new PowerScroll(SkillName.Blacksmith, 120)),
        ("Maximum stat cap scroll", () => new StatCapScroll(250)),
        ("100 iron ingots", () => new IronIngot(100)),
        ("100 bronze ingots", () => new BronzeIngot(100))
    ];

    public override bool Singleton => true;

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 548, 470, 0x13BE);
        builder.AddLabel(24, 16, Text, "Playtest tools");
        builder.AddLabelCropped(148, 16, 372, 24, Text, grant.Player.Name);

        builder.AddLabel(24, 54, Text, "Bank check");
        builder.AddTextEntry(120, 54, 110, 24, Text, 0, "10000");
        AddAction(ref builder, 254, 54, Check, "Issue check");

        builder.AddLabel(24, 88, Text, "STR");
        builder.AddTextEntry(60, 88, 45, 24, Text, 1, $"{grant.Player.RawStr}");
        builder.AddLabel(115, 88, Text, "DEX");
        builder.AddTextEntry(153, 88, 45, 24, Text, 2, $"{grant.Player.RawDex}");
        builder.AddLabel(211, 88, Text, "INT");
        builder.AddTextEntry(247, 88, 45, 24, Text, 3, $"{grant.Player.RawInt}");
        AddAction(ref builder, 316, 87, Stats, "Set base stats");

        builder.AddLabel(24, 122, Text, "Skill name");
        builder.AddTextEntry(120, 122, 156, 24, Text, 4, "Blacksmithy");
        builder.AddTextEntry(284, 122, 54, 24, Text, 5, "100");
        AddAction(ref builder, 350, 121, Skill, "Set skill");

        builder.AddLabel(24, 162, Text, "Give an item");
        for (var i = 0; i < _items.Length; i++)
        {
            var x = 24 + i / 11 * 258;
            var y = 194 + i % 11 * 24;
            AddAction(ref builder, x, y, FirstItem + i, _items[i].Label);
        }
    }

    private static void AddAction(ref DynamicGumpBuilder builder, int x, int y, int button, string label)
    {
        builder.AddButton(x, y, 4005, 4007, button);
        builder.AddLabelCropped(x + 34, y, 218, 23, Text, label);
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 0 || !PlaytestConsole.MayUse(grant, sender))
        {
            return;
        }

        var item = info.ButtonID - FirstItem;
        var succeeded = info.ButtonID switch
        {
            Check => PlaytestActions.TryIssueCheck(grant, sender, info.GetTextEntry(0)),
            Stats => PlaytestActions.TrySetStats(grant, sender, info.GetTextEntry(1), info.GetTextEntry(2), info.GetTextEntry(3)),
            Skill => PlaytestActions.TrySetSkill(grant, sender, info.GetTextEntry(4), info.GetTextEntry(5)),
            _ when item >= 0 && item < _items.Length => PlaytestActions.TryGiveItem(grant, sender, _items[item].Create),
            _ => false
        };

        grant.Player.SendMessage(succeeded ? "Playtest change applied." : "Check the values or make room in your backpack.");
        grant.Player.SendGump(new PlaytestGump(grant));
    }
}
