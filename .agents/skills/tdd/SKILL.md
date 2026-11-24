---
name: tdd
description: Use focused test-first development for gameplay rules, serialization, packet behavior, or regressions.
license: MIT
---

# Test-first development

Start with an observable rule from the feature brief. Pick the closest existing
test harness that reaches the rule through its normal entry point. Use fixed
inputs, independent expected values, and one behavior per assertion group.

1. Write the smallest failing test and confirm it fails for the intended reason.
2. Add the smallest valid implementation and re-run that same test.
3. Add only relevant boundary, compatibility, and rejection cases.
4. Refactor with the focused tests green. Review any persistence or protocol
   change through real saves or packets where the feature requires it.

A copied world verifies only the behavior it exercises. Record client, server,
and persistence evidence separately. See [build and test](../../../dev-docs/uoex-development/README.md#build-and-test).
