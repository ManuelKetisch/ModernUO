# Memory protocol

Each note records one durable claim and its evidence. Keep the index and folder
READMEs as routers. A note is a hint until its source is checked. The owner's
accepted requirements establish intent, and the current code establishes
implementation.

## Read and maintain

1. Read `index.md`, choose the feature, and follow its README to the relevant notes.
2. Verify claims against the linked source, command, or reproducible result.
3. Add one note per new invariant, ownership rule, decision, gotcha, or tested
   workflow. Correct a superseded claim with the reason and a reference to its
   replacement. Use `_archive/` when retired material still explains a decision.
4. Update the adjacent router and add a topic folder when three or more notes
   share a specific topic. The feature router links that topic's README.
5. Run the [workspace check](../../dev-docs/uoex-development/README.md#workspace-checks).
   Report whether memory changed. During a swarm, the orchestrator writes
   shared memory, and workers supply evidence.

Use lowercase kebab-case feature, topic, and note names. Limit the index to 200
lines, routers to 80 lines, and note bodies to 60 lines. Store edited notes under
`.memory/<feature>/` or `.memory/<feature>/<topic>/`. Use the
[note template](memory-file-template.md).

Keep credentials, account or character details, save files, private captures,
and raw logs outside memory and Git. Link to engineering evidence and record
current constraints, not a narrative of a session. Mark unverified leads
explicitly. The checker validates structure, links, and metadata, while source
inspection or testing establishes whether the claim is still true.
