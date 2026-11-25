using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.Craft;
using Server.Engines.WeaponLevelingServices;
using Server.Gumps;
using Server.Network;
using Server.Targeting;

namespace Server.Items;

public enum TradeSupplyKind
{
    Spellcasting,
    Woodworking,
    Metalworking,
    Tailoring,
    Stoneworking,
    Gems,
    Tools,
    RunicTools
}

[SerializationGenerator(0)]
public partial class TradeResourceStorage : Item, ICraftable
{
    internal const int MaxPerType = 60000;
    private const int PageSize = 8;

    private static readonly HashSet<Type>[] _catalog =
    [
        [
            typeof(BlackPearl), typeof(Bloodmoss), typeof(Garlic), typeof(Ginseng), typeof(MandrakeRoot),
            typeof(Nightshade), typeof(SpidersSilk), typeof(SulfurousAsh), typeof(BatWing), typeof(DaemonBlood),
            typeof(GraveDust), typeof(NoxCrystal), typeof(PigIron), typeof(DeadWood), typeof(DragonsBlood),
            typeof(FertileDirt), typeof(ZoogiFungus), typeof(Bone), typeof(DaemonBone), typeof(PotionKeg),
            typeof(BlankScroll), typeof(Bottle), typeof(Sand), typeof(KeyRing), typeof(Beeswax), typeof(EtherealPowder)
        ],
        [
            typeof(Log), typeof(OakLog), typeof(AshLog), typeof(YewLog), typeof(HeartwoodLog),
            typeof(BloodwoodLog), typeof(FrostwoodLog), typeof(Board), typeof(OakBoard), typeof(AshBoard),
            typeof(YewBoard), typeof(HeartwoodBoard), typeof(BloodwoodBoard), typeof(FrostwoodBoard),
            typeof(Arrow), typeof(DippedArrow), typeof(Bolt), typeof(Feather), typeof(Shaft)
        ],
        [
            typeof(IronIngot), typeof(DullCopperIngot), typeof(ShadowIronIngot), typeof(CopperIngot),
            typeof(BronzeIngot), typeof(GoldIngot), typeof(AgapiteIngot), typeof(VeriteIngot),
            typeof(ValoriteIngot), typeof(MeteoricBronzeIngot), typeof(ConductiveSilverIngot)
        ],
        [
            typeof(Leather), typeof(SpinedLeather), typeof(HornedLeather), typeof(BarbedLeather),
            typeof(Hides), typeof(SpinedHides), typeof(HornedHides), typeof(BarbedHides), typeof(Cloth), typeof(UncutCloth), typeof(BoltOfCloth), typeof(SpoolOfThread), typeof(DarkYarn),
            typeof(LightYarn), typeof(LightYarnUnraveled), typeof(Wool), typeof(Cotton), typeof(RedScales),
            typeof(YellowScales), typeof(BlackScales), typeof(GreenScales), typeof(WhiteScales), typeof(BlueScales)
        ],
        [
            typeof(Granite), typeof(DullCopperGranite), typeof(ShadowIronGranite), typeof(CopperGranite),
            typeof(BronzeGranite), typeof(GoldGranite), typeof(AgapiteGranite), typeof(VeriteGranite),
            typeof(ValoriteGranite)
        ],
        [
            typeof(Amber), typeof(Amethyst), typeof(Citrine), typeof(Diamond), typeof(Emerald),
            typeof(Ruby), typeof(Sapphire), typeof(StarSapphire), typeof(Tourmaline),
            typeof(BlueDiamond), typeof(BrilliantAmber), typeof(DarkSapphire), typeof(EcruCitrine),
            typeof(FireRuby), typeof(PerfectEmerald), typeof(Turquoise), typeof(WhitePearl)
        ],
        [
            typeof(TinkerTools), typeof(SmithHammer), typeof(SturdySmithHammer), typeof(SledgeHammer), typeof(Tongs),
            typeof(SewingKit), typeof(SturdySewingKit),
            typeof(FletcherTools), typeof(Saw), typeof(DovetailSaw), typeof(ScribesPen), typeof(MapmakersPen),
            typeof(MortarPestle), typeof(MalletAndChisel), typeof(FlourSifter), typeof(Skillet),
            typeof(Shovel), typeof(SturdyShovel), typeof(Pickaxe), typeof(SturdyPickaxe),
            typeof(MeteoricPickaxe), typeof(Hatchet), typeof(SturdyAxe)
        ],
        [
            typeof(RunicHammer), typeof(RunicSewingKit), typeof(RunicDovetailSaw), typeof(RunicCarpenterSaw),
            typeof(RunicFletcherTool), typeof(RunicTinkerTool)
        ]
    ];

