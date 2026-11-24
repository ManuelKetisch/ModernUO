---
title: The macOS display cap applies during ordinary auto-login
feature: client
type: invariant
status: verified
verified: 2026-11-24
---

Ordinary launch and auto-login use macOS display sync or display-rate timing. The explicit XUO_AUTO_LOGIN benchmark environment variable bypasses frame pacing.

- [CrossUO loop and startup](../../../crossuo/src/Main.cpp)
- [Diagnostic controls](../../../crossuo/docs/ENVVARS.md)
