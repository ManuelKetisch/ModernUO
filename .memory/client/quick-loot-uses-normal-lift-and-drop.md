---
title: Corpse quick loot uses the normal lift and drop path
feature: client
type: invariant
status: verified
verified: 2026-11-24
---

CrossUO issues the existing lift and drop requests for quick loot. ModernUO validates the item move and owns the resulting inventory state.

- [CrossUO loot request](../../../crossuo/src/CrossUO.cpp)
- [ModernUO item handlers](../../Projects/UOContent/Network/Packets/IncomingItemPackets.cs)
