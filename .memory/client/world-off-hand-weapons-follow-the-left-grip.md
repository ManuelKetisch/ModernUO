---
title: World off-hand weapons follow the left grip
feature: client
type: invariant
status: verified
verified: 2026-11-25
---

The world renderer places the secondary weapon at the left grip using the
character's facing and stance. Native left-arm movement carries that grip through
other poses. Rendering, selection and frame bounds use the same placement.
Standing katana poses use intact stock frames where the native right-hand
frame has occlusion cutouts. Each replacement follows the resting blade angle.
The far grip in the rear guard pose is layered behind the torso. A zoomed
CrossUO capture and draw trace checked all eight standing facings in each mode.

- [World animation placement](../../../crossuo/src/Managers/AnimationManager.cpp)
- [Frame capture controls](../../../crossuo/docs/ENVVARS.md)
