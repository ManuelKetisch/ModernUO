using ModernUO.Serialization;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Items;

internal enum WeaponExchangePurchase
{
    Level = 1,
    Change,
    Relayer,
    Boost,
    GemPouch = 6,
    EtherealPowder
}

[SerializationGenerator(0)]
public sealed partial class WeaponLevelExchange : Item
{
    internal const int RecyclingAward = 2000;
    internal const int LevelDeedPrice = 100000;
    internal const int ChangeDeedPrice = 50000;
    internal const int RelayerDeedPrice = 150000;
    internal const int BoostDeedPrice = 250000;
    internal const int GemPouchPrice = 10000;
    internal const int EtherealPowderPrice = 2000;
    private const int Reach = 3;

    // Open pavement just east of the Trinsic bank on both Britannia facets.
    private static readonly Point3D _trinsicLocation = new(1820, 2828, 0);

    [Constructible]
    public WeaponLevelExchange() : base(0x0ED4)
    {
        Hue = 0x482;
        Movable = false;
    }

    public override string DefaultName => "weapon leveling exchange";

    public static void Initialize()
    {
        if (ServerConfiguration.GetSetting("weaponLeveling.placeTrinsicExchange", true))
        {
            EnsurePlacement(Map.Felucca);
            EnsurePlacement(Map.Trammel);
        }
    }

    private static void EnsurePlacement(Map map)
    {
        if (map == null)
        {
            return;
        }

        foreach (var stone in map.GetItemsInRange<WeaponLevelExchange>(_trinsicLocation, 6))
        {
            if (!stone.Deleted)
            {
                return;
            }
        }

        if (map.CanFit(_trinsicLocation, 16, false, false))
        {
            new WeaponLevelExchange().MoveToWorld(_trinsicLocation, map);
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile player && CheckReach(player))
        {
            ShowExchange(player);
        }
    }

    private void ShowExchange(PlayerMobile player)
    {
        if (player.NetState != null)
        {
            player.SendGump(new ExchangeGump(this, player));
        }
    }

    private bool CheckReach(PlayerMobile player)
    {
        if (!player.CheckAlive())
        {
            return false;
        }

        if (!Deleted && Parent == null && Map != null && Map != Map.Internal && player.Map == Map &&
            player.InRange(Location, Reach) && player.CanSee(this) && player.InLOS(this) && IsAccessibleTo(player))
        {
            return true;
        }

        player.SendMessage("Move closer to the weapon leveling exchange.");
        return false;
    }

    private bool CanRecycle(PlayerMobile player, Item item) =>
        player.Backpack != null && item is
        {
            Deleted: false, Movable: true, PlayerConstructed: false, QuestItem: false, Nontransferable: false,
            Insured: false, BlessedFor: null, Amount: 1, LootType: LootType.Regular
        } && item.IsChildOf(player.Backpack) && item.IsAccessibleTo(player) &&
        item switch
        {
            BaseWeapon { ArtifactRarity: 0, IsWeaponLevelingEnabled: false } weapon =>
                !weapon.Attributes.IsEmpty || !weapon.WeaponAttributes.IsEmpty,
            BaseArmor { ArtifactRarity: 0 } armor => !armor.Attributes.IsEmpty || !armor.ArmorAttributes.IsEmpty,
            BaseClothing { ArtifactRarity: 0 } clothing => !clothing.Attributes.IsEmpty || !clothing.Resistances.IsEmpty,
            BaseJewel { ArtifactRarity: 0 } jewel => !jewel.Attributes.IsEmpty || !jewel.Resistances.IsEmpty,
            _ => false
        };

    private void StartRecycling(PlayerMobile player)
    {
        player.SendMessage("Choose a piece of enchanted gear in your backpack.");
        player.BeginTarget(Reach, false, TargetFlags.None, (from, target) =>
        {
            if (from != player || !CheckReach(player))
            {
                return;
            }

            if (target is not Item item || !CanRecycle(player, item))
            {
                player.SendMessage("Choose enchanted gear without crafting, insurance, or special bindings.");
                return;
            }

            player.SendGump(new RecycleGump(this, player, item));
        });
    }

    internal bool TryRecycle(PlayerMobile player, Item item)
    {
        if (!CheckReach(player) || !CanRecycle(player, item))
        {
            return false;
        }

        var award = new WeaponRecyclingToken(RecyclingAward);
        if (!player.Backpack.TryDropItem(player, award, true))
        {
            award.Delete();
            return false;
        }

        item.Delete();
        player.SendMessage($"You receive {RecyclingAward:N0} recycling tokens.");
        return true;
    }

