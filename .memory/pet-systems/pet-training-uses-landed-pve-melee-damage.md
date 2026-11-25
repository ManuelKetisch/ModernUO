---
title: Pet training uses landed PvE melee damage
feature: pet-systems
type: invariant
status: verified
verified: 2026-11-24
---

BaseWeapon measures actual health lost to a qualifying non-ranged hit.
Progress uses the pet's damage across the encounter, within a fixed budget
based on the opponent's maximum health. Sufficient contribution can earn a
defeat bonus. Use NPC Info to check progress and preview spending points.
The BaseCreature serializer preserves progression and founder traits.

- [Weapon hit path](../../Projects/UOContent/Items/Weapons/BaseWeapon.cs)
- [Progression rules](../../Projects/UOContent/Engines/PetProgression/BaseCreature.PetProgression.cs)
- [Encounter accounting](../../Projects/UOContent/Engines/PetProgression/BaseCreature.PetTrainingCombat.cs)
