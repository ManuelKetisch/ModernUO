---
title: Excelsior grades preserve existing materials
feature: server-progression
type: invariant
status: verified
verified: 2026-11-24
---

The Excelsior catalog appends resource IDs and provides explicit refined
variants for shared grades. Ordinary gear keeps its existing stats and
saved grade. Extra boards and logs have their own item bases, so ordinary
wood recipes only use them when the matching grade is selected.

- [Resource registry](../../../Projects/UOContent/Misc/ResourceInfo.cs)
- [Refinery](../../../Projects/UOContent/Engines/Metallurgy/ExcelsiorRefinery.cs)
