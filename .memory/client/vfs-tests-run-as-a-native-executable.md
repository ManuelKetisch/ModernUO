---
title: CrossUO VFS tests run as a native executable
feature: client
type: entrypoint
status: verified
verified: 2026-11-24
---

CrossUO's asset-streaming suite is a standalone CMake executable named
`vfs_tests`. Build that target and run it directly for VFS changes.

- [VFS test definition](../../../crossuo/tests/vfs/CMakeLists.txt)
- [CrossUO build and test workflow](../../dev-docs/uoex-development/README.md#build-and-test)
