---
title: Weapon upgrade caps use effective values
feature: server-progression
type: invariant
status: verified
verified: 2026-11-24
---

Weapon Leveling quotes Luck and Lower Requirements using the effective
material-inclusive value. An accepted upgrade writes only the purchased
base increment, so resource bonuses still come from the material. The
existing item keeps any bonus already over the purchase cap.

- [Upgrade catalog](../../../Projects/UOContent/Engines/WeaponLeveling/WeaponUpgradeCatalog.cs)
- [Upgrade quote and commit](../../../Projects/UOContent/Engines/WeaponLeveling/BaseWeapon.Leveling.cs)
