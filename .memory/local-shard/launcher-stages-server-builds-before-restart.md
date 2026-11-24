---
title: The launcher stages the server build before restarting
feature: local-shard
type: entrypoint
status: verified
verified: 2026-11-24
---

The Finder launcher builds into a staging directory before saving and stopping a running managed server. It installs binaries separately from configuration and saved worlds. Use a test copy for experiments that mutate game state.

- [Launcher implementation](../../../runtime/launcher/shard_launcher.py)
- [Build and startup guide](../../../README.md)
