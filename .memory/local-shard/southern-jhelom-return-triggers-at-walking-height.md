---
title: Southern Jhelom return triggers at walking height
feature: local-shard
type: invariant
status: verified
verified: 2026-11-24
---

The southern Jhelom return pad at (1406, 3996) is walked at Z 6 on Trammel and Felucca. The other three departures trigger at Z 5. Generation uses the walking height, and shard startup moves matching legacy southern return triggers after the world loads. A disposable movement check exercises all eight routes across both facets.

- [Teleporter definitions](../../Distribution/Data/teleporters.json)
- [Existing-world alignment](../../Projects/UOContent/Commands/Object%20Creation/GenTeleporter.cs)
