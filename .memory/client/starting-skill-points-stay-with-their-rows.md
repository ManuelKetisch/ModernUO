---
title: Starting skill points stay with their rows
feature: client
type: invariant
status: verified
verified: 2026-11-24
---

CrossUO Advanced character creation stores points on the profession draft. Changing a row's skill name keeps its points. Editing a row changes that row and the remaining pool. The three-slot protocol has 100 points and the four-slot protocol has 120, with up to 50 per row. A row needs a skill name only if it has points. The creation packet appends unnamed zero-point slots after selected skills so that the Alchemy placeholder does not precede a real Alchemy allocation.

- [Client allocation](../../../crossuo/src/Profession.h)
- [Client character creation](../../../crossuo/src/Gumps/GumpScreenSelectProfession.cpp)
- [Client packet encoding](../../../crossuo/src/Network/Packets.cpp)
- [Server validation](../../Projects/UOContent/Engines/Character%20Creation/CharacterCreation.cs)
