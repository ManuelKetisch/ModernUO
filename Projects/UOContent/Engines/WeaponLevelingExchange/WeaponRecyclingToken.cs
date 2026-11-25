using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0)]
public sealed partial class WeaponRecyclingToken : Item
{
    [Constructible]
    public WeaponRecyclingToken(int amount = 1) : base(0x0EED)
    {
        Hue = 0x482;
        Stackable = true;
        Amount = Math.Clamp(amount, 1, 60000);
    }

    public override string DefaultName => "weapon recycling token";
    public override double DefaultWeight => 0;
}
