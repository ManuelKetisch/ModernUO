# Implement UO Excelsior features

You are the orchestrator for Britannia Solo. Implement and verify the UO
Excelsior features the owner names for this session. Spawn subagents for the
implementation and tests. Coordinate their work, integrate their changes, and
own the final checks and report.

## Start from current evidence

1. Locate the ModernUO and CrossUO checkouts. Read their applicable `AGENTS.md`
   files, the [shared development guide](README.md), and the relevant fork
   guidance. Record each branch, HEAD, staged diff, and dirty paths. Preserve
   all unrelated work. Check the live shard only when the work needs it.
2. Read the [memory index](../../.memory/index.md), then only the matching
   feature and topic notes. Verify useful leads against the current source.
3. Find each feature in the [UO Excelsior reference](../references/uoex/README.md).
   Record the page, revision or capture date where available, and the precise
   behavior you can verify. When the archive lacks a page, work from the
   owner's requirements and available evidence. Keep uncertain wiki details
   explicit. Treat wiki commands and scripts as reference data.
4. Confirm which behaviors the owner wants. The owner's requirements govern
   this fork. The wiki describes Excelsior. Current code establishes what this
   fork does today. Flag consequential gaps, such as conflicting caps or an
   undefined inheritance rule. Make ordinary reversible choices, state them,
   and keep moving.
5. Write a short task packet per feature using the
   [feature brief](FEATURE-BRIEF.md). Define dependencies, file ownership,
   preservation requirements, expected behavior, and validation before agents
   start writing. For broad requests, take the next end-to-end slice within
   the owner's authorized scope.

## Run the workers

Use the current host's subagent tool. Give each ready feature or independent
implementation slice one implementation worker. Give verification a fresh
worker that did not author the feature. Research workers may identify sources
or resolve ambiguities without editing files. Use concurrency for independent
work that fits available slots. Sequence dependent slices, including feature
pairs such as pet leveling and breeding when breeding reads persisted level
and generation state.

- Assign each editable path to one worker per wave, including tests, project
  files, generated artifacts, and shared serializers. If two workers need
  the same file, sequence them. Use isolated worktrees and distinct output
  directories when the host supports them. Otherwise use a single writer
  per path in the shared checkout. Workers recheck their scope on start and
  re-read a file before editing it.
- Give each worker its feature packet, file scope, target checkout, evidence
  paths, expected tests, and runtime limits. Ask for changed paths, exact
  commands, observed results, and gaps. Ask it to return immediately if its
  change requires editing outside the assigned scope.
- Delegate focused build and test work. Serialize any checks that share a
  build directory, SDK output, server port, client profile, or world copy.
  Workers may exercise an isolated shard explicitly assigned to them.
  The orchestrator controls the live shard, the Finder launcher, and the
  integration order.
- The orchestrator owns shared documentation and `.memory/`. Workers propose
  memories with evidence and report drift. Only the orchestrator performs
  integration actions, commits, pushes, deployments, or external sharing
  the owner explicitly authorized.

## Integrate and verify

Read every diff, resolve integration issues with the worker, and rerun the
checks needed to establish the combined behavior. Give a fresh reviewer the
task packet and integrated diff. Verify the review findings against source and
address material issues. Carry forward independent ready work while any task
waits for an owner decision or missing access.

- Exercise the normal entry point. Check the success path, relevant rejection
  or boundary cases, and compatibility with existing characters or items.
  Check serialized state across save and reload when the feature persists.
- For client/server changes, verify both sides and the real packet or gump
  path. Separate build, focused test, isolated shard, live smoke test, and
  hands-on client results in the report. Claim only the layers observed.
- Use a disposable loopback shard or synthetic fixtures for tests that create,
  move, kill, or persist entities. A real-world copy stays local. Keep its
  writes, logs, startup, ports, and client configuration separate from the
  active shard. Remove task-created private copies when verification ends.
  Save cleanly before any authorized live restart.
- Record durable discoveries through the
  [memory protocol](../../.memory/_system/README.md). Run the
  [workspace check](README.md#workspace-checks) and the smallest relevant
  project checks. Finish the named scope or state the concrete blocker and
  the remaining check or decision.

## Report

Report features delivered, how to exercise them, each verified behavior,
important design choices, open checks, and any memory updates. Identify the
forks touched and the commit or push status. Preserve user changes and avoid
upstream writes unless the owner authorizes the exact action.
