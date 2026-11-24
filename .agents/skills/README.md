# Britannia Solo skill catalog

Each skill lives in `.agents/skills/<name>/SKILL.md`. Claude Code's
`.claude/skills/` links point here. Agents can also read the skill directly.

| Skill | Use it for |
| --- | --- |
| [diagnosing-bugs](diagnosing-bugs/SKILL.md) | Reproduce a failure or measure a bottleneck |
| [tdd](tdd/SKILL.md) | Test-first implementation and regression checks |
| [code-review](code-review/SKILL.md) | Review an implementation against its feature brief |
| [writing-for-agents](writing-for-agents/SKILL.md) | Write agent guides, skills, prompts, and handoffs |
| [improve-codebase-architecture](improve-codebase-architecture/SKILL.md) | Assess or carry out a requested refactor |
| [research-and-think](research-and-think/SKILL.md) | Verify a dependency, platform detail, or Excelsior source |
| [autonomous-backlog](autonomous-backlog/SKILL.md) | Scope and sequence feature slices |
| [plain-language-ui-copy](plain-language-ui-copy/SKILL.md) | Write player-facing text and feedback |
| [asd-ste100-explanations](asd-ste100-explanations/SKILL.md) | Explain technical work and procedures clearly |
| [modernuo-content](modernuo-content/SKILL.md) | Route server gameplay and persistence changes |
| [crossuo-client](crossuo-client/SKILL.md) | Route client interaction, rendering, and protocol changes |

The first nine adapt Equanimity's workflows to C#, C++, and local shard testing.
The paired workspace also has a local shortcut to the
[full Equanimity skill library](../../../equanimity-skills/README.md).
Use its product-specific skills in their owning project.
User-level tools such as advisory-board, goldfish-review, and ECC remain
available through hosts where they are installed.

ModernUO's original [domain guides](../../dev-docs/claude-skills/) cover
serialization, game content, commands, gumps, networking, threading, and more.

Update the catalog when you add a skill. Run the
[workspace checker](../../dev-docs/uoex-development/README.md#workspace-checks)
with `--sync-skills` to create missing Claude Code mirror links. The checker's
normal mode verifies the catalog, frontmatter, and mirrors.
