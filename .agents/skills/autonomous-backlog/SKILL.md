---
name: autonomous-backlog
description: Scope or sequence UO feature work so an orchestrator can assign and verify independent slices.
---

# Scope independent feature work

Use one concise [feature brief](../../../dev-docs/uoex-development/FEATURE-BRIEF.md)
per feature or independently checkable slice. Name the current requirement,
edit scope, dependencies, expected behavior, and smallest meaningful evidence.
Keep the current status short. Replace it on each update.

Parallelize slices with disjoint writes and independent resources. Sequence
shared files, persistence boundaries, and protocol changes. Keep one
orchestrator as the writer of shared decisions, status, and memory. Record
blocked causes and what would unblock them. Finish an authorized playable
slice before expanding the scope.
