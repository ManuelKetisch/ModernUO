---
name: improve-codebase-architecture
description: Assess or carry out a requested refactor of a bounded ModernUO or CrossUO subsystem.
license: MIT
---

# Assess architecture

Start with a specific failure, cost, or recurring change. Trace callers, state,
and boundaries in current source. Verify candidate hotspots with focused tests,
profiles, or traces where possible. For broad navigation, use the available
code search or language server and confirm results in the owning source.

Identify the smallest seam that improves the verified problem. Preserve gameplay,
saved state, wire compatibility, and renderer draw order where applicable.
Document the tradeoff and how to measure it. Keep architectural advice within
the requested scope and implement only changes the owner authorized.

Use the [feature brief](../../../dev-docs/uoex-development/FEATURE-BRIEF.md) for work
that needs more than one slice, then [review](../code-review/SKILL.md) the result.
