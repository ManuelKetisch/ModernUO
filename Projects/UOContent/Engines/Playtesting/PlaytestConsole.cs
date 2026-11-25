using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.Playtesting;

public static class PlaytestConsole
{
    internal const string AccountName = "playtest";
    private static readonly TimeSpan _sessionDuration = TimeSpan.FromMinutes(45);
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(PlaytestConsole));
    private static PlaytestGrant _grant;
    private static TimerExecutionToken _expiry;

    public static void Initialize()
    {
        if (!SoloProgression.Enabled || !ServerConfiguration.GetSetting("playtest.enabled", true) ||
            !HasLoopbackListeners())
        {
            return;
        }

        ConsoleInputHandler.RegisterCommand("playtest", "Manage the local playtest account and session.",
            input => Core.LoopContext.Post(() => HandleConsoleCommand(input)));
        // Unannotated instance handlers are omitted from the ordinary HelpInfo list.
        CommandSystem.Register("Playtest", AccessLevel.Player, new ChatHandler().Open);
    }

    private static bool HasLoopbackListeners()
    {
        var listeners = ServerConfiguration.Listeners;
        if (listeners.Count == 0)
        {
            return false;
        }

        foreach (var listener in listeners)
        {
            if (!IPAddress.IsLoopback(listener.Address))
            {
                return false;
            }
        }

        return true;
    }

    private static void HandleConsoleCommand(string input)
    {
        input = input?.Trim() ?? string.Empty;
        if (input.Equals("provision", StringComparison.OrdinalIgnoreCase))
        {
            ProvisionAccount();
            return;
        }

        if (input.Equals("revoke", StringComparison.OrdinalIgnoreCase))
        {
            Revoke();
            _logger.Information("Playtest access revoked.");
            return;
        }

        const string prefix = "grant ";
        if (!input.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            _logger.Information("Use playtest provision, playtest grant <character name>, or playtest revoke.");
            return;
        }

        var name = input[prefix.Length..].Trim();
        foreach (var connection in NetState.Instances)
        {
            if (connection.Mobile is PlayerMobile player &&
                string.Equals(player.Name, name, StringComparison.OrdinalIgnoreCase) &&
                GrantSession(player, connection, Core.Now) != null)
            {
                _logger.Information("Playtest access granted to {Character} for 45 minutes.", player.Name);
                player.SendMessage("Your playtest session is ready. Enter [Playtest.");
                return;
            }
        }

        _logger.Warning("Sign into the playtest account and enter the exact name of its active character.");
    }

    internal static PlaytestGrant GrantSession(PlayerMobile player, NetState connection, DateTime now)
    {
        if (!PlaytestGrant.IsEligible(player, connection))
        {
            return null;
        }

        Revoke();
        var grant = new PlaytestGrant(player, connection, player.Account, now + _sessionDuration);
        _grant = grant;
        Timer.StartTimer(_sessionDuration, Revoke, out _expiry);
        return grant;
    }

    internal static void Revoke()
    {
        var grant = _grant;
        _grant = null;
        _expiry.Cancel();
        if (grant != null && grant.Player.NetState == grant.Connection)
        {
            grant.Player.CloseGump<PlaytestGump>();
        }
    }

    private static void ProvisionAccount()
    {
        if (Accounts.GetAccount(AccountName) != null)
        {
            _logger.Information("The playtest account already exists. Its credentials were left as they are.");
            return;
        }

        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
        var path = Path.Combine(Path.GetTempPath(), $"britannia-playtest-{Path.GetRandomFileName()}.txt");
        var fileCreated = false;
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None
            };

            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using (var writer = new StreamWriter(path, options))
            {
                fileCreated = true;
                writer.WriteLine(AccountName);
                writer.WriteLine(password);
            }

            _ = new Account(AccountName, password);
            World.Save();
            _logger.Information("Playtest login details written to {CredentialPath}.", path);
        }
        catch (Exception error)
        {
            if (fileCreated && Accounts.GetAccount(AccountName) == null)
            {
                File.Delete(path);
            }

            _logger.Error("Playtest account setup failed with {ErrorType}.", error.GetType().Name);
        }
    }

    internal static bool MayUse(PlaytestGrant grant, NetState sender) =>
        grant != null && ReferenceEquals(grant, _grant) && grant.Authorizes(sender, Core.Now);

    private sealed class ChatHandler
    {
        public void Open(CommandEventArgs args)
        {
            var grant = _grant;
            if (args.Mobile is PlayerMobile player && ReferenceEquals(grant?.Player, player) &&
                player.NetState is { } connection && MayUse(grant, connection))
            {
                player.SendGump(new PlaytestGump(grant));
            }
        }
    }
}

