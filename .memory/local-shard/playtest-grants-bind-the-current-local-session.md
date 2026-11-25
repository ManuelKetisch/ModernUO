---
title: Playtest grants bind the current local session
feature: local-shard
type: invariant
status: verified
verified: 2026-11-24
---

The Playtest window needs a server-console grant for the separate ordinary
playtest account on a loopback-only Solo shard. Every action checks the
same account object, character, live connection, and grant identity. A
reconnect or expiry invalidates the grant. The account's normal bank wallet
holds test funds independently from the usual player account.

- [Setup and operation](../../dev-docs/uoex-development/PLAYTEST.md)
- [Authorization](../../Projects/UOContent/Engines/Playtesting/PlaytestConsole.cs)
