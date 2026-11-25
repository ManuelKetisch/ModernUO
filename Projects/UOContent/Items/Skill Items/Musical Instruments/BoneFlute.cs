using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0)]
public sealed partial class BoneFlute : BaseInstrument
{
    [Constructible]
    public BoneFlute() : base(0x2805, 0x504, 0x503) => Hue = 0x47E;

    public override string DefaultName => "bone flute";
    public override double DefaultWeight => 2.0;
}
