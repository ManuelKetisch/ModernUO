using System;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public sealed class TradeToolsTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void ResourceKeysPreserveStockAtTheLimitAndOnReload()
    {
        var owner = NewPlayer();
        var stranger = NewPlayer();
        var keys = Put(owner, new TradeResourceStorage());
        var iron = Put(owner, new IronIngot(59999));
        var moreIron = Put(owner, new IronIngot(1));
        var excess = Put(owner, new IronIngot(1));
        var gold = Put(owner, new GoldIngot(2));
        var foreign = Put(stranger, new GoldIngot(2));

        Assert.False(keys.TryStore(owner, foreign));
        Assert.False(keys.TryStore(stranger, iron));
        Assert.True(keys.TryStore(owner, iron));
        Assert.True(keys.TryStore(owner, moreIron));
        Assert.True(moreIron.Deleted);
        Assert.False(keys.TryStore(owner, excess));
        Assert.True(excess.IsChildOf(owner.Backpack));
        Assert.True(keys.TryStore(owner, gold));
        Assert.Equal(60000, keys.GetStoredAmount(typeof(IronIngot)));
        Assert.Equal(2, keys.GetStoredAmount(typeof(GoldIngot)));

        owner.Backpack.MaxItems = owner.Backpack.TotalItems;
        Assert.False(keys.TryWithdraw(owner, gold, 2));
        Assert.Equal(2, keys.GetStoredAmount(typeof(GoldIngot)));
        owner.Backpack.MaxItems = 125;

        Assert.True(keys.TryWithdraw(owner, iron, 100));
        Assert.Equal(59900, keys.GetStoredAmount(typeof(IronIngot)));
        Assert.Equal(101, owner.Backpack.GetAmount(typeof(IronIngot)));

        var writer = new BufferWriter(true);
        keys.Serialize(writer);
        var restored = _world.TrackItem(new TradeResourceStorage(World.NewItem));
        var reader = new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
        restored.Deserialize(reader);
        Assert.Equal(writer.Position, reader.Position);
        Assert.Equal(59900, restored.GetStoredAmount(typeof(IronIngot)));
        Assert.Equal(2, restored.GetStoredAmount(typeof(GoldIngot)));
    }

    [Fact]
    public void AllStorageRecipesProduceTheirOwnCatalogAndDisplay()
    {
        if (DefTinkering.CraftSystem == null)
        {
            DefTinkering.Initialize();
        }

        var tinkering = DefTinkering.CraftSystem;
        var group = tinkering.CraftGroups[tinkering.CraftGroups.SearchFor("Trade Tools")];
        var storageRecipes = group.CraftItems.Where(item => item.ItemType == typeof(TradeResourceStorage)).ToArray();
        Assert.Equal(Enum.GetValues<TradeSupplyKind>().Length, storageRecipes.Length);
        Assert.Contains(group.CraftItems, item => item.ItemType == typeof(MobileForge));
        foreach (var recipe in storageRecipes)
        {
            var storage = _world.TrackItem(new TradeResourceStorage());
            storage.OnCraft(1, false, null, tinkering, typeof(IronIngot), null, recipe, 0);
            Assert.Equal(recipe.NameString, storage.DefaultName);
        }
    }

    [Fact]
    public void ToolHousesConserveUsesWithoutChangingRunicTierOrMaker()
    {
        var owner = NewPlayer();
        var house = Put(owner, new TradeResourceStorage(TradeSupplyKind.RunicTools));
        var first = Put(owner, new RunicHammer(CraftResource.DullCopper, 70));
        var second = Put(owner, new RunicHammer(CraftResource.DullCopper, 30));
        var bronze = Put(owner, new RunicHammer(CraftResource.Bronze, 40));
        var signed = Put(owner, new RunicHammer(CraftResource.DullCopper, 50) { Crafter = "Ariadne" });
        var ordinary = Put(owner, new SmithHammer(100));

        Assert.False(house.TryStore(owner, ordinary));
        Assert.True(house.TryStore(owner, first));
        Assert.True(house.TryStore(owner, second));
        Assert.True(house.TryStore(owner, bronze));
        Assert.True(house.TryStore(owner, signed));
        Assert.True(second.Deleted);
        Assert.False(bronze.Deleted);
        Assert.False(signed.Deleted);
        Assert.Equal(100, first.UsesRemaining);
        Assert.Equal(150, house.GetStoredAmount(typeof(RunicHammer), CraftResource.DullCopper));
        Assert.Equal(40, house.GetStoredAmount(typeof(RunicHammer), CraftResource.Bronze));

        Assert.False(house.TryWithdraw(owner, signed, 15));
        Assert.Null(owner.Backpack.FindItemByType<RunicHammer>());
        var output = Put(owner, new RunicHammer(CraftResource.DullCopper, 5) { Crafter = "Ariadne" });
        Assert.True(house.TryWithdraw(owner, signed, 15));
        Assert.Same(output, owner.Backpack.FindItemByType<RunicHammer>());
        Assert.Equal(1, owner.Backpack.GetAmount(typeof(RunicHammer)));
        Assert.Equal(CraftResource.DullCopper, output.Resource);
        Assert.Equal("Ariadne", output.Crafter);
        Assert.Equal(20, output.UsesRemaining);
        Assert.Equal(35, signed.UsesRemaining);
        Assert.Equal(135, house.GetStoredAmount(typeof(RunicHammer), CraftResource.DullCopper));
    }

    [Fact]
    public void ToolHousePreservesPickaxesWhileConsolidatingPlainShovels()
    {
        var owner = NewPlayer();
        var house = Put(owner, new TradeResourceStorage(TradeSupplyKind.Tools));
        var first = Put(owner, new SturdyPickaxe(120) { Name = "Quartz pick" });
        var second = Put(owner, new SturdyPickaxe(150));
        var ordinary = Put(owner, new Pickaxe());
        var shovel = Put(owner, new SturdyShovel(40));
        var spare = Put(owner, new SturdyShovel(60));
        var gargoyle = Put(owner, new GargoylesPickaxe());

        Assert.False(house.TryStore(owner, gargoyle));
        Assert.True(house.TryStore(owner, first));
        Assert.True(house.TryStore(owner, second));
        Assert.True(house.TryStore(owner, ordinary));
        Assert.True(house.TryStore(owner, shovel));
        Assert.True(house.TryStore(owner, spare));
        Assert.Equal(270, house.GetStoredAmount(typeof(SturdyPickaxe)));
        Assert.Equal(50, house.GetStoredAmount(typeof(Pickaxe)));
        Assert.False(house.TryWithdraw(owner, first, 40));
        Assert.True(house.TryWithdraw(owner, first, 120));
        Assert.True(first.IsChildOf(owner.Backpack));
        Assert.Equal("Quartz pick", first.Name);
        Assert.Equal(120, first.UsesRemaining);

        Assert.True(spare.Deleted);
        Assert.Equal(100, shovel.UsesRemaining);
        var availableShovel = Put(owner, new SturdyShovel(10));
        Assert.True(house.TryWithdraw(owner, shovel, 15));
        Assert.Equal(85, shovel.UsesRemaining);
        Assert.Equal(25, availableShovel.UsesRemaining);
        Assert.Same(availableShovel, owner.Backpack.FindItemByType<SturdyShovel>());
        Assert.Equal(1, owner.Backpack.GetAmount(typeof(SturdyShovel)));
    }

    [Fact]
    public void PlainMiningToolsConsolidateAndTransferUsesWithoutCreatingMoreWeapons()
    {
        var owner = NewPlayer();
        var house = Put(owner, new TradeResourceStorage(TradeSupplyKind.Tools));
        var first = Put(owner, new Pickaxe { MaxHitPoints = 40, HitPoints = 40 });
        var second = Put(owner, new Pickaxe { MaxHitPoints = 40, HitPoints = 40 });
        var magical = Put(owner, new Pickaxe { MaxHitPoints = 40, HitPoints = 40 });
        magical.Attributes.BonusStr = 5;

        Assert.True(house.TryStore(owner, first));
        Assert.True(house.TryStore(owner, second));
        Assert.True(second.Deleted);
        Assert.True(house.TryStore(owner, magical));
        Assert.False(magical.Deleted);
        Assert.Equal(100, first.UsesRemaining);
        Assert.Equal(150, house.GetStoredAmount(typeof(Pickaxe)));
        Assert.False(house.TryWithdraw(owner, first, 12));
        Assert.Null(owner.Backpack.FindItemByType<Pickaxe>());

        var destination = Put(owner, new Pickaxe { MaxHitPoints = 40, HitPoints = 40 });
        Assert.True(house.TryWithdraw(owner, first, 12));
        Assert.Equal(88, first.UsesRemaining);
        Assert.Equal(62, destination.UsesRemaining);
        Assert.Equal(40, destination.HitPoints);
        Assert.False(house.TryWithdraw(owner, magical, 12));
        Assert.Same(destination, owner.Backpack.FindItemByType<Pickaxe>());
        Assert.Equal(1, owner.Backpack.GetAmount(typeof(Pickaxe)));
    }

    [Fact]
    public void MinocStarterKeysCanBeClaimedOncePerAccount()
    {
        var owner = NewPlayer();
        var alternate = NewPlayer();
        var supplies = _world.TrackItem(new MinocTradeSupplies());
        supplies.MoveToWorld(owner.Location, owner.Map);
        var previous = AccountSecurity.CurrentAlgorithm;
        Account account = null;
        try
        {
            AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
            account = new Account($"trade-{Guid.NewGuid():N}", "temporary-test-password");
            account[0] = owner;
            account[1] = alternate;
            Put(owner, new IronIngot(1));
            owner.Backpack.MaxItems = owner.Backpack.TotalItems;
            Assert.False(supplies.TryClaim(owner));
            owner.Backpack.MaxItems = 125;
            Assert.True(supplies.TryClaim(owner));
            Assert.Equal(TradeSupplyKind.Spellcasting, owner.Backpack.FindItemByType<TradeResourceStorage>().Kind);
            Assert.False(supplies.TryClaim(owner));
            Assert.False(supplies.TryClaim(alternate));
            Assert.Null(alternate.Backpack.FindItemByType<TradeResourceStorage>());
        }
        finally
        {
            account?.Delete();
            AccountSecurity.CurrentAlgorithm = previous;
        }
    }

    [Fact]
    public void TrinsicExchangeSuppliesGemPouchAndEtherealPowderForManaOintment()
    {
        var owner = NewPlayer();
        var exchange = _world.TrackItem(new WeaponLevelExchange());
        exchange.MoveToWorld(owner.Location, owner.Map);
        Put(owner, new WeaponRecyclingToken(WeaponLevelExchange.GemPouchPrice + WeaponLevelExchange.EtherealPowderPrice));
        Assert.True(exchange.TryPurchase(owner, WeaponExchangePurchase.GemPouch));
        var pouch = owner.Backpack.FindItemByType<TradeResourceStorage>();
        Assert.Equal(TradeSupplyKind.Gems, pouch.Kind);
        Assert.True(pouch.TryStore(owner, Put(owner, new BlueDiamond())));
        Assert.True(exchange.TryPurchase(owner, WeaponExchangePurchase.EtherealPowder));
        var powder = owner.Backpack.FindItemByType<EtherealPowder>();
        var keys = Put(owner, new TradeResourceStorage(TradeSupplyKind.Spellcasting));
        Assert.True(keys.TryStore(owner, powder));
        Assert.True(keys.TryWithdraw(owner, powder, 1));
        var ointment = Put(owner, new PetOintment());
        Assert.True(ointment.TryMix(owner, powder));
        Assert.Equal(PetOintmentKind.Mana, ointment.Kind);
        Assert.True(powder.Deleted);
        Assert.Equal(0, owner.Backpack.GetAmount(typeof(WeaponRecyclingToken)));
    }

    [Theory]
    [InlineData(SkillName.Carpentry)]
    [InlineData(SkillName.Fletching)]
    public void AncientTradeHammerAndArmorBoostTheirOwnSkill(SkillName skill)
    {
        var owner = NewPlayer();
        owner.InitStats(75, 75, 75);
        owner.Skills[skill].Base = 40;
        var gloves = Put(owner, new CraftSkillBonusArmor(skill, 5) { Layer = Layer.Gloves });
        var hammer = Put(owner, new AncientTradeHammer(skill, 10));
        if (DefCarpentry.CraftSystem == null)
        {
            DefCarpentry.Initialize();
        }

        if (DefBowFletching.CraftSystem == null)
        {
            DefBowFletching.Initialize();
        }

        var runic = Put(owner, skill == SkillName.Carpentry
            ? (BaseRunicTool)new RunicDovetailSaw(CraftResource.OakWood, 5)
            : new RunicFletcherTool(CraftResource.OakWood, 5));

        Assert.Equal(600, hammer.UsesRemaining);
        Assert.Same(runic.CraftSystem, hammer.CraftSystem);
        Assert.True(owner.EquipItem(gloves));
        Assert.Equal(45, owner.Skills[skill].Value);
        Assert.True(owner.EquipItem(hammer));
        Assert.Equal(55, owner.Skills[skill].Value);
        owner.Backpack.DropItem(hammer);
        Assert.Equal(45, owner.Skills[skill].Value);
        owner.Backpack.DropItem(gloves);
        Assert.Equal(40, owner.Skills[skill].Value);
    }

    [Fact]
    public void MobileForgeRunsTheNormalOreTargetAndRejectsSomeoneElsesPack()
    {
        var owner = NewPlayer();
        var stranger = NewPlayer();
        var forge = Put(owner, new MobileForge());
        var foreignForge = Put(stranger, new MobileForge());
        var ore = Put(owner, new IronOre(3));
        ore.ItemID = 0x19B9;
        var priorCheck = Mobile.SkillCheckTargetHandler;
        Mobile.SkillCheckTargetHandler = (_, skill, _, _, _) => skill == SkillName.Mining;
        try
        {
            ore.OnDoubleClick(owner);
            Assert.NotNull(owner.Target);
            owner.Target.Invoke(owner, foreignForge);
            Assert.Equal(3, ore.Amount);
            Assert.Equal(0, owner.Backpack.GetAmount(typeof(IronIngot)));

            ore.OnDoubleClick(owner);
            Assert.NotNull(owner.Target);
            owner.Target.Invoke(owner, forge);
            Assert.True(ore.Deleted);
            Assert.Equal(6, owner.Backpack.GetAmount(typeof(IronIngot)));
        }
        finally
        {
            Mobile.SkillCheckTargetHandler = priorCheck;
        }
    }

    private PlayerMobile NewPlayer()
    {
        var player = _world.Track(new PlayerMobile { Player = true, Race = Race.Human });
        player.AddItem(_world.TrackItem(new Backpack()));
        player.MoveToWorld(new Point3D(1800, 1800, 0), Map.Felucca);
        return player;
    }

    private T Put<T>(PlayerMobile player, T item) where T : Item
    {
        player.Backpack.DropItem(_world.TrackItem(item));
        return item;
    }
}
