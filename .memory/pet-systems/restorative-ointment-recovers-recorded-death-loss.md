---
title: Restorative ointment recovers recorded death loss
feature: pet-systems
type: invariant
status: verified
verified: 2026-11-24
---

The bonded pet's serialized death-loss ledger records lost permanent stats.
Restorative ointment recovers that recorded loss up to the applicable
training cap or the pet's previous value, whichever is higher. The separate
mixed pet ointment system applies resource-specific stat boosts.

- [Death-loss ledger](../../Projects/UOContent/Engines/PetProgression/BaseCreature.PetCare.cs)
- [Pet care items](../../Projects/UOContent/Engines/PetProgression/PetCare.cs)
