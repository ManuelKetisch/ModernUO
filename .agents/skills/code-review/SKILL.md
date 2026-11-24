---
name: code-review
description: Review a scoped UO diff against its requirements and current client, server, and runtime behavior.
license: MIT
---

# Review a change

Read the applicable fork guide and feature packet. Confirm branch and worktree
state. Review the requested diff and relevant new files. Preserve unrelated
changes and verify each suspected problem in source or a focused test.

Prioritize behavior regressions, existing-save compatibility, authority and
input boundaries, packet ordering, thread ownership, and incomplete tests.
For gameplay, trace the normal player interaction to the authoritative change.
For UI, check the actual gump and existing input gestures. Distinguish unverified
behavior from proven defects.

An independent review uses a fresh worker that did not author the change.
Report actionable findings with the file, line, trigger, and consequence.
If no defects are found, state the review scope and any remaining verification.
