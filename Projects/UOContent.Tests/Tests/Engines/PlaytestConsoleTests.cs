using System;
using Server;
using Server.Accounting;
using Server.Engines.Playtesting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests;
using Server.Tests.Network;
using Xunit;

namespace UOContent.Tests.Engines;

[Collection("Sequential UOContent Tests")]
public class PlaytestConsoleTests : IDisposable
{
    private readonly MobileTestHarness _world = new();

    public void Dispose()
    {
        PlaytestConsole.Revoke();
        _world.Dispose();
    }

    [Fact]
    public void OnlyDedicatedOrdinaryAccountWithItsCurrentSessionCanBeGranted()
    {
        var regular = _world.NewPlayer();
        var dummy = _world.NewPlayer();
        using var regularState = Connect(regular, "ordinary");
        using var dummyState = Connect(dummy, PlaytestConsole.AccountName);
        using var replacement = PacketTestUtilities.CreateTestNetState();
        try
        {
            Assert.Null(PlaytestConsole.GrantSession(regular, regularState, Core.Now));
            dummy.Account.AccessLevel = AccessLevel.Owner;
            Assert.Null(PlaytestConsole.GrantSession(dummy, dummyState, Core.Now));
            dummy.Account.AccessLevel = AccessLevel.Player;

            var grant = PlaytestConsole.GrantSession(dummy, dummyState, Core.Now);
            Assert.NotNull(grant);
            Assert.True(PlaytestConsole.MayUse(grant, dummyState));
            Assert.False(grant.Authorizes(dummyState, grant.ExpiresAt));
            Assert.False(PlaytestConsole.MayUse(grant, regularState));
            Assert.False(PlaytestActions.TryIssueCheck(grant, regularState, "1000"));

            replacement.Account = dummy.Account;
            dummyState.Mobile = null;
            replacement.Mobile = dummy;
            dummy.NetState = replacement;

            Assert.False(PlaytestConsole.MayUse(grant, dummyState));
            Assert.False(PlaytestConsole.MayUse(grant, replacement));
            Assert.Null(dummy.Backpack.FindItemByType<BankCheck>());
        }
        finally
        {
            Disconnect(regular, regularState);
            Disconnect(dummy, replacement);
        }
    }

    [Fact]
    public void PreviousAuthorizationLosesAccessEvenWhenRegrantedInTheSameTick()
    {
        var dummy = _world.NewPlayer();
        using var state = Connect(dummy, PlaytestConsole.AccountName);
        try
        {
            var now = Core.Now;
            var first = PlaytestConsole.GrantSession(dummy, state, now);
            var second = PlaytestConsole.GrantSession(dummy, state, now);
            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.False(PlaytestConsole.MayUse(first, state));
            Assert.True(PlaytestConsole.MayUse(second, state));

            PlaytestConsole.Revoke();

            Assert.False(PlaytestConsole.MayUse(second, state));
        }
        finally
        {
            Disconnect(dummy, state);
        }
    }

    [Fact]
    public void AuthorizedControlsSetOnlyBoundedValuesAndIssueItemsToTheDummy()
    {
        var dummy = _world.NewPlayer();
        using var state = Connect(dummy, PlaytestConsole.AccountName);
        try
        {
            var grant = PlaytestConsole.GrantSession(dummy, state, Core.Now);
            Assert.NotNull(grant);
            Assert.False(PlaytestActions.TryIssueCheck(grant, state, "1000001"));
            Assert.True(PlaytestActions.TryIssueCheck(grant, state, "10000"));
            Assert.Equal(10000, dummy.Backpack.FindItemByType<BankCheck>().Worth);
            Assert.True(PlaytestActions.TryGiveItem(grant, state, () => new Halberd()));
            Assert.NotNull(dummy.Backpack.FindItemByType<Halberd>());

            Assert.True(PlaytestActions.TrySetStats(grant, state, "125", "130", "150"));
            Assert.False(PlaytestActions.TrySetStats(grant, state, "151", "100", "80"));
            Assert.Equal((125, 130, 150), (dummy.RawStr, dummy.RawDex, dummy.RawInt));

            var skill = dummy.Skills.Tinkering;
            Assert.False(PlaytestActions.TrySetSkill(grant, state, skill.Info.Name, "120.1"));
            Assert.True(PlaytestActions.TrySetSkill(grant, state, skill.Info.Name, "119.5"));
            Assert.Equal(119.5, skill.Base);
            Assert.Equal(119.5, skill.Cap);
        }
        finally
        {
            Disconnect(dummy, state);
        }
    }

    [Fact]
    public void PlaytestGumpRejectsOtherClientsAndUnknownItemButtons()
    {
        var dummy = _world.NewPlayer();
        var other = _world.NewPlayer();
        using var state = Connect(dummy, PlaytestConsole.AccountName);
        using var otherState = Connect(other, "ordinary");

        try
        {
            var grant = PlaytestConsole.GrantSession(dummy, state, Core.Now);
            Assert.NotNull(grant);
            dummy.SendGump(new PlaytestGump(grant));
            var panel = dummy.FindGump<PlaytestGump>();
            Assert.NotNull(panel);

            panel.OnResponse(otherState, new RelayInfo(20, [], [], [], []));
            panel.OnResponse(state, new RelayInfo(9999, [], [], [], []));
            Assert.Empty(dummy.Backpack.Items);

            panel.OnResponse(state, new RelayInfo(20, [], [], [], []));
            Assert.IsType<Longsword>(Assert.Single(dummy.Backpack.Items));
            Assert.Empty(other.Backpack.Items);
            Assert.Equal(AccessLevel.Player, dummy.AccessLevel);

            PlaytestConsole.Revoke();
            panel.OnResponse(state, new RelayInfo(20, [], [], [], []));
            Assert.Single(dummy.Backpack.Items);
        }
        finally
        {
            Disconnect(dummy, state);
            Disconnect(other, otherState);
        }
    }

    private static NetState Connect(PlayerMobile player, string username)
    {
        var state = PacketTestUtilities.CreateTestNetState();
        var account = new TestAccount(username);
        state.Account = account;
        state.Mobile = player;
        player.Account = account;
        player.NetState = state;
        player.AddItem(new Backpack());
        return state;
    }

    private static void Disconnect(PlayerMobile player, NetState state)
    {
        player.NetState = null;
        state.Mobile = null;
    }

    private sealed class TestAccount(string username) : MockAccount, IAccount
    {
        string IAccount.Username => username;
    }
}
