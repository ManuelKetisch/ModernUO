using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0)]
public abstract partial class ExcelsiorHides : BaseHides, IScissorable
{
    protected ExcelsiorHides(CraftResource resource, int amount) : base(resource, amount)
    {
    }

    public bool Scissor(Mobile from, Scissors scissors)
    {
        if (Deleted || !from.CanSee(this) || from.Backpack == null || !IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(502437);
            return false;
        }

        var leather = CraftResources.GetInfo(Resource)?.ResourceTypes[0].CreateEntityInstance<Item>();
        if (leather == null)
        {
            return false;
        }

        ScissorHelper(from, leather, 1);
        return true;
    }
}

[SerializationGenerator(0)]
public sealed partial class PolarLeather : BaseLeather
{
    [Constructible]
    public PolarLeather(int amount = 1) : base(CraftResource.PolarLeather, amount)
    {
    }

    public override string DefaultName => "polar leather";
}

[SerializationGenerator(0)]
public sealed partial class PolarHides : ExcelsiorHides
{
    [Constructible]
    public PolarHides(int amount = 1) : base(CraftResource.PolarLeather, amount)
    {
    }

    public override string DefaultName => "polar hides";
}

[SerializationGenerator(0)]
public sealed partial class SyntheticLeather : BaseLeather
{
    [Constructible]
    public SyntheticLeather(int amount = 1) : base(CraftResource.SyntheticLeather, amount)
    {
    }

    public override string DefaultName => "synthetic leather";
}

[SerializationGenerator(0)]
public sealed partial class SyntheticHides : ExcelsiorHides
{
    [Constructible]
    public SyntheticHides(int amount = 1) : base(CraftResource.SyntheticLeather, amount)
    {
    }

    public override string DefaultName => "synthetic hides";
}

[SerializationGenerator(0)]
public sealed partial class BlazeLeather : BaseLeather
{
    [Constructible]
    public BlazeLeather(int amount = 1) : base(CraftResource.BlazeLeather, amount)
    {
    }

    public override string DefaultName => "blaze leather";
}

[SerializationGenerator(0)]
public sealed partial class BlazeHides : ExcelsiorHides
{
    [Constructible]
    public BlazeHides(int amount = 1) : base(CraftResource.BlazeLeather, amount)
    {
    }

    public override string DefaultName => "blaze hides";
}

[SerializationGenerator(0)]
public sealed partial class DaemonicLeather : BaseLeather
{
    [Constructible]
    public DaemonicLeather(int amount = 1) : base(CraftResource.DaemonicLeather, amount)
    {
    }

    public override string DefaultName => "daemonic leather";
}

[SerializationGenerator(0)]
public sealed partial class DaemonicHides : ExcelsiorHides
{
    [Constructible]
    public DaemonicHides(int amount = 1) : base(CraftResource.DaemonicLeather, amount)
    {
    }

    public override string DefaultName => "daemonic hides";
}

[SerializationGenerator(0)]
public sealed partial class ShadowLeather : BaseLeather
{
    [Constructible]
    public ShadowLeather(int amount = 1) : base(CraftResource.ShadowLeather, amount)
    {
    }

    public override string DefaultName => "shadow leather";
}

[SerializationGenerator(0)]
public sealed partial class ShadowHides : ExcelsiorHides
{
    [Constructible]
    public ShadowHides(int amount = 1) : base(CraftResource.ShadowLeather, amount)
    {
    }

    public override string DefaultName => "shadow hides";
}

[SerializationGenerator(0)]
public sealed partial class FrostLeather : BaseLeather
{
    [Constructible]
    public FrostLeather(int amount = 1) : base(CraftResource.FrostLeather, amount)
    {
    }

    public override string DefaultName => "frost leather";
}

[SerializationGenerator(0)]
public sealed partial class FrostHides : ExcelsiorHides
{
    [Constructible]
    public FrostHides(int amount = 1) : base(CraftResource.FrostLeather, amount)
    {
    }

    public override string DefaultName => "frost hides";
}

[SerializationGenerator(0)]
public sealed partial class EtherealLeather : BaseLeather
{
    [Constructible]
    public EtherealLeather(int amount = 1) : base(CraftResource.EtherealLeather, amount)
    {
    }

    public override string DefaultName => "ethereal leather";
}

[SerializationGenerator(0)]
public sealed partial class EtherealHides : ExcelsiorHides
{
    [Constructible]
    public EtherealHides(int amount = 1) : base(CraftResource.EtherealLeather, amount)
    {
    }

    public override string DefaultName => "ethereal hides";
}
