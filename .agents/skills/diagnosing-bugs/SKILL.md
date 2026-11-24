---
name: diagnosing-bugs
description: Diagnose CrossUO and ModernUO failures or performance regressions through controlled local checks.
license: MIT
---

# Diagnose a failure

Reproduce the reported behavior through the nearest real entry point. Record
the triggering inputs, version or branch, observed outcome, expected outcome,
and test environment. If you only have a source lead, state that limitation.

Rank plausible causes. Say what each predicts and use the smallest observation
that distinguishes them. Change one factor at a time. Use bounded instrumentation
and keep captures local. For frame pacing, separate elapsed frame time, CPU
submission time, wait time, and the screen's refresh cadence.

Re-run the original scenario after any fix. Add a focused test when it can
catch the failure. Record what was observed and a durable fact if the result
helps future work. Follow the [shared development guide](../../../dev-docs/uoex-development/README.md)
for build and shard isolation.
