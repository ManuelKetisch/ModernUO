using Server.Gumps;
using Server.Network;

namespace Server.Mobiles;

internal sealed class PetCombatRoleGump(
    BaseCreature pet, Mobile owner, PetCombatRole current, PetCombatRole selected
) : DynamicGump(90, 80)
{
    private const int TextHue = 0x480;
    private const int ApplyButton = 4;
    private const int BackButton = 5;

    public override bool Singleton => true;

    internal static void DisplayTo(Mobile owner, BaseCreature pet, PetCombatRole selected = PetCombatRole.None)
    {
        if (owner?.NetState == null || !pet.CanManagePetProgression(owner) ||
            pet.PetLevel < BaseCreature.PetCombatRoleLevel)
        {
            return;
        }

        owner.SendGump(new PetCombatRoleGump(pet, owner, pet.PetRole, selected));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 510, 280, 9270);
        builder.AddAlphaRegion(12, 12, 486, 256);
        builder.AddLabel(24, 18, TextHue, "Combat role");
        builder.AddLabel(24, 43, TextHue, $"Current role: {current}");
        builder.AddLabel(24, 69, TextHue, "Roles activate in fights against wild creatures.");

        if (selected == PetCombatRole.None)
        {
            AddChoice(ref builder, PetCombatRole.Guardian, 103);
            AddChoice(ref builder, PetCombatRole.Striker, 142);
            AddChoice(ref builder, PetCombatRole.Scout, 181);
            builder.AddButton(24, 231, 4017, 4019, 0);
            builder.AddLabel(59, 231, TextHue, "Back to training");
            return;
        }

        builder.AddLabel(24, 106, TextHue, $"Choose {selected}?");
        builder.AddLabel(24, 131, TextHue, Describe(selected));
        builder.AddLabel(24, 165, TextHue, "You can change roles outside combat.");
        builder.AddButton(24, 231, 4005, 4007, ApplyButton);
        builder.AddLabel(59, 231, TextHue, "Choose role");
        builder.AddButton(249, 231, 4017, 4019, BackButton);
        builder.AddLabel(284, 231, TextHue, "Back to roles");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile != owner || !pet.CanManagePetProgression(owner) ||
            pet.PetLevel < BaseCreature.PetCombatRoleLevel)
        {
            return;
        }

        switch (info.ButtonID)
        {
            case >= (int)PetCombatRole.Guardian and <= (int)PetCombatRole.Scout
                when selected == PetCombatRole.None:
                DisplayTo(owner, pet, (PetCombatRole)info.ButtonID);
                break;
            case ApplyButton when selected != PetCombatRole.None:
                if (pet.PetRole != current)
                {
                    owner.SendMessage("Your pet's role changed. Review your choice.");
                    DisplayTo(owner, pet);
                }
                else if (pet.TryChoosePetCombatRole(owner, selected))
                {
                    PetProgressionGump.DisplayTo(owner, pet);
                }
                else
                {
                    DisplayTo(owner, pet, selected);
                }

                break;
            case BackButton when selected != PetCombatRole.None:
                DisplayTo(owner, pet);
                break;
            case 0:
                PetProgressionGump.DisplayTo(owner, pet);
                break;
        }
    }

    private static void AddChoice(ref DynamicGumpBuilder builder, PetCombatRole role, int y)
    {
        builder.AddButton(24, y, 4005, 4007, (int)role);
        builder.AddLabel(59, y, TextHue, $"{role}");
        builder.AddLabel(150, y, TextHue, Describe(role));
    }

    private static string Describe(PetCombatRole role) => role switch
    {
        PetCombatRole.Guardian => "Heals you when its target attacks you nearby.",
        PetCombatRole.Striker  => "Adds a follow-up hit to its melee attack.",
        PetCombatRole.Scout    => "Drains its target's stamina with a melee hit.",
        _                      => ""
    };
}
