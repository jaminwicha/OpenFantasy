# Named actor resource operations and restorative composition

## Scope

This increment implements the first executable portion of the selected world-composition discipline:
named immutable damage, capped additive restoration, exact MP expenditure and experience/reward
transitions over the existing single actor resource owner. Actual encounter damage and reward code
use those contracts. A reusable component planner composes one exact raw/crafted item dose with a
health change for self-use or a different recipient. No aftermath acknowledgment is required by that
component calculation.

The transition reports original/candidate actor state, operation, requested amount and actual HP/MP
values/capacities. It is transient evidence, not persisted/authenticated history. Full health rejects
the instantaneous remedy before publishing expenditure; failed/stale/overflowing composition retains
all original owners and exposes neither claim nor health effect. Existing progression/encounter
formats remain unchanged. See the [architecture contract](../../../Runtime/docs/architecture/actor-resources.md).

## Preserved work and qualification boundary

The unfinished care/material integration was preserved before this change at
`codex/checkpoint-before-resource-composition-20260926T002022Z` (`80cac15`). A subsequent WIP snapshot
`6125a62` preserves resource primitives, care integration and failed build/test evidence before an
isolated implementation delegation. Neither snapshot is an acceptance claim. The original
`codex/checkpoint-care-wip-before-motion-research-20260926T000024Z` remains intact.

The proposed implementation commit contains only the nine source/test files listed in
[isolated-source.json](qualification/isolated-source.json), over committed `13b3379`, plus its related
documentation and routing doctrine. Verification uses that isolated tree, excluding unfinished care,
geography and profile25 files. Three small repairs to the separate WIP restore buildability and the
actual source Potion field path; those files are not promoted with this domain increment.

## Tests and evidence

The new tests cover ordinary/incapacitated resource arithmetic, saturation at maximum integer values,
exact MP costs, no-effect/revision behavior, level gain and no-level rewards, source-backed raw/crafted
self/external treatment, material mass, stale selections, cross-scope refusal, full health, exhausted
revisions, and command reuse. Existing complete encounter and progression suites protect old behavior.

The isolated solution builds with zero warnings/errors. All **1,222 Core + 665 Hosting/client =
1,887 managed cases** pass, with zero failed/skipped. The [verification record](qualification/verification.json)
pins the exact source scope, 200 checked local links, unchanged frozen specs and prior milestones.
Results are retained in [qualification](qualification/) and [review](REVIEW.md).
The test-first build intentionally failed because the new methods/result type did not yet exist.
Subsequent source-backed tests exposed an incorrect assumed `effects.healHp` nesting; the retained
Potion record has a top-level `healHp` field. Content remains unchanged, and the test now reads it.
The initial sandbox test runner could not bind its local socket; this was an aborted run, not a pass.

## Remaining integration

Generic durable resource history, cause-specific admission and journal replay must replace the draft
care-specific last-attack supersession rule before general live healing is enabled. Every writer,
including XP rewards with unchanged resources, must participate. Treatment permission, delivery mode,
anatomical eligibility, geometry, action costs, encounter consequences and atomic persistence remain
complete-world responsibilities. The draft profile remains refused by durable genesis.

Required continuation: actual defeat -> selected recovery -> helper action -> renewed choice and
another attack, ordinary living-target treatment, raw/crafted provenance, adverse ordering, exact
retry, crashes/restart, and native restrained presentation. Passive recovery needs real exposure and
carry. No full lifecycle, native recovery, human art/motion, performance, platform or campaign gate
closes from this component increment.

The owner directs further three-pass cross-system composition analysis, a separate independent
challenge review, and implementation of accepted findings with tests. Bounded delegated coding and
analysis use `fable[1m]` with Max effort; Astra reviews coherent units. Pending worker output is not
included in this qualification. The delegation adapter's explicit effort capability is ecosystem
commit `54ab14a`; all 33 adapter tests pass and six installed script/reference copies match.
