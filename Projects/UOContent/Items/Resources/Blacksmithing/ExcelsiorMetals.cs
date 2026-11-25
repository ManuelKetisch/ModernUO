using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0)]
public sealed partial class BlazeOre : BaseOre
{
    [Constructible]
    public BlazeOre(int amount = 1) : base(CraftResource.Blaze, amount)
    {
    }

    public override string DefaultName => "blaze ore";

    public override BaseIngot GetIngot() => new BlazeIngot();
}

[SerializationGenerator(0)]
public sealed partial class BlazeIngot : BaseIngot
{
    [Constructible]
    public BlazeIngot(int amount = 1) : base(CraftResource.Blaze, amount)
    {
    }

    public override string DefaultName => "blaze ingot";
}

[SerializationGenerator(0)]
public sealed partial class BlazeGranite : BaseGranite
{
    [Constructible]
    public BlazeGranite() : base(CraftResource.Blaze)
    {
    }

    public override string DefaultName => "blaze granite";
}

[SerializationGenerator(0)]
public sealed partial class IceOre : BaseOre
{
    [Constructible]
    public IceOre(int amount = 1) : base(CraftResource.Ice, amount)
    {
    }

    public override string DefaultName => "ice ore";

    public override BaseIngot GetIngot() => new IceIngot();
}

[SerializationGenerator(0)]
public sealed partial class IceIngot : BaseIngot
{
    [Constructible]
    public IceIngot(int amount = 1) : base(CraftResource.Ice, amount)
    {
    }

    public override string DefaultName => "ice ingot";
}

[SerializationGenerator(0)]
public sealed partial class IceGranite : BaseGranite
{
    [Constructible]
    public IceGranite() : base(CraftResource.Ice)
    {
    }

    public override string DefaultName => "ice granite";
}

[SerializationGenerator(0)]
public sealed partial class ToxicOre : BaseOre
{
    [Constructible]
    public ToxicOre(int amount = 1) : base(CraftResource.Toxic, amount)
    {
    }

    public override string DefaultName => "toxic ore";

    public override BaseIngot GetIngot() => new ToxicIngot();
}

[SerializationGenerator(0)]
public sealed partial class ToxicIngot : BaseIngot
{
    [Constructible]
    public ToxicIngot(int amount = 1) : base(CraftResource.Toxic, amount)
    {
    }

    public override string DefaultName => "toxic ingot";
}

[SerializationGenerator(0)]
public sealed partial class ToxicGranite : BaseGranite
{
    [Constructible]
    public ToxicGranite() : base(CraftResource.Toxic)
    {
    }

    public override string DefaultName => "toxic granite";
}

[SerializationGenerator(0)]
public sealed partial class ElectrumOre : BaseOre
{
    [Constructible]
    public ElectrumOre(int amount = 1) : base(CraftResource.Electrum, amount)
    {
    }

    public override string DefaultName => "electrum ore";

    public override BaseIngot GetIngot() => new ElectrumIngot();
}

[SerializationGenerator(0)]
public sealed partial class ElectrumIngot : BaseIngot
{
    [Constructible]
    public ElectrumIngot(int amount = 1) : base(CraftResource.Electrum, amount)
    {
    }

    public override string DefaultName => "electrum ingot";
}

[SerializationGenerator(0)]
public sealed partial class ElectrumGranite : BaseGranite
{
    [Constructible]
    public ElectrumGranite() : base(CraftResource.Electrum)
    {
    }

    public override string DefaultName => "electrum granite";
}

[SerializationGenerator(0)]
public sealed partial class PlatinumOre : BaseOre
{
    [Constructible]
    public PlatinumOre(int amount = 1) : base(CraftResource.Platinum, amount)
    {
    }

    public override string DefaultName => "platinum ore";

    public override BaseIngot GetIngot() => new PlatinumIngot();
}

[SerializationGenerator(0)]
public sealed partial class PlatinumIngot : BaseIngot
{
    [Constructible]
    public PlatinumIngot(int amount = 1) : base(CraftResource.Platinum, amount)
    {
    }

    public override string DefaultName => "platinum ingot";
}

[SerializationGenerator(0)]
public sealed partial class PlatinumGranite : BaseGranite
{
    [Constructible]
    public PlatinumGranite() : base(CraftResource.Platinum)
    {
    }

    public override string DefaultName => "platinum granite";
}
