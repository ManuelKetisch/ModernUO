---
title: The FPS counter position and size are saved with the character desktop
feature: client
type: invariant
status: verified
verified: 2026-11-24
---

CrossUO's FPS-only overlay uses the normal gump drag, resize, and character desktop save paths. The developer-mode setting controls visibility. Layouts without a saved position start beside the game viewport. The counter fits inside the current window for drawing and hit tests. Only a completed drag updates its stored position, so a smaller startup window cannot overwrite the saved coordinates. Layouts that store a position without a size use the original text size.

- [Counter window](../../../crossuo/src/Gumps/GumpFpsCounter.cpp)
- [Desktop layout read and write](../../../crossuo/src/Managers/GumpManager.cpp)
