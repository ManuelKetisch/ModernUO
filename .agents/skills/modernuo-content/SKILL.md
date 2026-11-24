---
name: modernuo-content
description: Route C# work on ModernUO items, loot, creatures, commands, skills, and saved gameplay state.
---

# ModernUO content

Read [ModernUO's guide](../../../CLAUDE.md) and its
[domain skill catalog](../../../dev-docs/claude-skills/). Use the content,
serialization, commands, gump, or era guide only where the task needs it.
Preserve the existing authorization for changes to `Projects/Server/`. Game
state runs on the main loop.

Prefer `Projects/UOContent/` entry points for shard rules and content. Use the
existing serializer or source generator and check compatibility with older
saved entities. Exercise item consumption, transferred quantities, and the
normal server validation path. See the [local build and test recipe](../../../dev-docs/uoex-development/README.md#build-and-test).