internal sealed record PlaytestGrant(PlayerMobile Player, NetState Connection, IAccount Account, DateTime ExpiresAt)
{
    internal static bool IsEligible(PlayerMobile player, NetState connection) =>
        player is { Alive: true, Deleted: false, AccessLevel: AccessLevel.Player } &&
        connection?.Mobile == player && player.NetState == connection &&
        player.Account is { AccessLevel: AccessLevel.Player } account && connection.Account == account &&
        string.Equals(account.Username, PlaytestConsole.AccountName, StringComparison.OrdinalIgnoreCase) &&
        connection.Address != null && IPAddress.IsLoopback(connection.Address);

    internal bool Authorizes(NetState sender, DateTime now) =>
        now < ExpiresAt && sender == Connection && Player.Account == Account && IsEligible(Player, sender);
}

internal static class PlaytestActions
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(PlaytestActions));
    private const int MaximumCheck = 1_000_000;

    internal static bool TryIssueCheck(PlaytestGrant grant, NetState sender, string input)
    {
        if (!PlaytestConsole.MayUse(grant, sender) ||
            !int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var amount) ||
            amount is < 1 or > MaximumCheck)
        {
            return false;
        }

        if (!TryGiveItem(grant, sender, () => new BankCheck(amount)))
        {
            return false;
        }

        _logger.Information("Playtest check issued for {Amount} gold to {Character}.", amount, grant.Player.Name);
        return true;
    }

    internal static bool TrySetStats(PlaytestGrant grant, NetState sender, string strength, string dexterity, string intelligence)
    {
        if (!PlaytestConsole.MayUse(grant, sender) || !TryParseStat(strength, out var str) ||
            !TryParseStat(dexterity, out var dex) || !TryParseStat(intelligence, out var intel))
        {
            return false;
        }

        var player = grant.Player;
        player.StatCap = Math.Max(player.StatCap, 3 * Math.Max(str, Math.Max(dex, intel)));
        player.RawStr = str;
        player.RawDex = dex;
        player.RawInt = intel;
        _logger.Information("Playtest base stats set for {Character} to {Str}/{Dex}/{Int}.", player.Name, str, dex, intel);
        return true;
    }

    internal static bool TrySetSkill(PlaytestGrant grant, NetState sender, string skillName, string input)
    {
        if (!PlaytestConsole.MayUse(grant, sender) ||
            !decimal.TryParse(input, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) ||
            value < 0 || value > SoloProgression.MaxSkill || decimal.Round(value, 1) != value)
        {
            return false;
        }

        var player = grant.Player;
        for (var i = 0; i < player.Skills.Length; i++)
        {
            var skill = player.Skills[i];
            if (!string.Equals(skill.Info.Name, skillName?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fixedValue = (int)(value * 10);
            skill.CapFixedPoint = Math.Max(fixedValue, skill.CapFixedPoint);
            skill.BaseFixedPoint = fixedValue;
            _logger.Information("Playtest skill {Skill} set to {Value} for {Character}.", skill.Info.Name, value, player.Name);
            return true;
        }

        return false;
    }

    internal static bool TryGiveItem(PlaytestGrant grant, NetState sender, Func<Item> create)
    {
        if (!PlaytestConsole.MayUse(grant, sender) || grant.Player.Backpack is not { } pack)
        {
            return false;
        }

        var item = create();
        if (!pack.TryDropItem(grant.Player, item, true))
        {
            item.Delete();
            return false;
        }

        _logger.Information("Playtest item {ItemType} issued to {Character}.", item.GetType().Name, grant.Player.Name);
        return true;
    }

    private static bool TryParseStat(string input, out int value) =>
        int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
        value is >= 10 and <= SoloProgression.MaxStat;
}
