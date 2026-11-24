# Feature brief template

Use one short packet per feature. Fill the sections that affect implementation.

## Requested behavior

Name the feature, its first playable slice, and the owner instruction it
implements. Identify the expected entry point and the player-visible result.

## Reference and decisions

List Excelsior page paths, revision IDs or capture dates when known, and the
mechanics used. Record the fork's decisions and identify unresolved differences.
Keep design intent in the brief and durable operational facts in `.memory/`.

## Ownership and dependencies

Identify the owning fork, allowed write paths, test paths, and shared resources.
List any prerequisite slices and the integration order. Name the worker and the
independent verifier when assigning the brief.

## Acceptance checks

Specify a setup, action, and observable result for each important case. Include
edge cases and rejection behavior that matter to this feature. For stateful
changes, name the restart, save/reload, or existing-save check. Describe which
checks run through the real client and which run in an isolated host harness.

## Current status

State what is presently verified and what remains. Replace this section as work
progresses. Link to the commands, source, or reproducible evidence, and keep
private captures in a local scratch directory.
