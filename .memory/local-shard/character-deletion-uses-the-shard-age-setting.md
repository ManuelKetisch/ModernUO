---
title: Character deletion uses the shard age setting
feature: local-shard
type: invariant
status: verified
verified: 2026-11-24
---

Britannia Solo sets `accountHandler.characterDeletionDelay` to zero.
The game still requires confirmation, and the server still checks the
account slot, active character session, and jail. If a shard refuses
deletion, CrossUO keeps the refusal visible while refreshing its list.

- [Server deletion policy](../../Projects/UOContent/Accounting/AccountHandler.cs)
