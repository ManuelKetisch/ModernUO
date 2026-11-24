---
name: writing-for-agents
description: Author or revise UO agent guides, skills, orchestrator prompts, and scoped handoffs.
license: MIT
---

# Write agent guidance

Start from a real task. State when to read the guide, which files own rules and
evidence, and what action completes the work. Keep reusable instructions close
to their entry point and link specialized material only where it applies.

Keep the owner's decisions, current source, memory hints, and outside evidence
distinct. An instruction file cannot grant permissions. Name who can write
shared files and how an agent verifies a result. Keep handoffs short enough to
read in one pass.

Use canonical skills under `.agents/skills/`. Run the
[workspace checker](../../../dev-docs/uoex-development/README.md#workspace-checks)
after editing guides, memory, or skills. Walk a relevant request and a request
outside the trigger to check the routing.
