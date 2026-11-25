using System;
using Server.Commands;
using Server.Network;

namespace Server.Assistants;

public static class AssistantCommands
{
    public static void Configure()
    {
        CommandSystem.Register("Assistants", AccessLevel.GameMaster, Assistants_OnCommand);
    }

    [Usage("Assistants [allow <feature> | disallow <feature> | resend]")]
    [Description("Inspects assistant negotiation status and connected clients, or updates disallowed assistant features.")]
    private static void Assistants_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var args = e.Arguments;

        if (args.Length == 0)
        {
            ShowStatus(from);
            return;
        }

        if (from.AccessLevel < AccessLevel.Administrator)
        {
            from.SendMessage("Only Administrators can modify assistant negotiation settings.");
            return;
        }

        var action = args[0];

        if (action.InsensitiveEquals("resend"))
        {
            var count = 0;
            var disallowed = AssistantConfiguration.Settings?.DisallowedFeatures ?? AssistantFeatures.None;

            foreach (var ns in NetState.Instances)
            {
                if (!ns.Running || ns.Mobile == null)
                {
                    continue;
                }

                ns.SendAssistVersionReq();
                ns.SendAssistHandshake(disallowed);
                count++;
            }

            from.SendMessage($"Sent assistant version request and handshake to {count} connected client(s).");
            return;
        }

        if ((action.InsensitiveEquals("allow") || action.InsensitiveEquals("disallow")) && args.Length >= 2)
        {
            if (!Enum.TryParse<AssistantFeatures>(args[1], true, out var feature))
            {
                from.SendMessage($"Unknown assistant feature '{args[1]}'.");
                return;
            }

            var disallow = action.InsensitiveEquals("disallow");
            AssistantConfiguration.SetDisallowed(feature, disallow);
            AssistantHandler.BroadcastDisallowedFeatures();

            var settings = AssistantConfiguration.Settings ?? AssistantConfiguration.CreateDefaultSettings();
            var verb = disallow ? "Disallowed" : "Allowed";
            from.SendMessage($"{verb} assistant feature {feature}. Current disallowed mask: {settings.DisallowedFeatures}.");
            return;
        }

        from.SendMessage("Usage: [Assistants [allow <feature> | disallow <feature> | resend]");
    }

    private static void ShowStatus(Mobile from)
    {
        var settings = AssistantConfiguration.Settings ?? AssistantConfiguration.CreateDefaultSettings();
        var negotiation = AssistantHandler.Enabled ? "Enabled" : "Disabled";

        from.SendMessage(
            $"Assistant Negotiation: {negotiation} | Disallowed: {settings.DisallowedFeatures} | Warn: {settings.WarnOnFailure} | Kick: {settings.KickOnFailure} ({settings.DisconnectDelay.TotalSeconds:F0}s)"
        );

        var shown = 0;
        foreach (var ns in NetState.Instances)
        {
            if (!ns.Running || ns.Mobile == null)
            {
                continue;
            }

            var assistant = ns.Assistant ?? "None";
            var state = AssistantHandler.GetHandshakeState(ns.Mobile);
            from.SendMessage($"  {ns.Mobile.Name} ({ns}): Assistant={assistant}, Handshake={state}");
            shown++;
        }

        if (shown == 0)
        {
            from.SendMessage("  No connected players.");
        }
    }
}