    [SerializableField(0, setter: "private")]
    private TradeSupplyKind _kind;

    [SerializableField(1, getter: "private", setter: "private")]
    private List<Item> _supplies;

    [Constructible]
    public TradeResourceStorage(TradeSupplyKind kind = TradeSupplyKind.Metalworking) : base(GetGraphic(kind))
    {
        _supplies = [];
        ConfigureKind(kind);
    }

    private void ConfigureKind(TradeSupplyKind kind)
    {
        Kind = kind;
        ItemID = GetGraphic(kind);
        Hue = kind switch
        {
            TradeSupplyKind.Spellcasting => 0x21,
            TradeSupplyKind.Woodworking  => 0x5,
            TradeSupplyKind.Metalworking => 0x23,
            TradeSupplyKind.Tailoring    => 0x44,
            TradeSupplyKind.Stoneworking => 0x35,
            TradeSupplyKind.Tools        => 0x3B2,
            TradeSupplyKind.RunicTools   => 0x489,
            _                            => 0
        };
    }

    public override string DefaultName => GetName(Kind);

    private static int GetGraphic(TradeSupplyKind kind) => kind switch
    {
        TradeSupplyKind.Gems                                => 0xE79,
        TradeSupplyKind.Tools or TradeSupplyKind.RunicTools => 0xE80,
        _                                                   => 0x1011
    };

    private static string GetName(TradeSupplyKind kind) => kind switch
    {
        TradeSupplyKind.Spellcasting => "spell caster's keys",
        TradeSupplyKind.Woodworking  => "wood worker's keys",
        TradeSupplyKind.Metalworking => "metal worker's keys",
        TradeSupplyKind.Tailoring    => "tailor's keys",
        TradeSupplyKind.Stoneworking => "stone worker's keys",
        TradeSupplyKind.Gems         => "gem pouch",
        TradeSupplyKind.Tools        => "tool house",
        TradeSupplyKind.RunicTools   => "runic house",
        _                            => "resource keys"
    };

    private bool StoresTools => Kind is TradeSupplyKind.Tools or TradeSupplyKind.RunicTools;

    public override double DefaultWeight => StoresTools ? 3.0 : 1.0;

    internal static void AddRecipes(DefTinkering tinkering)
    {
        foreach (var kind in Enum.GetValues<TradeSupplyKind>())
        {
            var (minimum, cost) = kind switch
            {
                TradeSupplyKind.Gems         => (35, 8),
                TradeSupplyKind.Spellcasting => (35, 20),
                TradeSupplyKind.Tools        => (65, 40),
                TradeSupplyKind.RunicTools   => (80, 60),
                _                            => (50, 20)
            };
            var index = tinkering.AddCraft(
                typeof(TradeResourceStorage), "Trade Tools", GetName(kind), GetGraphic(kind),
                minimum, minimum + 40, typeof(IronIngot), 1044036, cost, 1044037
            );
            tinkering.ForceNonExceptional(index);
        }
    }

    public int OnCraft(
        int quality, bool makersMark, Mobile from, CraftSystem craftSystem, Type typeRes, BaseTool tool,
        CraftItem craftItem, int resHue
    )
    {
        foreach (var kind in Enum.GetValues<TradeSupplyKind>())
        {
            if (craftItem.NameString == GetName(kind))
            {
                ConfigureKind(kind);
                break;
            }
        }

        return quality;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (TradeToolAccess.InBackpack(from, this))
        {
            from.SendGump(new SupplyGump(from, this, 0));
        }
        else
        {
            from.SendMessage("Keep this in your backpack to use it.");
        }
    }

    internal int GetStoredAmount(Type type, CraftResource tier = CraftResource.None)
    {
        var count = 0;
        foreach (var stored in _supplies)
        {
            if (IsStored(stored) && stored.GetType() == type && Tier(stored) == tier)
            {
                count += Units(stored);
            }
        }

        return count;
    }

