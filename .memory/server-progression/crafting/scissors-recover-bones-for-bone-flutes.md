---
title: Scissors recover bones for bone flutes
feature: server-progression
type: invariant
status: verified
verified: 2026-11-24
---

Ordinary scissors and the Salvage Bag share the armor cutting path. The five standard bone armor types yield half their Tailoring recipe's bone cost when crafted, and one bone otherwise. The yield is ordinary uncolored Bone. Protected items and other bone-like armor do not enter this rule.

A Bone Flute consumes 15 Bone and requires 80 Carpentry and 45 Musicianship. It uses the normal instrument mechanics, and the selected wood does not affect its recipe.

- [Armor scissoring](../../../Projects/UOContent/Items/Armor/BaseArmor.cs)
- [Carpentry recipe](../../../Projects/UOContent/Engines/Craft/DefCarpentry.cs)
- [Crafting tests](../../../Projects/UOContent.Tests/Tests/Engines/BoneCraftingTests.cs)
