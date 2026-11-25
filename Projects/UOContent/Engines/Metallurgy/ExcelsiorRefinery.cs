using Server.Engines.Craft;
using Server.Engines.WeaponLevelingServices;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Engines.Metallurgy;

public static class ExcelsiorRefinery
{
    public static void Initialize() => CommandSystem.Register(
        "RefineCraftGrade", AccessLevel.Player, args =>
        {
            if (args.Mobile is not PlayerMobile { Alive: true, NetState: not null } owner)
            {
                return;
            }

            owner.SendMessage("Choose an ordinary ingot, leather or board stack in your backpack.");
            owner.BeginTarget(-1, false, TargetFlags.None, (_, selected) =>
            {
                if (selected is Item source && CanRefine(owner, source, out var grade))
                {
                    owner.SendGump(new RefineGradeGump(owner, source, source.Amount, grade));
                }
            });
        }
    );

    internal static bool TryRefine(PlayerMobile owner, Item source, int amount, CraftResource grade)
    {
        if (!CanRefine(owner, source, out var current) || current != grade)
        {
            return false;
        }

        if (source.Amount != amount)
        {
            owner.SendMessage("The stack changed. Review it again.");
            return false;
        }

        var data = CraftResources.GetInfo(grade);
        var result = data.ResourceTypes[0].CreateEntityInstance<Item>(amount);
        if (!owner.Backpack.TryDropItem(owner, result, true))
        {
            result.Delete();
            return false;
        }

        source.Consume(amount);
        owner.SendMessage($"You refined {amount} {data.Name}.");
        return true;
    }

    private static bool CanRefine(PlayerMobile owner, Item source, out CraftResource grade)
    {
        grade = ExcelsiorMaterialVariants.VariantOf(source?.GetType() ?? typeof(Item));
        if (grade == CraftResource.None || owner is not { Alive: true, Deleted: false } ||
            !WeaponServiceTransfer.IsInBackpack(owner, source) || source is not
            {
                Amount: > 0, Movable: true, Stackable: true, Nontransferable: false,
                LootType: LootType.Regular, BlessedFor: null, Insured: false
            })
        {
            owner?.SendMessage("Choose an ordinary unbound material stack in your backpack.");
            return false;
        }

        var required = ExcelsiorMaterials.RequiredSkill(grade);
        var family = CraftResources.GetType(grade);
        var skill = family switch
        {
            CraftResourceType.Metal   => owner.Skills.Blacksmith.Base,
            CraftResourceType.Leather => owner.Skills.Tailoring.Base,
            _ => System.Math.Max(owner.Skills.Carpentry.Base, owner.Skills.Fletching.Base)
        };
        if (skill < required)
        {
            owner.SendMessage($"Refining this grade requires {required} in the relevant crafting skill.");
            return false;
        }

        if (family == CraftResourceType.Metal)
        {
            DefBlacksmithy.CheckAnvilAndForge(owner, 2, out var anvil, out var forge);
            if (!anvil || !forge)
            {
                owner.SendMessage("Stand within two steps of an anvil and a forge.");
                return false;
            }
        }

        return true;
    }

    private sealed class RefineGradeGump(PlayerMobile owner, Item source, int amount, CraftResource grade)
        : DynamicGump(55, 55)
    {
        public override bool Singleton => true;

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();
            builder.AddBackground(0, 0, 405, 151, 0x13BE);
            builder.AddLabel(22, 20, 0x481, "Refine material grade");
            builder.AddLabel(22, 51, 0x481, $"Prepare {amount} {CraftResources.GetName(grade)}.");
            builder.AddLabel(22, 77, 0x481, "This uses the selected stack.");
            builder.AddButton(22, 111, 0xFA5, 0xFA7, 1);
            builder.AddLabel(57, 111, 0x481, "Refine");
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender == owner.NetState && sender.Mobile == owner && info.ButtonID == 1)
            {
                TryRefine(owner, source, amount, grade);
            }
        }
    }
}
