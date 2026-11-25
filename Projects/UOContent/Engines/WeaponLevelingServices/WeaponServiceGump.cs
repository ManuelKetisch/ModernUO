using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.WeaponLevelingServices;

internal sealed class WeaponServiceGump(
    PlayerMobile owner, WeaponServiceDeed deed, BaseWeapon source, Item template, WeaponServiceOperation operation
) : DynamicGump(50, 50)
{
    private const int LabelHue = 0x481;
    private const int ConfirmButton = 1;
    private const int ChangeTypeButton = 2;

    private readonly WeaponServiceTransfer.PreviewStamp _expected = new(source, template);

    public override bool Singleton => true;

    internal static void Show(
        PlayerMobile owner, WeaponServiceDeed deed, BaseWeapon source, Item template, WeaponServiceOperation operation
    )
    {
        if (owner.NetState != null && deed.CanUse(owner) &&
            WeaponServiceTransfer.CanPerform(owner, source, template, operation))
        {
            owner.SendGump(new WeaponServiceGump(owner, deed, source, template, operation));
        }
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 484, 270, 0x13BE);
        builder.AddLabel(24, 19, LabelHue, operation switch
        {
            WeaponServiceOperation.WaiveTag => "Remove artifact tag",
            WeaponServiceOperation.ChangeType => "Change weapon type",
            WeaponServiceOperation.OneHanded => "Make a one-handed weapon",
            _ => "Re-layer weapon"
        });

        builder.AddLabel(24, 52, LabelHue, "Your weapon");
        AddItemName(ref builder, source, 52);

        if (template != null)
        {
            builder.AddLabel(24, 79, LabelHue, "Template");
            AddItemName(ref builder, template, 79);
        }

        switch (operation)
        {
            case WeaponServiceOperation.WaiveTag:
                builder.AddLabel(24, 112, LabelHue, "Future upgrades cost the regular amount.");
                builder.AddLabel(24, 137, LabelHue, "Your weapon keeps its type and progress.");
                builder.AddButton(23, 219, 0xFA5, 0xFA7, ChangeTypeButton);
                builder.AddLabel(57, 219, LabelHue, "Choose a different weapon type");
                break;
            case WeaponServiceOperation.ChangeType:
                builder.AddLabel(24, 112, LabelHue, "Keep your progress and added properties.");
                builder.AddLabel(24, 137, LabelHue, "The new type sets the damage, speed and specials.");
                builder.AddLabel(24, 162, LabelHue, "Both original items are used to make the replacement.");
                break;
            case WeaponServiceOperation.OneHanded:
                builder.AddLabel(24, 112, LabelHue, "Keep your weapon and use it with one hand.");
                builder.AddLabel(24, 137, LabelHue, "The blank template is consumed.");
                break;
            case WeaponServiceOperation.Relayer:
                builder.AddLabel(24, 112, LabelHue, "Keep bonuses that work on the new equipment.");
                builder.AddLabel(24, 137, LabelHue, "Weapon hits and further weapon training are lost.");
                builder.AddLabel(24, 162, LabelHue, "Both original items are used to make the replacement.");
                break;
        }

        if (operation is WeaponServiceOperation.Relayer or WeaponServiceOperation.ChangeType &&
            template is not BaseRanged && WeaponServiceTransfer.HasRangedModifiers(source))
        {
            var ranged = (BaseRanged)source;
            var lost = ranged.Balanced && ranged.Velocity != 0 ? "Balanced and Velocity" :
                       ranged.Balanced ? "Balanced" : "Velocity";
            builder.AddLabel(24, 187, LabelHue, $"Lost on the new item: {lost}.");
        }

        var x = operation == WeaponServiceOperation.WaiveTag ? 307 : 23;
        builder.AddButton(x, 219, 0xFA5, 0xFA7, ConfirmButton);
        builder.AddLabel(x + 34, 219, LabelHue, "Confirm");
    }

    private static void AddItemName(ref DynamicGumpBuilder builder, Item item, int y)
    {
        if (string.IsNullOrEmpty(item.Name))
        {
            builder.AddHtmlLocalized(143, y, 313, 24, item.LabelNumber, 0xFFFFFF);
        }
        else
        {
            builder.AddLabelCropped(143, y, 313, 24, LabelHue, item.Name);
        }

        builder.AddItemProperty(item.Serial);
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender != owner.NetState || sender.Mobile != owner)
        {
            return;
        }

        if (info.ButtonID == ChangeTypeButton && operation == WeaponServiceOperation.WaiveTag)
        {
            deed.ChooseTemplate(owner, source);
        }
        else if (info.ButtonID == ConfirmButton)
        {
            WeaponServiceTransfer.Complete(owner, deed, source, template, operation, _expected);
        }
    }
}
