---
title: Armor double-click uses equipment checks
feature: server-progression
type: invariant
status: verified
verified: 2026-11-24
---

Double-clicking armor inside the player's backpack uses the normal equip
checks. A movable armor piece in the same slot goes into the backpack when
the new piece equips. A failed equip restores the worn armor.

- [Armor item use](../../Projects/UOContent/Items/Armor/BaseArmor.cs)
- [Equipment interaction tests](../../Projects/UOContent.Tests/Tests/Mobiles/EquipmentInteractionTests.cs)
