# Example worker assignments

This example shows how to divide a request for pet leveling and breeding. It
does not authorize either feature.

- A research worker reads the two wiki pages and reports which breeding rules
  depend on level, generation, or spent ability points. It writes no code.
- The leveling worker owns the server-side progression and the focused tests.
  Its brief states the approved entry point, the saved state that breeding
  will read, and the first playable case.
- Once the leveling state is established, the breeding worker owns the
  eligibility and inheritance changes and their tests. If both changes need
  the same pet serializer, the orchestrator assigns it to only one worker
  at a time.
- An independent verifier exercises the integrated result, such as testing
  a qualifying pair and a rejected pair, then saving and reloading the
  qualifying parents or offspring in an isolated shard.

A client-only request such as an options gump or renderer regression goes to
the CrossUO route. A corroborating server worker is useful when wire behavior
or authoritative state is in scope.
