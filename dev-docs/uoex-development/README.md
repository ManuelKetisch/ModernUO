# Britannia Solo development

Start each UO Excelsior implementation session by pasting the
[orchestrator prompt](ORCHESTRATOR.md) and naming the feature or features you
want. See the [worker assignment example](WORKER-EXAMPLE.md) for a dependent
feature pair. Start from the `ultima-online-local` folder when a task needs both
forks. Use the [feature brief](FEATURE-BRIEF.md) to scope each assignment.

## Where to work

| Area | Entry point |
| --- | --- |
| Rules, items, creature loot, progression, serialized state | ModernUO [`Projects/UOContent/`](../../Projects/UOContent/) and its [guide](../../CLAUDE.md) |
| Server engine | ModernUO [`Projects/Server/`](../../Projects/Server/). Get the owner's approval for engine edits per the fork guide. |
| Rendering, input, gumps, client settings | CrossUO [`src/`](../../../crossuo/src/) and its [guide](../../../crossuo/AGENTS.md) in the paired checkout |
| Excelsior evidence | [Reference index](../references/uoex/README.md) |
| Durable shared knowledge | [Memory index](../../.memory/index.md) |
| Task guidance | [Skills](../../.agents/skills/README.md) |

The parent folder is the paired workspace. ModernUO owns these shared docs and
skill sources so they travel with its fork. The client can also be built on its
own. In the paired workspace, root and CrossUO entry points route here.
Use the parent's [launcher guide](../../../README.md) for interactive play,
server saves, and restart commands.

## Workspace checks

From this folder or any working directory, run the checker by path.

```sh
python3 /path/to/ultima-online-local/ModernUO/dev-docs/uoex-development/check_workspace.py
```

It checks the memory shape and routes, seed evidence paths, native skill
manifest and links, and paired entry points when both checkouts are present.
Run it with `--status` to show each fork's current Git worktree status.
Use `--changed` to find memory notes that cite edited worktree files.
On a fresh ModernUO checkout, run it with `--sync-skills` once to rebuild the
local Claude Code mirrors from the canonical skill files.
The check uses standard Python 3.9 or later and Git and makes no network requests.

## Build and test

**ModernUO.** Use the SDK and NuGet cache installed under the workspace's
`runtime/` folder. For gameplay logic, start with a filter in
[`UOContent.Tests`](../../Projects/UOContent.Tests/). For an authorized engine
change, use [`Server.Tests`](../../Projects/Server.Tests/) as applicable.
Run from the paired workspace root.

```sh
uo_root="$PWD"
uo_build="$(mktemp -d)"
cd "$uo_root/ModernUO"
DOTNET_ROOT="$uo_root/runtime/dotnet" \
DOTNET_CLI_HOME="$uo_root/runtime/dotnet-home" \
NUGET_PACKAGES="$uo_root/runtime/nuget" \
DOTNET_CLI_TELEMETRY_OPTOUT=1 \
"$uo_root/runtime/dotnet/dotnet" test Projects/UOContent.Tests/UOContent.Tests.csproj \
  -c Release -r osx-arm64 --no-restore "-p:OutDir=$uo_build/" "-p:SolutionDir=$PWD/" \
  --filter 'FullyQualifiedName~SoloProgressionTests'
```

Substitute the relevant test filter. The example uses a checkout with restored
package assets. The output directory isolates binaries. Test runs that share
the same checkout can still write the same `obj` folders.

**CrossUO.** Run from the paired workspace root.

```sh
cmake --build runtime/crossuo-build --target crossuo --parallel 7
```

Use an independently configured build directory for a worktree. A client
build checks compilation. For asset-streaming changes, build the
`vfs_tests` target and run `runtime/crossuo-build/tests/vfs/vfs_tests`
directly. The current client build has no tests registered with CTest.
For input and gumps, exercise the actual UI path. For renderer work, capture
the backend, display mode, workload, and frame-time evidence with
[CrossUO's diagnostic toggles](../../../crossuo/docs/ENVVARS.md).

**Shard integration.** Use a disposable copy for save or gameplay experiments.
Keep the data paths and listeners local, choose an unused loopback game port,
and give the client a separate profile and settings directory. Check which
account and save directory the test instance uses before a mutation. Save
before any authorized live restart through the existing launcher. Raw captures,
credentials, profile data, world saves, and game assets stay local.

## Equanimity patterns

The shared setup carries over selective memory routing, explicit scope and
acceptance checks, independent review, an orchestrator-owned integration
process, and manual preflight checks. The [skill catalog](../../.agents/skills/README.md)
lists the portable adaptations and provides access to the full Equanimity
library from the paired workspace.
