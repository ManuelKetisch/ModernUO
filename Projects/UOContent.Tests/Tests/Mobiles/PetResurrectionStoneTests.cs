using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests.Mobiles;

[Collection("Sequential UOContent Tests")]
public class PetResurrectionStoneTests : IDisposable
{
    private readonly MobileTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void StoneOnlyResurrectsTheOwnersPetAndRechecksReach()
    {
        var owner = _harness.NewPlayer();
        var stranger = _harness.NewPlayer();
        var pet = _harness.NewCreature();
        var stone = _harness.TrackItem(new PetResurrectionStone());
        var map = Map.Felucca;

        stone.MoveToWorld(new Point3D(1100, 1100, 0), map);
        owner.MoveToWorld(new Point3D(1101, 1100, 0), map);
        stranger.MoveToWorld(owner.Location, owner.Map);
        pet.MoveToWorld(new Point3D(1101, 1101, 0), map);
        pet.Tamable = true;
        Assert.True(pet.SetControlMaster(owner));
        pet.IsBonded = true;
        pet.IsDeadPet = true;

        stone.OnDoubleClick(stranger);
        Assert.NotNull(stranger.Target);
        stranger.Target.Invoke(stranger, pet);
        Assert.True(pet.IsDeadPet);

        stone.OnDoubleClick(owner);
        Assert.NotNull(owner.Target);
        stone.MoveToWorld(new Point3D(1120, 1100, 0), map);
        owner.Target.Invoke(owner, pet);
        Assert.True(pet.IsDeadPet);

        stone.MoveToWorld(new Point3D(1100, 1100, 0), map);
        stone.OnDoubleClick(owner);
        Assert.NotNull(owner.Target);
        owner.Target.Invoke(owner, pet);

        Assert.False(pet.IsDeadPet);
        Assert.Same(owner, pet.ControlMaster);
    }
}
