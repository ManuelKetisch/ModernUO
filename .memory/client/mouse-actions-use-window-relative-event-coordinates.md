---
title: Mouse actions use window-relative event coordinates
feature: client
type: invariant
status: verified
verified: 2026-11-24
---

CrossUO reads motion, presses, releases, and scrolling from SDL's game-window events. Polling the latest desktop cursor during a Cocoa drag mixed coordinate times and discarded fractional deltas. X11 keeps a desktop fallback for missing captured motion, using one floating-point origin for the capture. A native replay reproduced missed movement and release drift before the fix and checks the dropped gump coordinates.

- [Mouse coordinates](../../../crossuo/src/Managers/MouseManager.cpp)
- [Window event handling](../../../crossuo/src/GameWindow.cpp)
