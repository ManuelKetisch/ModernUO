---
title: Jhelom cemetery uses independent undead slots
feature: local-shard
type: invariant
status: verified
verified: 2026-11-24
---

The middle-island cemetery keeps its stock spawner and adds two bone knight, two skeletal mage, one wraith, two zombie, and two skeleton slots on each facet. The added spawners use independent 3–5 minute vacancy timers. The local Jhelom startup loader creates missing spawners by their saved identities.

- [Cemetery definitions](../../Distribution/Data/Spawns/shared/jhelom-cemetery.json)
- [Jhelom spawn provisioning](../../Projects/UOContent/Engines/Spawners/JhelomSpawns.cs)
