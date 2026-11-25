using System;
using Server;
using Server.Engines.BioEngineering;
using Server.Gumps;
using Server.Mobiles;
using Server.SkillHandlers;
using Server.Tests;
using Server.Tests.Network;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public class BioAnimalLoreTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void SuccessfulAnimalLoreOffersTheSameDonorReportAndRechecksReach()
    {
        var engineer = _world.NewPlayer();
        engineer.Player = true;
        engineer.MoveToWorld(new Point3D(1800, 1800, 0), Map.Felucca);
        engineer.Skills.AnimalLore.Cap = 120;
        engineer.Skills.AnimalLore.Base = 120;
        engineer.Skills.AnimalTaming.Base = 100;
        engineer.Skills.Tinkering.Base = 100;

        var donor = _world.NewCreature();
        donor.Body = 400;
        donor.MoveToWorld(engineer.Location, engineer.Map);

        using var state = PacketTestUtilities.CreateTestNetState();
        state.Account = new MockAccount();
        state.Mobile = engineer;
        engineer.NetState = state;
        var priorCheck = Mobile.SkillCheckTargetHandler;
        try
        {
            Mobile.SkillCheckTargetHandler = (_, _, _, _, _) => false;
            AnimalLore.OnUse(engineer);
            engineer.Target.Invoke(engineer, donor);
            Assert.Null(engineer.FindGump<AnimalLoreGump>());

            Mobile.SkillCheckTargetHandler = (_, _, _, _, _) => true;
            AnimalLore.OnUse(engineer);
            engineer.Target.Invoke(engineer, donor);
            var lore = Assert.IsType<AnimalLoreGump>(engineer.FindGump<AnimalLoreGump>());
            donor.MoveToWorld(new Point3D(1900, 1900, 0), Map.Felucca);
            lore.OnResponse(state, new RelayInfo(3, [], [], [], []));
            Assert.Null(engineer.FindGump<BioDonorJournal.JournalGump>());

            donor.MoveToWorld(engineer.Location, engineer.Map);
            AnimalLore.OnUse(engineer);
            engineer.Target.Invoke(engineer, donor);
            engineer.FindGump<AnimalLoreGump>().OnResponse(state, new RelayInfo(3, [], [], [], []));
            Assert.NotNull(engineer.FindGump<BioDonorJournal.JournalGump>());
        }
        finally
        {
            Mobile.SkillCheckTargetHandler = priorCheck;
            engineer.CloseGump<AnimalLoreGump>();
            engineer.CloseGump<BioDonorJournal.JournalGump>();
            engineer.NetState = null;
            state.Mobile = null;
        }
    }

    [Fact]
    public void NamedDonorFamiliesOfferDistinctPersistedMimicMoves()
    {
        var slime = _world.Track(new Slime());
        var harpy = _world.Track(new StoneHarpy());
        var fire = _world.Track(new FireElemental());
        var ice = _world.Track(new IceElemental());
        Assert.Equal(3, CaptureTrait(slime));
        Assert.Equal(7, CaptureTrait(harpy));
        Assert.Equal(1, CaptureTrait(fire));
        Assert.Equal(6, CaptureTrait(ice));

        var unsafeDonor = _world.NewCreature();
        unsafeDonor.Body = 402;
        Assert.Null(BioVial.Capture(BioSampleKind.Mimic, unsafeDonor));
    }

    private static int CaptureTrait(BaseCreature donor) => BioVial.Capture(BioSampleKind.Mimic, donor)[3];
}
