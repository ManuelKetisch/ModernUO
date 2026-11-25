using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Mobiles;

[Collection("Sequential UOContent Tests")]
public class PetOintmentTests : IDisposable
{
    private readonly MobileTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void MixedOintmentRetainsTheIngredientChoiceAndCapsTheBoost()
    {
        var owner = _harness.NewPlayer();
        var pet = _harness.NewCreature();
        owner.MoveToWorld(new Point3D(1100, 1100, 0), Map.Felucca);
        pet.MoveToWorld(new Point3D(1101, 1100, 0), owner.Map);
        pet.Tamable = true;
        Assert.True(pet.SetControlMaster(owner));
        pet.HitsMaxSeed = 1080;

        var pack = new Backpack();
        owner.AddItem(pack);
        var herbs = new Ginseng(139);
        var ointment = new PetOintment();
        pack.DropItem(herbs);
        pack.DropItem(ointment);

        Assert.False(ointment.TryMix(owner, herbs));
        Assert.Equal(139, herbs.Amount);
        Assert.Equal(PetOintmentKind.Unmixed, ointment.Kind);
        herbs.Amount = 140;

        ointment.OnDoubleClick(owner);
        Assert.NotNull(owner.Target);
        owner.Target.Invoke(owner, herbs);

        Assert.True(herbs.Deleted);
        Assert.Equal(PetOintmentKind.Hits, ointment.Kind);

        var writer = new BufferWriter(true);
        ointment.Serialize(writer);
        ointment.Delete();
        var reader = new BufferReader(writer.Buffer.AsSpan(0, (int)writer.Position).ToArray());
        var copy = _harness.TrackItem(new PetOintment(World.NewItem));
        copy.Deserialize(reader);
        pack.DropItem(copy);

        Assert.Equal(writer.Position, reader.Position);
        Assert.Equal(PetOintmentKind.Hits, copy.Kind);
        pet.HitsMaxSeed = 1100;
        Assert.False(copy.TryApply(owner, pet));
        Assert.False(copy.Deleted);

        pet.HitsMaxSeed = 1080;
        copy.OnDoubleClick(owner);
        Assert.NotNull(owner.Target);
        owner.Target.Invoke(owner, pet);

        Assert.Equal(1100, pet.HitsMaxSeed);
        Assert.True(copy.Deleted);
    }
}
