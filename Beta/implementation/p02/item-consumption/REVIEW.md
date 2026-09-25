# Item consumption review and failure disposition

An independent native critic reviewed the new material candidates, shared production validation,
raw/produced rights, authority participants, restoration, identity collisions, V1 compatibility and
the complete-world rejection boundary. No external model was invoked.

The review found one actionable invariant gap: the planner derived the consumption identity from
its command, but the claim constructor admitted an arbitrary nonaliasing identity. The constructor
now enforces `EntityId.DeriveSpawned(commandId, 0)`, matching production batch identity ownership.
A codec regression replaces only that identity in an otherwise valid archive and requires rejection
for the deterministic-identity violation. Synthetic boundary fixtures now use valid derived IDs.
The same critic checked the exact correction and the new architecture reference; no actionable
issue remained in that bounded follow-up. The critic ran no builds or tests.

The critic also identified an important scope limitation: finite raw-source capacity and disjoint
claims do not establish complete issued coverage. A standalone archive can omit a raw claim and
leave a structurally admissible unallocated gap. This is explicitly documented, rather than presented
as authenticated history. The future care profile must join issuance owners and preserve append-only
claims through actual commands/replay. Produced budgets do require exact complete coverage. The
current complete-world guard prevents either interpretation from silently entering existing saves.

## Build failures

- `qualification/build-01.log`: one compiler error in the newly written golden test, a missing
  `OpenFantasy.Core.Custody` import. The test import was corrected.
- `qualification/build-02.log`: 21 compiler errors from the new codec tests calling internal
  `CanonicalEncodingV1`. Test-local independent frame/number helpers replace those calls; runtime
  visibility is not widened for tests.

Both failed builds remain retained. `build-03.log` passes with zero warnings/errors. The subsequent
focused 115 Core/4 source-supply checks and full 1,200 Core/649 Hosting-client regressions all pass,
with zero failed/skipped. Four independent golden byte/hash vectors match actual candidates, and
both independently malformed archives reject. Old independent V1 fixtures reproduce exactly.

No test threshold or runtime rule was relaxed. Production and identity changes receive the full
managed regression because their shared validators also serve existing crafting, construction and
world profiles. Native presentation and live treatment are unchanged; this packet makes no new
rendered, crash-recovery, device, throughput or complete-care acceptance claim.
