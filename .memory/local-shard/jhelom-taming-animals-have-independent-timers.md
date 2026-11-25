---
title: Jhelom taming animals have independent timers
feature: local-shard
type: invariant
status: verified
verified: 2026-11-24
---

The eastern pen on northern Jhelom has 12 bulls, 12 horses, 12 goats, 8 brown bears, and 8 great harts per facet. The separate northern pen has 12 goats, 12 sheep, and 12 cows per facet. Each is managed by a separate one-slot stock spawner with a 2–3 minute timer. Taming unlinks the animal from its spawn point, and a wild death also frees the slot. Startup adds missing spawn points by their saved identities so existing herds survive restarts.

- [Spawn definitions](../../Distribution/Data/Spawns/shared/jhelom-taming-pit.json)
- [Runtime provisioning](../../Projects/UOContent/Engines/Spawners/JhelomSpawns.cs)
