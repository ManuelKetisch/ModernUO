using System;
using Server;
using Server.Engines.CannedEvil;
using Server.Engines.Craft;
using Server.Engines.Quests.Doom;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public sealed class BoneCraftingTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public BoneCraftingTests()
    {
        if (DefTailoring.CraftSystem == null)
        {
            DefTailoring.Initialize();
        }

        if (DefCarpentry.CraftSystem == null)
        {
            DefCarpentry.Initialize();
        }
    }

    public void Dispose() => _world.Dispose();

    [Theory]
    [InlineData(typeof(BoneHelm), 1)]
    [InlineData(typeof(BoneGloves), 1)]
    [InlineData(typeof(BoneArms), 2)]
    [InlineData(typeof(BoneLegs), 3)]
    [InlineData(typeof(BoneChest), 5)]
    public void OrdinaryScissorsReturnOnlyTheExpectedBones(Type armorType, int craftedYield)
    {
        var player = NewPlayer();
        var scissors = Put(player, new Scissors());
        var bones = Put(player, new Bone(1));
        var crafted = Put(player, (BaseArmor)Activator.CreateInstance(armorType));
        crafted.PlayerConstructed = true;
        crafted.Hue = 0x489;
        var loot = Put(player, (BaseArmor)Activator.CreateInstance(armorType));

        Cut(player, scissors, crafted);
        Assert.True(crafted.Deleted);
        Assert.Equal(1 + craftedYield, bones.Amount);
        Cut(player, scissors, loot);
        Assert.True(loot.Deleted);
        Assert.Equal(2 + craftedYield, bones.Amount);
        Assert.Null(player.Backpack.FindItemByType<Leather>());
    }

    [Fact]
    public void ProtectedPiecesAndArtifactsArePreserved()
    {
        var player = NewPlayer();
        var scissors = Put(player, new Scissors());
        var blessed = Put(player, new BoneChest { LootType = LootType.Blessed });
        var insured = Put(player, new BoneChest { Insured = true });
        var artifact = Put(player, new MidnightBracers());
        var orcHelm = Put(player, new OrcHelm());
        var ground = _world.TrackItem(new BoneChest());
        ground.MoveToWorld(player.Location, player.Map);

        foreach (var item in new BaseArmor[] { blessed, insured, artifact, orcHelm, ground })
        {
            Cut(player, scissors, item);
            Assert.False(item.Deleted);
        }
        Assert.Null(player.Backpack.FindItemByType<Bone>());

        var leather = Put(player, new LeatherArms { PlayerConstructed = true });
        Cut(player, scissors, leather);
        Assert.True(leather.Deleted);
        Assert.NotNull(_world.TrackItem(player.Backpack.FindItemByType<Leather>()));
    }

    [Fact]
    public void CarpentryMakesAPlayableFluteFromRecoveredBones()
    {
        var player = NewPlayer();
        player.Skills.Carpentry.Base = 105;
        player.Skills.Musicianship.Base = 50;
        var saw = Put(player, new Saw(10));
        var scissors = Put(player, new Scissors());
        var bones = Put(player, new Bone(11));
        var chest = Put(player, new BoneChest { PlayerConstructed = true });
        Cut(player, scissors, chest);

        var system = DefCarpentry.CraftSystem;
        var recipe = system.CraftItems.SearchFor(typeof(BoneFlute));
        Assert.NotNull(recipe);
        Assert.Equal("Bone Flute", recipe.NameString);
        var resource = Assert.Single(recipe.Resources);
        Assert.Equal(typeof(Bone), resource.ItemType);
        Assert.Equal(15, resource.Amount);
        Assert.Contains(recipe.Skills, skill => skill.SkillToMake == SkillName.Carpentry && skill.MinSkill == 80.0);
        Assert.Contains(recipe.Skills, skill => skill.SkillToMake == SkillName.Musicianship && skill.MinSkill == 45.0);

        recipe.CompleteCraft(1, false, player, system, typeof(FrostwoodLog), saw, null);

        var flute = _world.TrackItem(player.Backpack.FindItemByType<BoneFlute>());
        Assert.NotNull(flute);
        Assert.Equal("bone flute", flute.DefaultName);
        Assert.True(flute.UsesRemaining > 0);
        Assert.Equal(1, bones.Amount);
        Assert.Equal(9, saw.UsesRemaining);
    }

    [Fact]
    public void BoneFluteKeepsInstrumentPropertiesAfterSerialization()
    {
        var flute = _world.TrackItem(new BoneFlute
        {
            Quality = InstrumentQuality.Exceptional,
            UsesRemaining = 47
        });

        var writer = new BufferWriter(true);
        flute.Serialize(writer);
        var restored = _world.TrackItem(new BoneFlute(World.NewItem));
        var reader = new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
        restored.Deserialize(reader);

        Assert.Equal(writer.Position, reader.Position);
        Assert.Equal(flute.Hue, restored.Hue);
        Assert.Equal(InstrumentQuality.Exceptional, restored.Quality);
        Assert.Equal(47, restored.UsesRemaining);
    }

    [Theory]
    [InlineData(typeof(BoneScythe), 100)]
    [InlineData(typeof(SkullHeap), 200)]
    public void BoneCarvingRegistersTheRecipes(Type type, int boneCost)
    {
        var system = DefCarpentry.CraftSystem;
        var recipe = system.CraftItems.SearchFor(type);
        Assert.NotNull(recipe);
        Assert.Equal("Bone Carving", recipe.GroupNameString);
        Assert.Contains(recipe.Resources, res => res.ItemType == typeof(Bone) && res.Amount == boneCost);
        Assert.Contains(recipe.Skills, skill => skill.SkillToMake == SkillName.Carpentry && skill.MinSkill == 80);
        Assert.Contains(recipe.Skills, skill => skill.SkillToMake == SkillName.Necromancy && skill.MinSkill == 105);
    }

    [Fact]
    public void BoneScytheUsesBonesWithoutInheritingTheSelectedWood()
    {
        var player = NewCarver();
        var saw = Put(player, new Saw(10));
        var bones = Put(player, new Bone(101));
        var system = DefCarpentry.CraftSystem;
        var recipe = system.CraftItems.SearchFor(typeof(BoneScythe));

        player.Skills.Necromancy.Base = 104.9;
        recipe.GetSuccessChance(player, typeof(FrostwoodLog), system, false, out var hasSkills);
        Assert.False(hasSkills);
        player.Skills.Necromancy.Base = 105.0;

        recipe.CompleteCraft(1, false, player, system, typeof(FrostwoodLog), saw, null);

        var scythe = _world.TrackItem(player.Backpack.FindItemByType<BoneScythe>());
        Assert.NotNull(scythe);
        Assert.Equal(CraftResource.None, scythe.Resource);
        Assert.True(scythe.PlayerConstructed);
        Assert.Equal(1, bones.Amount);
        Assert.Equal(9, saw.UsesRemaining);
    }

    [Fact]
    public void SkullHeapConsumesTheSpecialIngredientsAndOnlyStoresSkulls()
    {
        var player = NewCarver();
        var saw = Put(player, new Saw(10));
        var bones = Put(player, new Bone(201));
        var skulls = Put(player, new JinxedSkull());
        skulls.Amount = 11;
        var ribs = Put(player, new HellhoundRib());
        ribs.Amount = 7;
        var system = DefCarpentry.CraftSystem;
        var recipe = system.CraftItems.SearchFor(typeof(SkullHeap));

        Assert.Contains(recipe.Resources, res => res.ItemType == typeof(JinxedSkull) && res.Amount == 10);
        Assert.Contains(recipe.Resources, res => res.ItemType == typeof(HellhoundRib) && res.Amount == 6);
        recipe.CompleteCraft(1, false, player, system, typeof(FrostwoodLog), saw, null);

        var heap = _world.TrackItem(player.Backpack.FindItemByType<SkullHeap>());
        Assert.NotNull(heap);
        Assert.Equal(1, bones.Amount);
        Assert.Equal(1, skulls.Amount);
        Assert.Equal(1, ribs.Amount);
        Assert.Equal(9, saw.UsesRemaining);

        var goldenSkull = Put(player, new GoldenSkull());
        var championSkull = Put(player, new ChampionSkull(ChampionSkullType.Power));
        Assert.True(heap.TryDropItem(player, skulls, false));
        Assert.True(heap.TryDropItem(player, goldenSkull, false));
        Assert.True(heap.OnDragDropInto(player, championSkull, new Point3D(30, 50, 0)));
        Assert.False(heap.TryDropItem(player, bones, false));
        Assert.False(heap.OnDragDropInto(player, ribs, new Point3D(30, 50, 0)));
        Assert.True(bones.IsChildOf(player.Backpack));
        Assert.True(championSkull.IsChildOf(heap));
    }

    [Fact]
    public void RecipeIngredientsComeFromWildCreatureDeathLoot()
    {
        var knight = _world.Track(new BoneKnight());
        var hound = _world.Track(new HellHound());
        knight.GenerateLoot(true);
        hound.GenerateLoot(true);
        Assert.Null(knight.Backpack?.FindItemByType<JinxedSkull>());
        Assert.Null(hound.Backpack?.FindItemByType<HellhoundRib>());

        knight.GenerateLoot(false);
        hound.GenerateLoot(false);
        Assert.Equal(1, knight.Backpack.GetAmount(typeof(JinxedSkull)));
        Assert.Equal(1, hound.Backpack.GetAmount(typeof(HellhoundRib)));

        var pet = _world.Track(new HellHound());
        Assert.True(pet.SetControlMaster(NewPlayer()));
        pet.GenerateLoot(false);
        Assert.Null(pet.Backpack?.FindItemByType<HellhoundRib>());
    }

    private PlayerMobile NewCarver()
    {
        var player = NewPlayer();
        player.Skills.Carpentry.Base = 105.0;
        player.Skills.Necromancy.Base = 105.0;
        return player;
    }

    private PlayerMobile NewPlayer()
    {
        var player = _world.NewPlayer();
        player.AddItem(_world.TrackItem(new Backpack()));
        player.MoveToWorld(new Point3D(1100, 1100, 0), Map.Felucca);
        return player;
    }

    private T Put<T>(Mobile player, T item) where T : Item
    {
        _world.TrackItem(item);
        player.Backpack.DropItem(item);
        return item;
    }

    private static void Cut(Mobile from, Scissors scissors, Item item)
    {
        scissors.OnDoubleClick(from);
        Assert.NotNull(from.Target);
        from.Target.Invoke(from, item);
    }
}