    internal bool TryPurchase(PlayerMobile player, WeaponExchangePurchase purchase)
    {
        if (!CheckReach(player) || player.Backpack is not { } pack)
        {
            return false;
        }

        var price = Price(purchase);
        if (price == 0 || pack.GetAmount(typeof(WeaponRecyclingToken)) < price)
        {
            player.SendMessage("You need more weapon recycling tokens.");
            return false;
        }

        Item deed = purchase switch
        {
            WeaponExchangePurchase.Level => new WeaponLevelDeed(),
            WeaponExchangePurchase.Change => new WeaponChangeDeed(),
            WeaponExchangePurchase.Relayer => new WeaponRelayerDeed(),
            WeaponExchangePurchase.Boost => new WeaponLevelBoostDeed(),
            WeaponExchangePurchase.GemPouch => new TradeResourceStorage(TradeSupplyKind.Gems),
            WeaponExchangePurchase.EtherealPowder => new EtherealPowder(),
            _ => null
        };

        if (deed == null)
        {
            return false;
        }

        if (!pack.CheckHold(player, deed, true) || !pack.ConsumeTotal(typeof(WeaponRecyclingToken), price))
        {
            deed.Delete();
            return false;
        }

        pack.DropItem(deed);
        player.SendMessage($"You buy a {deed.DefaultName}.");
        return true;
    }

    private static int Price(WeaponExchangePurchase purchase) => purchase switch
    {
        WeaponExchangePurchase.Level => LevelDeedPrice,
        WeaponExchangePurchase.Change => ChangeDeedPrice,
        WeaponExchangePurchase.Relayer => RelayerDeedPrice,
        WeaponExchangePurchase.Boost => BoostDeedPrice,
        WeaponExchangePurchase.GemPouch => GemPouchPrice,
        WeaponExchangePurchase.EtherealPowder => EtherealPowderPrice,
        _ => 0
    };

    private sealed class ExchangeGump(WeaponLevelExchange exchange, PlayerMobile owner) : DynamicGump(55, 55)
    {
        private const int RecyclingButton = 5;
        private const int LabelHue = 0x481;

        public override bool Singleton => true;

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();
            builder.AddBackground(0, 0, 430, 310, 0x13BE);
            builder.AddLabel(22, 15, LabelHue, "Weapon leveling exchange");
            var tokens = owner.Backpack?.GetAmount(typeof(WeaponRecyclingToken)) ?? 0;
            builder.AddLabel(22, 42, LabelHue, $"Recycling tokens: {tokens:N0}");
            AddPurchase(ref builder, 73, WeaponExchangePurchase.Level, "Weapon level deed");
            AddPurchase(ref builder, 104, WeaponExchangePurchase.Change, "Weapon change deed");
            AddPurchase(ref builder, 135, WeaponExchangePurchase.Relayer, "Weapon relayer deed");
            AddPurchase(ref builder, 166, WeaponExchangePurchase.Boost, "Ten-level weapon deed");
            AddPurchase(ref builder, 197, WeaponExchangePurchase.GemPouch, "Gem pouch");
            AddPurchase(ref builder, 228, WeaponExchangePurchase.EtherealPowder, "Ethereal powder");
            builder.AddButton(22, 265, 4005, 4007, RecyclingButton);
            builder.AddLabel(55, 265, LabelHue, "Recycle enchanted gear");
        }

        private static void AddPurchase(ref DynamicGumpBuilder builder, int y, WeaponExchangePurchase action, string name)
        {
            builder.AddButton(22, y, 4005, 4007, (int)action);
            builder.AddLabel(55, y, LabelHue, $"{name}  ·  {Price(action):N0} tokens");
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile != owner || info.ButtonID == 0 || !exchange.CheckReach(owner))
            {
                return;
            }

            if (info.ButtonID == RecyclingButton)
            {
                exchange.StartRecycling(owner);
                return;
            }

            if (Price((WeaponExchangePurchase)info.ButtonID) > 0)
            {
                exchange.TryPurchase(owner, (WeaponExchangePurchase)info.ButtonID);
                exchange.ShowExchange(owner);
            }
        }
    }

    private sealed class RecycleGump(WeaponLevelExchange exchange, PlayerMobile owner, Item item) : DynamicGump(55, 55)
    {
        private const int LabelHue = 0x481;

        public override bool Singleton => true;

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();
            builder.AddBackground(0, 0, 405, 165, 0x13BE);
            builder.AddLabel(22, 15, LabelHue, "Recycle this item?");
            if (item.Name is { } name)
            {
                builder.AddLabelCropped(22, 44, 273, 23, LabelHue, name);
            }
            else if (item.LabelNumber > 0)
            {
                builder.AddHtmlLocalized(22, 44, 273, 23, item.LabelNumber, 0xFFFFFF);
            }

            builder.AddItem(333, 29, item.ItemID, item.Hue);
            builder.AddItemProperty(item.Serial);
            builder.AddLabel(22, 78, LabelHue, $"This destroys the item and gives {RecyclingAward:N0} tokens.");
            builder.AddButton(22, 117, 4005, 4007, 1);
            builder.AddLabel(55, 117, LabelHue, "Recycle");
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile == owner && info.ButtonID == 1 && exchange.TryRecycle(owner, item))
            {
                exchange.ShowExchange(owner);
            }
        }
    }
}
