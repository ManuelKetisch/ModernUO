using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0)]
public abstract partial class ExcelsiorWoodStock : Item, ICommodity
{
    protected ExcelsiorWoodStock(CraftResource resource, int amount, int art) : base(art)
    {
        _resource = resource;
        Stackable = true;
        Amount = amount;
        Hue = CraftResources.GetHue(resource);
    }

    [SerializableField(0, setter: "private")]
    private CraftResource _resource;

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;
}

[SerializationGenerator(0)]
public abstract partial class ExcelsiorLog : ExcelsiorWoodStock, IAxe
{
    protected ExcelsiorLog(CraftResource resource, int amount) : base(resource, amount, 0x1BDD)
    {
    }

    public override double DefaultWeight => ItemWeights.RawWood;
    public abstract bool Axe(Mobile from, BaseAxe axe);

    protected bool TryCreateBoards(Mobile from, double skill, Item boards)
    {
        if (Deleted || from.Backpack == null || !IsChildOf(from.Backpack) || !IsAccessibleTo(from) ||
            (from.Skills.Carpentry.Value < skill && from.Skills.Lumberjacking.Value < skill))
        {
            boards.Delete();
            from.SendMessage("Keep the logs in your backpack and meet the grade's crafting skill.");
            return false;
        }

        ScissorHelper(from, boards, 1, false);
        return true;
    }
}

[SerializationGenerator(0)]
public abstract partial class ExcelsiorBoard : ExcelsiorWoodStock
{
    protected ExcelsiorBoard(CraftResource resource, int amount) : base(resource, amount, 0x1BD7)
    {
    }

    public override double DefaultWeight => ItemWeights.ProcessedMaterial;
}

[SerializationGenerator(0)]
public sealed partial class PineLog : ExcelsiorLog
{
    [Constructible]
    public PineLog(int amount = 1) : base(CraftResource.PineWood, amount)
    {
    }

    public override string DefaultName => "pine log";

    public override bool Axe(Mobile from, BaseAxe axe) => TryCreateBoards(from, 20, new PineBoard());
}

[SerializationGenerator(0)]
public sealed partial class PineBoard : ExcelsiorBoard
{
    [Constructible]
    public PineBoard(int amount = 1) : base(CraftResource.PineWood, amount)
    {
    }

    public override string DefaultName => "pine board";
}

[SerializationGenerator(0)]
public sealed partial class MahoganyLog : ExcelsiorLog
{
    [Constructible]
    public MahoganyLog(int amount = 1) : base(CraftResource.MahoganyWood, amount)
    {
    }

    public override string DefaultName => "mahogany log";

    public override bool Axe(Mobile from, BaseAxe axe) => TryCreateBoards(from, 40, new MahoganyBoard());
}

[SerializationGenerator(0)]
public sealed partial class MahoganyBoard : ExcelsiorBoard
{
    [Constructible]
    public MahoganyBoard(int amount = 1) : base(CraftResource.MahoganyWood, amount)
    {
    }

    public override string DefaultName => "mahogany board";
}

[SerializationGenerator(0)]
public sealed partial class ZircoteLog : ExcelsiorLog
{
    [Constructible]
    public ZircoteLog(int amount = 1) : base(CraftResource.ZircoteWood, amount)
    {
    }

    public override string DefaultName => "zircote log";

    public override bool Axe(Mobile from, BaseAxe axe) => TryCreateBoards(from, 70, new ZircoteBoard());
}

[SerializationGenerator(0)]
public sealed partial class ZircoteBoard : ExcelsiorBoard
{
    [Constructible]
    public ZircoteBoard(int amount = 1) : base(CraftResource.ZircoteWood, amount)
    {
    }

    public override string DefaultName => "zircote board";
}

[SerializationGenerator(0)]
public sealed partial class EbonyLog : ExcelsiorLog
{
    [Constructible]
    public EbonyLog(int amount = 1) : base(CraftResource.EbonyWood, amount)
    {
    }

    public override string DefaultName => "ebony log";

    public override bool Axe(Mobile from, BaseAxe axe) => TryCreateBoards(from, 80, new EbonyBoard());
}

[SerializationGenerator(0)]
public sealed partial class EbonyBoard : ExcelsiorBoard
{
    [Constructible]
    public EbonyBoard(int amount = 1) : base(CraftResource.EbonyWood, amount)
    {
    }

    public override string DefaultName => "ebony board";
}

[SerializationGenerator(0)]
public sealed partial class BambooLog : ExcelsiorLog
{
    [Constructible]
    public BambooLog(int amount = 1) : base(CraftResource.BambooWood, amount)
    {
    }

    public override string DefaultName => "bamboo log";

    public override bool Axe(Mobile from, BaseAxe axe) => TryCreateBoards(from, 90, new BambooBoard());
}

[SerializationGenerator(0)]
public sealed partial class BambooBoard : ExcelsiorBoard
{
    [Constructible]
    public BambooBoard(int amount = 1) : base(CraftResource.BambooWood, amount)
    {
    }

    public override string DefaultName => "bamboo board";
}

[SerializationGenerator(0)]
public sealed partial class PurpleHeartLog : ExcelsiorLog
{
    [Constructible]
    public PurpleHeartLog(int amount = 1) : base(CraftResource.PurpleHeartWood, amount)
    {
    }

    public override string DefaultName => "purple heart log";

    public override bool Axe(Mobile from, BaseAxe axe) => TryCreateBoards(from, 100, new PurpleHeartBoard());
}

[SerializationGenerator(0)]
public sealed partial class PurpleHeartBoard : ExcelsiorBoard
{
    [Constructible]
    public PurpleHeartBoard(int amount = 1) : base(CraftResource.PurpleHeartWood, amount)
    {
    }

    public override string DefaultName => "purple heart board";
}

[SerializationGenerator(0)]
public sealed partial class RedwoodLog : ExcelsiorLog
{
    [Constructible]
    public RedwoodLog(int amount = 1) : base(CraftResource.Redwood, amount)
    {
    }

    public override string DefaultName => "redwood log";

    public override bool Axe(Mobile from, BaseAxe axe) => TryCreateBoards(from, 110, new RedwoodBoard());
}

[SerializationGenerator(0)]
public sealed partial class RedwoodBoard : ExcelsiorBoard
{
    [Constructible]
    public RedwoodBoard(int amount = 1) : base(CraftResource.Redwood, amount)
    {
    }

    public override string DefaultName => "redwood board";
}

[SerializationGenerator(0)]
public sealed partial class PetrifiedLog : ExcelsiorLog
{
    [Constructible]
    public PetrifiedLog(int amount = 1) : base(CraftResource.PetrifiedWood, amount)
    {
    }

    public override string DefaultName => "petrified log";

    public override bool Axe(Mobile from, BaseAxe axe) => TryCreateBoards(from, 119, new PetrifiedBoard());
}

[SerializationGenerator(0)]
public sealed partial class PetrifiedBoard : ExcelsiorBoard
{
    [Constructible]
    public PetrifiedBoard(int amount = 1) : base(CraftResource.PetrifiedWood, amount)
    {
    }

    public override string DefaultName => "petrified board";
}
