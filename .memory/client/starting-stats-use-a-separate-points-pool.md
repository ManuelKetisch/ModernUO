---
title: Starting stats use a separate points pool
feature: client
type: invariant
status: verified
verified: 2026-11-24
---

CrossUO Advanced character creation starts Strength, Dexterity, and Intelligence at their minimums and spends the remaining stat points independently of starting skills. ModernUO expects 10–60 per stat, totaling 90 on four-skill creation or 80 on three-skill creation. CrossUO preserves its earlier classic rules for older protocols.

- [Client allocation](../../../crossuo/src/Profession.h)
- [Client protocol limits](../../../crossuo/src/Gumps/GumpScreenSelectProfession.cpp)
- [Server validation](../../Projects/UOContent/Engines/Character%20Creation/CharacterCreation.cs)
