---
title: BaseWeapon owns weapon leveling state
feature: server-progression
type: invariant
status: verified
verified: 2026-11-24
---

BaseWeapon version 12 adds optional progression data to the weapon. Versions
through 11 load without activation. Activated weapons run from level 1 to 100.
The cumulative threshold at level n is 100 times (n squared minus one), with
a ceiling of 999,900 XP. Convert between weapon types through the shared
progression copy, which keeps XP, spent points and boost history.

- [Weapon save fields](../../../Projects/UOContent/Items/Weapons/BaseWeapon.cs)
- [Weapon progression](../../../Projects/UOContent/Engines/WeaponLeveling/BaseWeapon.Leveling.cs)
- [Levelable Weapons reference](https://www.uoex.net/wiki/Levelable_Weapons)
