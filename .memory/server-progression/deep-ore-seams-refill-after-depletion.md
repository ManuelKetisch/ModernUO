---
title: Deep ore seams refill after depletion
feature: server-progression
type: invariant
status: verified
verified: 2026-11-24
---

Fire in Trammel has a volcanic glass seam and Shame in Trammel has a meteoric
iron seam. Ordinary mining tools can target both. Each seam saves its
remaining portions and replenishes 25 minutes after its fourth portion.
Specialty recipes use the new materials without altering ordinary ore yields.

- [Deep ore seam](../../Projects/UOContent/Engines/Metallurgy/DeepOreSeam.cs)
- [Metallurgy recipes](../../Projects/UOContent/Engines/Metallurgy/Metallurgy.cs)
