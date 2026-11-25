using System;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Tests;
using Server.Tests.Network;
using Xunit;

namespace UOContent.Tests.Mobiles;

[Collection("Sequential UOContent Tests")]
public class PetCombatRoleGumpTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void TrainingOpensRolePreviewAndOnlyConfirmationAppliesIt()
    {
        var owner = _world.NewPlayer();
        owner.Player = true;
        owner.MoveToWorld(new Point3D(1100, 1100, 0), Map.Felucca);
        var pet = _world.NewCreature();
        pet.Tamable = true;
        Assert.True(pet.SetControlMaster(owner));
        pet.MoveToWorld(owner.Location, owner.Map);

        using var state = PacketTestUtilities.CreateTestNetState();
        state.Account = new MockAccount();
        state.Mobile = owner;
        owner.NetState = state;
        try
        {
            PetProgressionGump.DisplayTo(owner, pet);
            owner.FindGump<PetProgressionGump>().OnResponse(state, Reply(103));
            Assert.Null(owner.FindGump<PetCombatRoleGump>());

            pet.AwardPetExperience(585);
            Assert.Equal(10, pet.PetLevel);
            PetProgressionGump.DisplayTo(owner, pet);
            owner.FindGump<PetProgressionGump>().OnResponse(state, Reply(103));
            var choices = Assert.IsType<PetCombatRoleGump>(owner.FindGump<PetCombatRoleGump>());
            choices.OnResponse(state, Reply(2));
            Assert.Equal(PetCombatRole.None, pet.PetRole);

            var preview = Assert.IsType<PetCombatRoleGump>(owner.FindGump<PetCombatRoleGump>());
            Assert.NotSame(choices, preview);
            preview.OnResponse(state, Reply(4));
            Assert.Equal(PetCombatRole.Striker, pet.PetRole);
            Assert.NotNull(owner.FindGump<PetProgressionGump>());
        }
        finally
        {
            owner.CloseGump<PetProgressionGump>();
            owner.CloseGump<PetCombatRoleGump>();
            owner.NetState = null;
            state.Mobile = null;
        }
    }

    private static RelayInfo Reply(int button) => new(button, [], [], [], []);
}