    private bool Accepts(Item item, Type type)
    {
        if ((uint)Kind >= _catalog.Length)
        {
            return false;
        }

        if (_catalog[(int)Kind].Contains(type))
        {
            return true;
        }

        var resourceType = CraftResources.GetType(CraftResources.GetFromType(type));
        return Kind switch
        {
            TradeSupplyKind.Metalworking => resourceType == CraftResourceType.Metal && item is BaseIngot,
            TradeSupplyKind.Woodworking  => resourceType == CraftResourceType.Wood && item is Log or Board or ExcelsiorWoodStock,
            TradeSupplyKind.Tailoring    => resourceType == CraftResourceType.Leather && item is BaseLeather or BaseHides,
            TradeSupplyKind.Stoneworking => resourceType == CraftResourceType.Metal && item is BaseGranite,
            _                            => false
        };
    }

    private int Units(Item item) => StoresTools && item is IUsesRemaining tool ? tool.UsesRemaining : item.Amount;

    private static CraftResource Tier(Item item) => item is BaseRunicTool runic ? runic.Resource : CraftResource.None;

    private static bool IsStored(Item item) => item is { Deleted: false, Parent: null } && item.Map == Map.Internal;

    internal bool TryStore(Mobile from, Item item)
    {
        if (!TradeToolAccess.InBackpack(from, this) || !TradeToolAccess.InBackpack(from, item))
        {
            from.SendMessage("Keep the storage and the item in your backpack.");
            return false;
        }

        var type = item.GetType();
        var units = Units(item);
        if (!Accepts(item, type) || !TradeToolAccess.CanStore(item) || units <= 0 || StoresTools && item.Amount != 1)
        {
            from.SendMessage("This item cannot be stored here.");
            return false;
        }

        if (units > MaxPerType - GetStoredAmount(type, Tier(item)))
        {
            if (StoresTools)
            {
                from.SendMessage($"This house holds up to {MaxPerType:N0} uses per tool and tier.");
            }
            else
            {
                from.SendMessage($"Storage holds up to {MaxPerType:N0} of each resource.");
            }

            return false;
        }

        foreach (var stored in _supplies)
        {
            if (!IsStored(stored))
            {
                continue;
            }

            if (StoresTools ? TryCombineTools(stored, item) : stored.StackWith(from, item, false))
            {
                return true;
            }
        }

        item.Internalize();
        AddToSupplies(item);
        return true;
    }

    internal bool TryWithdraw(Mobile from, Item item, int amount)
    {
        if (!TradeToolAccess.InBackpack(from, this) || amount <= 0 || !IsStored(item) || !_supplies.Contains(item))
        {
            return false;
        }

        var units = Units(item);
        if ((!StoresTools && !item.Stackable) || amount >= units)
        {
            if (!from.Backpack.TryDropItem(from, item, true))
            {
                return false;
            }

            _supplies.Remove(item);
            this.MarkDirty();
            return true;
        }

        if (StoresTools && item is IUsesRemaining storedTool)
        {
            return TryTransferUses(from, item, storedTool, amount);
        }

        // A partial resource withdrawal preserves the original hue and variant.
        var output = item.GetType().CreateEntityInstance<Item>();
        if (output == null)
        {
            return false;
        }

        item.Dupe(output);
        output.Amount = amount;
        if (!from.Backpack.TryDropItem(from, output, true))
        {
            output.Delete();
            return false;
        }

        item.Consume(amount);
        return true;
    }

    private static bool TryTransferUses(Mobile from, Item item, IUsesRemaining storedTool, int amount)
    {
        // Split charges into an existing tool so withdrawing cannot mint saleable items.
        foreach (var candidate in from.Backpack.FindItemsByType(item.GetType()))
        {
            if (candidate.Amount == 1 && candidate is IUsesRemaining { UsesRemaining: >= 0 } backpackTool &&
                amount <= MaxPerType - backpackTool.UsesRemaining && TradeToolAccess.InBackpack(from, candidate) &&
                TradeToolAccess.CanStore(candidate) && CanConsolidate(item, candidate))
            {
                backpackTool.UsesRemaining += amount;
                storedTool.UsesRemaining -= amount;
                return true;
            }
        }

        from.SendMessage("Bring a matching tool to transfer uses, or take the whole tool.");
        return false;
    }

    private static bool TryCombineTools(Item stored, Item item)
    {
        if (stored is not IUsesRemaining destination || item is not IUsesRemaining source || !CanConsolidate(stored, item))
        {
            return false;
        }

        destination.UsesRemaining += source.UsesRemaining;
        item.Delete();
        return true;
    }

