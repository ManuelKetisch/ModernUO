---
title: Trade storage preserves charged tool objects
feature: server-progression
type: invariant
status: verified
verified: 2026-11-24
---

Tool houses preserve tool identity and runic grade. A partial charge
withdrawal transfers uses into an existing compatible tool in the
backpack. Full withdrawal returns the stored object. Splitting uses does
not create additional sellable or recyclable tools.

- [Trade storage implementation](../../../Projects/UOContent/Items/TradeTools/TradeResourceStorage.cs)
