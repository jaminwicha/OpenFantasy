# Construction site reuse inside the world authority

Reusable construction geometry now runs inside the ordinary `WorldStepProcessor` and existing
SQLite authority. This makes the actual selected-batch executor consume the shared qualification;
it does not publish detached planner results or introduce a second work scheduler. Construction
still uses current inventory, tools, project revisions, funding, command authority and historical
identity occupancy. Existing time/order, complete physical admission and transactional publication
remain unchanged.

The processor's planner owns one synchronized `ConstructionSiteCache`. On each placement it checks
the running project and complete current scene. Changed terrain, actor/body/position, structure,
support or access dependencies discard the prior result and run ordinary geometry again. A previously
blocked site cannot remain blocked after its dependencies change. Each new command-derived structure
identity is checked, and successful placement still admits a full spatial candidate. Cache contents
are detached metadata, never world/scene/terrain/index references or stored material state.

The public standalone planner retains uncached geometry. The internal processor test seam selects
that same ordinary adapter for differential checks; only the Core test assembly is made a friend.
No production feature flag, codec/profile/schema change, source-content migration or new launch route
is introduced. The predecessor compile boundary remains `src/**/*.cs`; the successor editor exclusion
remains `Beta/.gdignore`.

## Verification and measured scope

Seven new Core cases and two new Hosting cases cover:

- All 200 funded steps through actual world selection, comparing complete canonical world bytes,
  receipt bytes and identity reservations with uncached execution. Every resident earns exactly one
  existing exposure increment per advancing step. Successful work suppresses selected builder movement;
  completion retires stock and produces the installed reserved output and exact offcuts.
- A lower-ordered actor opens dialogue before construction. Work refuses while that already-selected
  step still contributes time. Closing dialogue on the held boundary earns neither work nor time;
  a subsequent advancing step resumes work with qualified geometry.
- Movement, excavation and an arriving blocker invalidate geometry. A cached blockage is requalified
  after the blocker leaves. Stale tool/control/batch state and a final historical identity collision
  remain refusals with unchanged construction/material state.
- Weak-reference collection proves a long-lived processor's cache releases the source world, scene,
  identity index, resulting world and terrain. A new equivalent admitted world can reuse its metadata.
- The actual profile40 adventure earns its construction funding. Repeated host previews produce the
  same candidate without changing durable work or creating receipts. Before/after-commit interruption
  recovers exactly the old or complete new state, retained receipt and historical root. An exact retry
  never earns a second step. Existing final-completion fault/reopening tests exercise the same cache
  through the ordinary publisher.

`check-02/core` passes **465** cases in 23.158 seconds including build. It covers Construction, Spatial,
Structures, step behavior/custody, pacing, admission and consented material. `check-02/hosting` passes
**15** cases in 18.562 seconds including build: preview/recovery, actual construction completion and
final faults, structural carriage, complete-root/current-adventure layout and occupancy history.
That is **480 distinct passes, nine new**, with no skips. The initial six-case focused run also passes;
it overlaps the final selection and is not added again. Each command had the unchanged 180-second
bound, an owned process group and observed terminal result. Builds report no warnings or errors.

The paired 200-decision diagnostic in the affected run allocates 21,026,768 bytes uncached versus
17,284,728 with reuse (**17.8% lower cumulative managed allocations**); it captures once and reuses
199 times. Elapsed samples are 220.416 versus 211.038 ms. The smaller focused run records 20,984,256
versus 17,262,912 bytes and 74.358 versus 65.019 ms. These are one workload on this macOS arm64/.NET8
host, with alternating execution order, not statistical timing, peak-residency, disk-I/O or frontier
performance acceptance. Full scene creation and root encoding still cost work; no universal speedup
is claimed. The raw TRX output retains both samples.

There are no failed managed checks in this increment. Earlier full-Core timeouts and native reliability
failures remain open and are not diagnosed by these selected passes. Full-Core, independent critique,
new rendered play, human acceptance and full phase/platform/scale gates are unrun here.

## Remaining integrated proof

This is real authority integration for reusable geometry, but **the authority still holds and encodes
the complete world**. The detached preparation's low-level advancing/held flags are not upgraded into
permission; runtime work uses the ordinary selected batch and live running owners instead. The
source-free detached candidate API remains a domain experiment.

PLAN-STATE still needs admitted typed owner inputs, earned due scheduling with partial residency,
stable actor/custodian ownership, actual cross-owner cargo transfer, atomic owner-directory/history
publication and the entire absent-detail episode. The next implementation should join those owners
and time with the existing publisher, not become an extended cache-tuning project. Preserve the
current access-population law and the explicit unsupported detached material-context boundaries.
No permanent NPC persistence model or new save format is selected by this increment.

## Preservation

Checkpoint `codex/checkpoint-before-construction-authority-20261017` resolves to
`d5c3160222cfc0f98251233879b15b7c266c11c0`, parent `25085494`, retaining concurrent language-document
work as WIP for recovery only. [CHECKPOINT.json](CHECKPOINT.json) records the exact scope and protected
hashes. Accepted marker: `codex/milestone-construction-authority-20261017`. Work remains on
`codex/runtime-foundation`; no branch switch, push or merge. Unrelated predecessor staging, solution
changes, language-assessment edits, research inputs and historical spec/evidence remain outside this
commit.

## Documentation review

- **Plan/spec:** Roadmap, handoff, comparison and prototype contract now distinguish real selected-batch/publisher use of reusable geometry from still-required partially resident owner execution. All PLAN-STATE and P00–P14 gates remain at their original scope.
- **Reference:** Construction execution explains the cache lifetime, exact invalidation, live action ordering, held versus advancing time, complete scene/physical admission, preview semantics and SQLite recovery. Materials/activities describes qualification reuse without implying repeated route searches or permission from a cache.
- **Accuracy/completeness:** Lead traced the placement adapter through the live kernel, `DecideImmediate`, action rollback/reconciliation, time/movement order, host preview, serialized commit and historical reopening. Tests compare full canonical outputs and counterexamples. Cached facts own no time/material/receipt; full-root residency and encoding remain explicit. Independent critique is unrun under the owner's orchestration pause.
- **Checks:** VERIFICATION.json records actual command/TRX outcomes, source/binary pins, protected work and documentation/whitespace checks. Link validation supplements this source-based review and does not substitute for semantic or independent acceptance.