    private static bool CanConsolidate(Item stored, Item item)
    {
        if (stored.GetType() != item.GetType() || Tier(stored) != Tier(item) ||
            stored.Name != item.Name || stored.Hue != item.Hue || stored.ItemID != item.ItemID ||
            stored.Weight != item.Weight || stored.PlayerConstructed != item.PlayerConstructed ||
            ((IUsesRemaining)stored).ShowUsesRemaining != ((IUsesRemaining)item).ShowUsesRemaining)
        {
            return false;
        }

        if (stored is BaseWeapon targetWeapon && item is BaseWeapon sourceWeapon)
        {
            return CanDivideMiningTool(targetWeapon) && CanDivideMiningTool(sourceWeapon) &&
                   SameMiningTool(targetWeapon, sourceWeapon);
        }

        return ToolFinish(stored) == ToolFinish(item);
    }

    private static bool CanDivideMiningTool(BaseWeapon weapon) =>
        (weapon.GetType() == typeof(Pickaxe) || weapon.GetType() == typeof(SturdyPickaxe)) &&
        weapon is
        {
            ArtifactRarity: 0, IsWeaponLevelingEnabled: false, Cursed: false, Consecrated: false,
            Resource: CraftResource.Iron, FactionItemState: null, Poison: null, PoisonCharges: 0,
            Slayer: SlayerName.None, Slayer2: SlayerName.None, Attributes.IsEmpty: true,
            WeaponAttributes.IsEmpty: true, SkillBonuses.IsEmpty: true, AosElementDamages.IsEmpty: true,
            DamageLevel: WeaponDamageLevel.Regular, AccuracyLevel: WeaponAccuracyLevel.Regular,
            DurabilityLevel: WeaponDurabilityLevel.Regular
        } && weapon.Name == weapon.DefaultName && string.IsNullOrEmpty(weapon.EngravedText) &&
        !weapon.CaptureWeaponLeveling().ArtifactTagRemoved;

    private static bool SameMiningTool(BaseWeapon first, BaseWeapon second) =>
        new WeaponServiceTransfer.PreviewStamp(first, null).Matches(second, null) &&
        first.MinDamage == second.MinDamage && first.MaxDamage == second.MaxDamage && first.Speed == second.Speed &&
        first.StrRequirement == second.StrRequirement && first.DexRequirement == second.DexRequirement &&
        first.IntRequirement == second.IntRequirement && first.Skill == second.Skill && first.Type == second.Type &&
        first.Animation == second.Animation && first.MaxRange == second.MaxRange &&
        first.HitSound == second.HitSound && first.MissSound == second.MissSound &&
        first.Weight == second.Weight;

    private static (ToolQuality Quality, string Crafter) ToolFinish(Item item) => item switch
    {
        BaseTool tool        => (tool.Quality, tool.Crafter),
        BaseHarvestTool tool => (tool.Quality, tool.Crafter),
        _                    => default
    };

    public override void OnAfterDelete()
    {
        foreach (var item in _supplies)
        {
            if (IsStored(item))
            {
                item.Delete();
            }
        }

        base.OnAfterDelete();
    }

    private sealed class SupplyGump : DynamicGump
    {
        private const int Deposit = 1;
        private const int Previous = 2;
        private const int Next = 3;
        private const int WithdrawBase = 10;
        private const int TextHue = 0x481;
        private readonly Mobile _owner;
        private readonly TradeResourceStorage _storage;
        private readonly List<Item> _rows;
        private readonly int _page;
        private readonly bool _hasNext;

        public SupplyGump(Mobile owner, TradeResourceStorage storage, int page) : base(35, 35)
        {
            _owner = owner;
            _storage = storage;
            var lastPage = Math.Max(0, (storage._supplies.Count - 1) / PageSize);
            _page = Math.Clamp(page, 0, lastPage);
            var offset = _page * PageSize;
            _rows = storage._supplies.GetRange(offset, Math.Min(PageSize, storage._supplies.Count - offset));
            _hasNext = _page < lastPage;
        }

        public override bool Singleton => true;

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();
            builder.AddBackground(0, 0, 390, 360, 0x13BE);
            builder.AddLabel(22, 18, TextHue, _storage.DefaultName);
            builder.AddButton(22, 51, 0xFA5, 0xFA7, Deposit);
            builder.AddLabel(57, 49, TextHue, "Store item");
            builder.AddLabel(180, 49, TextHue, "Take up to");
            builder.AddBackground(260, 46, 90, 26, 0xBBC);
            builder.AddTextEntry(270, 49, 70, 23, TextHue, 0, "100");
            if (_rows.Count == 0)
            {
                builder.AddLabel(22, 89, TextHue, "Empty");
            }

