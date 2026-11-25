---
title: Dual wield keeps independent swing times
feature: server-progression
type: invariant
status: verified
verified: 2026-11-24
---

The off-hand weapon uses its own cooldown and range checks. Acquiring a target
starts its timer with a half-cycle offset when the off hand is ready. The main
hand keeps the standard combat timer and owns queued special moves. A main-hand
cooldown can block main-hand attacks while the off hand continues to deal damage.

- [Player equipment and off-hand combat](../../Projects/UOContent/Mobiles/PlayerMobile.Equipment.cs)
- [Weapon equip and combat](../../Projects/UOContent/Items/Weapons/BaseWeapon.cs)
- [Equipment interaction tests](../../Projects/UOContent.Tests/Tests/Mobiles/EquipmentInteractionTests.cs)
