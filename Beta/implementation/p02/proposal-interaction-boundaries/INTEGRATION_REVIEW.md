# Proposal interaction boundaries

Source: `71dcf41173ac74d7c4ae2f23a94dafde327a691a`; working branch `codex/runtime-foundation`.
Pre-change checkpoint and exact preservation are in [PRESERVATION.json](PRESERVATION.json).
Accepted at `codex/milestone-proposal-interaction-boundaries-20260929`: clean solution build and
165 distinct affected managed passes, including 34 focused cases and 11 new cases. The focused cases
are included in the regression total. [Final checks](qualification/FINAL_CHECKS.json) pin source/assembly
hashes, actual nonempty TRX results and unchanged 488 old test/fixture files, 23 frozen specifications
and the unrelated predecessor staging. No acceptance threshold changed.

## Implemented scope

- The public live proposal view provides bounded exact own-lot/recipient creation preparation. A
  positive explicit quantity selects a pinned raw/produced lot, full item definition, both participants/
  storages and a captured expiry. No owned inventory mutation, implicit target, reservation or second
  authority is introduced. Protected/reserved lots are filtered; no unrelated inventory is projected.
- `LocalWorldSession.InspectMaterialProposalAsync` selects one bounded inspection slot, uses only its
  fixed player/delivery and performs the actual durable lookup on a worker under existing serialization.
  Same pending identity shares a task; a different one is refused. There is no cache or automatic retry.
  Pause/reading inspection creates no advancing step. Stop waits for selected reading before disposal;
  backend failure faults both query and session. Archived inspection still requires full verified replay,
  which can delay simulation. This is not a latency/indexing qualification.
- Profile30 ordinary excavation now reaches its existing Core rule through the session. A real walk,
  earned effort, selected work and saved offer continue/reopen together. The audit confirms deferred
  structural excavation is a distinct profiles16–18 interpretation, excluded from the reservoir-sealed
  profile30 graph. A real pending16 cursor refuses proposal input without alteration; no fake combined
  pending state, numeric profile-range admission or Core/schema change is made.
- Timeless material proposal, runtime, presentation and excavation references are updated. The
  [next native slice](NEXT_NATIVE_SLICE.md) retains the explorer's concrete client file/API mapping.

## Review and failures retained

[Independent native Astra critique](CRITIC_REVIEW.md) strengthened the in-flight shutdown proof and
rechecked the resulting scheduling. No Fable request or model switch occurred: the earlier spend-limit
failure remains terminal, with no savings claim. Fable5.1/1M/Max remains the approved optional route.

The first build failed on missing test namespace imports and xUnit async/cancellation analyzers. The
new tests were corrected; no analyzer or deadline was disabled. The first focused run passed 31 and
failed 2 (33 total before the additional shutdown theory contrast). One deferred-context fixture reused the actor identity as a command; it now uses the fixture's
ordinary allocator. The archive damage test incorrectly assumed a concurrent SQL writer could mutate
an exclusively leased live database, and its finally assertion obscured that earlier failure. The
corrected test asserts SQL_BUSY while live, performs actual mutation only after disposal and requires
reopen refusal. A separate deliberate backend disposal proves session/query failure and no cached
answer. No live hardware/bit-rot fault injection is claimed. Original logs and source pins remain.

## Scope retained

No Godot client, asset, Core rule, codec, schema, old test, frozen specification or predecessor change.
New preparation is a Host seam, not a completed native pack/wheel. Opening an actual profile30 client,
explicit lot/quantity/offer selection, icons, controls and a fresh-process native gift journey remain
next. Existing wheel time continues while open; its captured-expiry matching must be handled explicitly,
not silently refreshed or compared against a freshly regenerated expiry every frame.

No full phase, control rollover, multiplayer authentication, retained520-dose acquisition/migration,
production throughput, graphical acceptance or human playtest gate closes here. The full campaign,
181 requirements and 14 full UI cases remain intact.