            for (var i = 0; i < _rows.Count; i++)
            {
                var item = _rows[i];
                if (!IsStored(item))
                {
                    continue;
                }

                var y = 86 + i * 28;
                builder.AddButton(22, y, 0xFA5, 0xFA7, WithdrawBase + i);
                AddRowLabel(ref builder, item, y);
                var units = _storage.Units(item);
                if (_storage.StoresTools && item is IUsesRemaining)
                {
                    if (item is BaseWeapon tool && !CanDivideMiningTool(tool))
                    {
                        builder.AddLabel(254, y, TextHue, $"{units:N0} uses (whole)");
                    }
                    else
                    {
                        builder.AddLabel(270, y, TextHue, $"{units:N0} uses");
                    }
                }
                else
                {
                    builder.AddLabel(284, y, TextHue, $"{units:N0}");
                }
            }

            if (_page > 0)
            {
                builder.AddButton(22, 324, 0xFAE, 0xFB0, Previous);
                builder.AddLabel(57, 323, TextHue, "Previous");
            }

            if (_hasNext)
            {
                builder.AddButton(269, 324, 0xFA5, 0xFA7, Next);
                builder.AddLabel(304, 323, TextHue, "Next");
            }
        }

        private static void AddRowLabel(ref DynamicGumpBuilder builder, Item item, int y)
        {
            if (item is BaseRunicTool runic)
            {
                var toolName = runic switch
                {
                    RunicHammer       => "runic hammer",
                    RunicSewingKit    => "runic sewing kit",
                    RunicDovetailSaw or RunicCarpenterSaw => "runic saw",
                    RunicTinkerTool   => "runic tinker tool",
                    RunicFletcherTool => "runic fletcher's tool",
                    _                 => "runic tool"
                };
                var tier = CraftResources.GetName(runic.Resource);
                builder.AddLabelCropped(57, y, 210, 20, TextHue, $"{tier} {toolName}");
            }
            else if (item.Name is { } name)
            {
                builder.AddLabelCropped(57, y, 210, 20, TextHue, name);
            }
            else
            {
                builder.AddHtmlLocalized(57, y, 210, 20, item.LabelNumber, 0xFFFFFF);
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile != _owner || sender != _owner.NetState || !TradeToolAccess.InBackpack(_owner, _storage))
            {
                return;
            }

            switch (info.ButtonID)
            {
                case Deposit:
                    _owner.SendMessage("Target an item in your backpack to store it.");
                    _owner.BeginTarget(2, false, TargetFlags.None, (from, target) =>
                    {
                        if (target is Item item)
                        {
                            _storage.TryStore(from, item);
                        }

                        if (TradeToolAccess.InBackpack(from, _storage))
                        {
                            from.SendGump(new SupplyGump(from, _storage, _page));
                        }
                    });
                    return;
                case Previous when _page > 0:
                    _owner.SendGump(new SupplyGump(_owner, _storage, _page - 1));
                    return;
                case Next when _hasNext:
                    _owner.SendGump(new SupplyGump(_owner, _storage, _page + 1));
                    return;
            }

            var row = info.ButtonID - WithdrawBase;
            if ((uint)row < _rows.Count)
            {
                if (int.TryParse(info.GetTextEntry(0), out var amount) && amount is > 0 and <= MaxPerType)
                {
                    _storage.TryWithdraw(_owner, _rows[row], amount);
                }
                else
                {
                    _owner.SendMessage($"Enter an amount from 1 to {MaxPerType:N0}.");
                }

                _owner.SendGump(new SupplyGump(_owner, _storage, _page));
            }
        }
    }
}

internal static class TradeToolAccess
{
    internal static bool InBackpack(Mobile from, Item item) =>
        from is { Alive: true, Deleted: false, Backpack: { } pack } &&
        item is { Deleted: false, Movable: true, IsLockedDown: false, IsSecure: false } &&
        item.IsChildOf(pack) && item.GetSecureTradeCont() == null && item.IsAccessibleTo(from);

    internal static bool CanStore(Item item) =>
        item is
        {
            LootType: LootType.Regular, QuestItem: false, Nontransferable: false,
            Insured: false, BlessedFor: null, TotalItems: 0
        };
}
