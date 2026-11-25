using System;
using Server;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Mobiles;

[Collection("Sequential UOContent Tests")]
public class PetTrainingEnhancementTests : IDisposable
{
    private readonly MobileTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    private (PlayerMobile Owner, TestCreature Pet) NewPet(int level = 10)
    {
        var owner = _harness.Track(new PlayerMobile());
        owner.RawStr = 100;
        owner.MoveToWorld(new Point3D(1100, 1100, 0), Map.Felucca);
        var pet = _harness.NewCreature();
        pet.Tamable = true;
        pet.HitsMaxSeed = 100;
        pet.InitializePetProgression(2, 45);
        pet.MoveToWorld(new Point3D(1101, 1100, 0), owner.Map);
        Assert.True(pet.SetControlMaster(owner));
        var advances = level - 1;
        pet.AwardPetExperience(25 * advances + 5 * advances * (advances - 1));
        Assert.Equal(level, pet.PetLevel);
        return (owner, pet);
    }

    private T PrepareOpponent<T>(T foe, int hits = 100) where T : TestCreature
    {
        _harness.Track(foe);
        foe.HitsMaxSeed = hits;
        foe.Hits = hits;
        foe.StamMaxSeed = 100;
        foe.Stam = 100;
        foe.MoveToWorld(new Point3D(1102, 1100, 0), Map.Felucca);
        return foe;
    }

    [Fact]
    public void RoleRequiresLevelTenCurrentOwnerAndNoFightAndKeepsBreedingPauseOnSave()
    {
        var (owner, pet) = NewPet(9);
        Assert.False(pet.TryChoosePetCombatRole(owner, PetCombatRole.Guardian));
        Assert.True(pet.TrySetPetBreedingPause(owner, true));
        pet.AwardPetExperience(105);
        Assert.Equal(10, pet.PetLevel);
        Assert.False(pet.TryChoosePetCombatRole(_harness.NewPlayer(), PetCombatRole.Guardian));
        Assert.True(pet.TryChoosePetCombatRole(owner, PetCombatRole.Guardian));

        pet.Combatant = PrepareOpponent(new TestCreature());
        Assert.False(pet.TryChoosePetCombatRole(owner, PetCombatRole.Scout));
        pet.Combatant = null;
        Assert.True(pet.TryChoosePetCombatRole(owner, PetCombatRole.Scout));
        Assert.True(pet.PetBreedingPauseEnabled);

        var writer = new BufferWriter(true);
        pet.Serialize(writer);
        var copy = _harness.Track(new TestCreature(World.NewMobile));
        var reader = new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
        copy.Deserialize(reader);
        Assert.Equal(writer.Position, reader.Position);
        Assert.Equal(PetCombatRole.Scout, copy.PetRole);
        Assert.True(copy.PetBreedingPauseEnabled);
    }

    [Fact]
    public void GuardianHelpsOnlyAnInjuredOwnerThreatenedByTheOpponentAndRespectsCooldown()
    {
        var (owner, pet) = NewPet();
        var foe = PrepareOpponent(new TestCreature());
        Assert.True(pet.TryChoosePetCombatRole(owner, PetCombatRole.Guardian));
        owner.Hits = owner.HitsMax - 20;
        var initial = owner.Hits;
        pet.OnPetRoleHit(foe, 12);
        Assert.Equal(initial, owner.Hits);

        foe.Combatant = owner;
        pet.OnPetRoleHit(foe, 12);
        Assert.Equal(initial + 8, owner.Hits);
        pet.OnPetRoleHit(foe, 12);
        Assert.Equal(initial + 8, owner.Hits);
    }

    [Fact]
    public void StrikerCanFollowUpAtMaximumLevel()
    {
        var (owner, pet) = NewPet(45);
        var foe = PrepareOpponent(new TestCreature());
        Assert.True(pet.TryChoosePetCombatRole(owner, PetCombatRole.Striker));

        pet.OnPetRoleHit(foe, 24);

        Assert.Equal(94, foe.Hits);
        Assert.Equal(0, pet.PetExperience);
    }

    [Fact]
    public void ScoutTiresAWildOpponentWithoutAffectingHealthOrControlledPets()
    {
        var (owner, pet) = NewPet();
        var foe = PrepareOpponent(new TestCreature());
        Assert.True(pet.TryChoosePetCombatRole(owner, PetCombatRole.Scout));

        Assert.True(foe.SetControlMaster(_harness.NewPlayer()));
        pet.OnPetRoleHit(foe, 10);
        Assert.Equal(100, foe.Stam);
        Assert.True(foe.SetControlMaster(null));
        pet.OnPetRoleHit(foe, 10);

        Assert.Equal(90, foe.Stam);
        Assert.Equal(100, foe.Hits);
    }

    [Fact]
    public void RecentDiversityRejectsWeakAndRepeatedFamiliesAndCapsThreePerWindow()
    {
        var (_, pet) = NewPet();
        var now = Core.Now;
        var weak = PrepareOpponent(new TestCreature(), 49);
        var first = PrepareOpponent(new TestCreature());
        var duplicate = PrepareOpponent(new TestCreature());
        var second = PrepareOpponent(new SecondFamily());
        var third = PrepareOpponent(new ThirdFamily());
        var fourth = PrepareOpponent(new FourthFamily());

        Assert.Equal(0, pet.TakePetDiversityBonus(weak, now));
        Assert.Equal(8, pet.TakePetDiversityBonus(first, now));
        Assert.Equal(0, pet.TakePetDiversityBonus(duplicate, now));
        Assert.Equal(8, pet.TakePetDiversityBonus(second, now));
        Assert.Equal(8, pet.TakePetDiversityBonus(third, now));
        Assert.Equal(0, pet.TakePetDiversityBonus(fourth, now.AddMinutes(29)));
        Assert.Equal(8, pet.TakePetDiversityBonus(duplicate, now.AddMinutes(30)));
    }

    private sealed class SecondFamily : TestCreature;
    private sealed class ThirdFamily : TestCreature;
    private sealed class FourthFamily : TestCreature;
}
