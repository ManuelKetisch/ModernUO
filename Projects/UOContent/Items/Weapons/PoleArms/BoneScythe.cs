using System;
using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Items;

[Flippable(0x26BA, 0x26C4)]
[SerializationGenerator(0)]
public sealed partial class BoneScythe : Scythe
{
    private const int BoneHue = 0x47E;

    [Constructible]
    public BoneScythe()
    {
        Resource = CraftResource.None;
        Hue = BoneHue;
    }

    public override string DefaultName => "bone scythe";

    public override int OnCraft(
        int quality, bool makersMark, Mobile from, CraftSystem craftSystem, Type typeRes, BaseTool tool,
        CraftItem craftItem, int resHue
    )
    {
        var result = base.OnCraft(quality, makersMark, from, craftSystem, typeof(Bone), tool, craftItem, resHue);
        Hue = craftSystem.GetContext(from)?.DoNotColor == true ? 0 : BoneHue;
        return result;
    }
}
