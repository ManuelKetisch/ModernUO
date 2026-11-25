---
title: Eligible pets can pause XP at level 42
feature: pet-systems
type: invariant
status: verified
verified: 2026-11-24
---

Level 42 is the last level eligible for breeding. An owner can arm the
saved pause from NPC Info on a tameable pet with a maximum above 42 and
an eligible generation. It stops XP at 42 and can be switched off to
resume progress toward the pet's natural maximum.

- [Progression and pause rules](../../Projects/UOContent/Engines/PetProgression/BaseCreature.PetProgression.cs)
- [Parent eligibility](../../Projects/UOContent/Engines/PetBreeding/PetBreedingSystem.cs)
