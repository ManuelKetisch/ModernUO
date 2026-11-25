using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0)]
public sealed partial class ExcelsiorIronIngot : BaseIngot
{
    [Constructible]
    public ExcelsiorIronIngot(int amount = 1) : base(CraftResource.ExcelsiorIron, amount)
    {
    }

    public override string DefaultName => "Excelsior iron ingot";
}

[SerializationGenerator(0)]
public sealed partial class ExcelsiorDullCopperIngot : BaseIngot
{
    [Constructible]
    public ExcelsiorDullCopperIngot(int amount = 1) : base(CraftResource.ExcelsiorDullCopper, amount)
    {
    }

    public override string DefaultName => "Excelsior dull copper ingot";
}

[SerializationGenerator(0)]
public sealed partial class ExcelsiorShadowIronIngot : BaseIngot
{
    [Constructible]
    public ExcelsiorShadowIronIngot(int amount = 1) : base(CraftResource.ExcelsiorShadowIron, amount)
    {
    }

    public override string DefaultName => "Excelsior shadow iron ingot";
}

[SerializationGenerator(0)]
public sealed partial class ExcelsiorCopperIngot : BaseIngot
{
    [Constructible]
    public ExcelsiorCopperIngot(int amount = 1) : base(CraftResource.ExcelsiorCopper, amount)
    {
    }

    public override string DefaultName => "Excelsior copper ingot";
}

[SerializationGenerator(0)]
public sealed partial class ExcelsiorBronzeIngot : BaseIngot
{
    [Constructible]
    public ExcelsiorBronzeIngot(int amount = 1) : base(CraftResource.ExcelsiorBronze, amount)
    {
    }

    public override string DefaultName => "Excelsior bronze ingot";
}

[SerializationGenerator(0)]
public sealed partial class ExcelsiorGoldIngot : BaseIngot
{
    [Constructible]
    public ExcelsiorGoldIngot(int amount = 1) : base(CraftResource.ExcelsiorGold, amount)
    {
    }

    public override string DefaultName => "Excelsior gold ingot";
}

[SerializationGenerator(0)]
public sealed partial class ExcelsiorAgapiteIngot : BaseIngot
{
    [Constructible]
    public ExcelsiorAgapiteIngot(int amount = 1) : base(CraftResource.ExcelsiorAgapite, amount)
    {
    }

    public override string DefaultName => "Excelsior agapite ingot";
}

[SerializationGenerator(0)]
public sealed partial class ExcelsiorVeriteIngot : BaseIngot
{
    [Constructible]
    public ExcelsiorVeriteIngot(int amount = 1) : base(CraftResource.ExcelsiorVerite, amount)
    {
    }

    public override string DefaultName => "Excelsior verite ingot";
}

[SerializationGenerator(0)]
public sealed partial class ExcelsiorValoriteIngot : BaseIngot
{
    [Constructible]
    public ExcelsiorValoriteIngot(int amount = 1) : base(CraftResource.ExcelsiorValorite, amount)
    {
    }

    public override string DefaultName => "Excelsior valorite ingot";
}

[SerializationGenerator(0)]
public sealed partial class ExcelsiorRegularLeather : BaseLeather
{
    [Constructible]
    public ExcelsiorRegularLeather(int amount = 1) : base(CraftResource.ExcelsiorRegularLeather, amount)
    {
    }

    public override string DefaultName => "Excelsior regular leather";
}

[SerializationGenerator(0)]
public sealed partial class ExcelsiorSpinedLeather : BaseLeather
{
    [Constructible]
    public ExcelsiorSpinedLeather(int amount = 1) : base(CraftResource.ExcelsiorSpinedLeather, amount)
    {
    }

    public override string DefaultName => "Excelsior spined leather";
}

[SerializationGenerator(0)]
public sealed partial class ExcelsiorHornedLeather : BaseLeather
{
    [Constructible]
    public ExcelsiorHornedLeather(int amount = 1) : base(CraftResource.ExcelsiorHornedLeather, amount)
    {
    }

    public override string DefaultName => "Excelsior horned leather";
}

[SerializationGenerator(0)]
public sealed partial class ExcelsiorBarbedLeather : BaseLeather
{
    [Constructible]
    public ExcelsiorBarbedLeather(int amount = 1) : base(CraftResource.ExcelsiorBarbedLeather, amount)
    {
    }

    public override string DefaultName => "Excelsior barbed leather";
}

[SerializationGenerator(0)]
public sealed partial class ExcelsiorAshBoard : ExcelsiorBoard
{
    [Constructible]
    public ExcelsiorAshBoard(int amount = 1) : base(CraftResource.ExcelsiorAshWood, amount)
    {
    }

    public override string DefaultName => "Excelsior ash board";
}

[SerializationGenerator(0)]
public sealed partial class ExcelsiorYewBoard : ExcelsiorBoard
{
    [Constructible]
    public ExcelsiorYewBoard(int amount = 1) : base(CraftResource.ExcelsiorYewWood, amount)
    {
    }

    public override string DefaultName => "Excelsior yew board";
}

[SerializationGenerator(0)]
public sealed partial class ExcelsiorOakBoard : ExcelsiorBoard
{
    [Constructible]
    public ExcelsiorOakBoard(int amount = 1) : base(CraftResource.ExcelsiorOakWood, amount)
    {
    }

    public override string DefaultName => "Excelsior oak board";
}
