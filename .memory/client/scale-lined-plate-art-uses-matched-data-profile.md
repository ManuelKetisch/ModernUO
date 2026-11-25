---
title: Scale-lined plate art uses a matched data profile
feature: client
type: invariant
status: verified
verified: 2026-11-24
---

The isolated art profile gives the hybrid plate its own static artwork and
paperdoll images. CrossUO and ModernUO use matching tile metadata for graphics
0x7FAC and 0x7FAD. Saved chests return to stock plate orientations when loaded
without those records. World movement uses the stock plate animation alias.

- [Profile builder](../../dev-docs/uoex-development/build_plate_art_profile.py)
- [Appearance selection](../../Projects/UOContent/Engines/HybridForge/HybridForgeItems.cs)
