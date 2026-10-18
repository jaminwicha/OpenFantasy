# Process membership, absence and service contract

PLAN-STATE preparation from `c85c0124729000cb0532b19888ca4b91cf93b973`. This refines the
[process discovery contract](PLAN_STATE_PROCESS_DISCOVERY.md), [current-profile map](PLAN_STATE_CURRENT_PROFILE_MAP.md)
and [exit matrix](PLAN_STATE_EXIT_REVIEW.md). No new runtime owner, persisted index, schema, profile,
scheduler or migration is introduced. The [packet result](p02/partition-state/membership-cold-load/RESULT.md)
retains source pins and measurement evidence; independent review remains unrun.

## Source findings that determine the design

- `ConstructionOwnerDirectory.Capture` admits only profile5 and emits its complete owner set from
  actual admitted state. `StoredConstructionInterval` captures every actor plus timing/gathering/catalog
  and the selected work dependencies. `StoredConstructionPreparation` checks the same directory on
  every read/completion and verifies required payload hashes/lengths. Missing required data already
  fails. This is useful bounded membership evidence, not a current40/41 decomposition.
- `WorldOwnerArchiveReplay` verifies exact retained historical archive membership during complete
  replay. A selected payload hash alone cannot replace that proof or authenticate a different head.
- Current40/41 use the complete V35-family frame with required coordinated owners and explicit control,
  care, resource history, withdrawal and timing laws. Their seals/terrain-liquid interpretation excludes
  the profile9 transport owner (`WorldSnapshot` and `WorldTerrainLiquidBinding`). Consequently the joined
  profile9 transport witness cannot be promoted by attaching its component to a current world.
- Ordinary `WorldStepProcessor` applies actions before expiries, transport, regrowth, resident exposure,
  liquid flow and movement. Encounter/care/controller/consequence dispatches have distinct boundaries.
  The profile number is a semantic interpreter choice, not a set of independently optional features.

Do not add a generic wrapper around a complete `WorldSnapshot` and claim selective discovery. Do not
weaken supported-composition admission to make the two-domain contrast available in current40/41.
Richer support needs an explicit retained-source composition/rollout decision and its exact proofs.

## Membership evidence required by a partial reader

A future partial reader needs all of the following evidence together. These are design fields and
invariants, not newly implemented public classes or allocated byte tags.

| Evidence | Requirement and owner |
| --- | --- |
| Source identity | World, branch, selected historical head/ancestry, admission and semantic interpreter; realm where spatial ownership requires it. The existing serialized authority authenticates the source. |
| Complete eligible set | A profile-specific enumeration of process-owning facets and their stable owner keys/revisions. Each range/page proves coverage and bounds; individually valid leaves without complete coverage are insufficient. |
| Presence policy | For each legal facet: required, optional with authenticated absence, or forbidden by this interpretation. A required missing facet fails; an unknown kind/version fails before publication. |
| Owner reference | Exact immutable payload hash and envelope, bound to that complete membership statement. Duplicate keys, mixed worlds/branches, stale revisions and swapped payload roles fail. |
| Operation dependency closure | Required stock, tools, access, physical neighbours, control, history and timing for this interpreter/operation. A process key alone does not authorize execution. |
| Effective candidate | Selected actions and earlier process effects overlay the source in canonical execution order. Newly created/retired/changed dependencies update membership and invalidate discovery as needed. |
| Completion evidence | Every required occurrence is evaluated, legitimately suppressed by this interpreter, or rejected with the entire selected boundary. One publisher atomically commits time, owners, membership, identities and outcomes. |

For the existing typed prototype, the complete replay-admitted directory plus exact interval identity
remains the membership authority. Any later bounded directory/page design must establish equal
completeness and historical authenticity before replacing it. Storage residency never decides membership.

## Absence is not an empty list

Distinguish these results in both API behavior and evidence:

1. **Forbidden facet:** selected interpreter does not admit it. Reject a supplied facet; do not create
   an empty owner to make arbitrary combinations appear legal.
2. **Legally absent optional facet:** the authenticated complete admission says it is absent. No payload
   read is needed for that owner; the absence statement must come from the same head.
3. **Present, empty due set:** the exact owner and its dependency/clock evidence were available and
   discovery returned no applicable occurrence. This still proves that owner was considered.
4. **Present, evaluated but blocked:** transport retains its stage/funding; regrowth may lack finite
   unissued source stock. This is a domain outcome, not missing work or a promise of successful delivery.
5. **Present, lawfully suppressed:** the selected interpreter's hold/time rules suppress evaluation.
   Record the reason/clock boundary when reporting service; do not count a blocked outcome as a hold.
6. **Unknown/unavailable:** required membership, payload or dependent evidence is missing/stale. Fail
   preparation without advancing time or acknowledging commands. Do not return an empty set.

The profile5 Gathering directory entry can encode null, but required actor membership still comes
from the complete admitted source. A future optional-owner representation must preserve that distinction
explicitly rather than silently deleting a singleton key when its domain has no occurrences.

## Clock and age semantics

| Domain | Actual clock / completion evidence | Measurement rule |
| --- | --- | --- |
| Regrowth and effort | `GatheringSnapshot.AdvancingStep`, advanced using pacing process intervals where present | Compare required process boundary with the last completely evaluated owner boundary. A depleted node's old due date alone overstates missing service when finite stock prevented regrowth. |
| Reserved transport | Exact `ReservoirTransportState.Boundary`, pending stages and selected hold interpretation | Separate time since first scheduled delivery from missed evaluation. Repeated head/throughput/material blocking retains identity and is not undiscovered work. A boundary advanced during a lawful hold does not prove each stage was physically attempted. |
| Proposal / paid quote expiry | Advancing count after selected actions, coupled sequence/revision | Preserve action-before-expiry semantics and exact source books. Commit count and calendar milliseconds are not substitutes. |
| Detailed liquid flow | Calendar or pacing process boundary, selected interpreter and hold | Failed bounded flow aborts the entire boundary. No time-only success may conceal omitted flow. |
| Personal exposure | Original resident set and earned exploration/tactical exposure | All affected residents remain mandatory, including offscreen bystanders. There is no extra interval per preparation slice or NPC. |
| Optional NPC proposals | Eligibility, actual host selection and feasible commitment | Service age needs a defined eligibility episode; a person's age, project revision or last command is not a lawful substitute. No persisted fairness claim exists yet. |
| Accepted excavation continuation | Original accepted command and reserved continuation | Intermediate work does not earn the terminal interval; durable continuation is distinct from disposable owner-read preparation. |

Report mandatory evaluation lag separately per timing domain, optional proposal service age separately,
and preparation wall latency separately. Do not take a maximum across unlike units. A complete atomic
boundary may establish no omitted mandatory work for that boundary; it does not establish a general
service guarantee or a world-scale oldest-age statistic. If the authenticated set/eligibility episode
is unavailable, report the metric unavailable, not zero. General persisted scheduling remains open.

## Selection, yielding and publication

Capture one complete selected batch under the existing serialization boundary. Dependency reads may
yield, but the published head and clocks remain unchanged until all required evidence and effects are
ready. Recheck source identity before every resumed read and final publication. A foreign/stale handle,
changed membership, revoked control, changed readiness or a failed read cannot resume as though current.
Apply the existing cancellation/reselection policy; do not automatically replay the old command.

After actions or earlier processes change an owner, discover against the effective candidate and
validate downstream cohorts again. A source-authenticated pre-action list is insufficient. Final
publication uses the existing SQLite writer and historical interpreter; no sidecar queue may advance
independently. Full admission fallback remains explicit where a bounded cohort cannot prove legality.
Its cost must appear in load/stall measurements, rather than being hidden behind a successful read slice.

## Executable acceptance and next decision

The next partial-discovery implementation must use an admitted composition that actually contains its
chosen owners. Decide the current-profile ownership/absence mapping and preserved-source transition
before extending schema14; do not allocate a new format by incrementally adding optional fields.
Reuse existing discovery consumers and sole-writer machinery. Required contrasts are:

- missing versus authenticated-absent versus present-empty owner; duplicate, foreign, stale and
  retired membership; unchanged payload under changed head; complete page coverage if paging is added;
- post-action cancellation or newly created obligation; earlier-process dependency changes; deterministic
  results across lawful load orders; optional proposal exclusions distinct from mandatory effects;
- held, blocked and non-time boundaries; actual process/tactical threshold and all-resident exposure;
- exact history and before/after fault recovery for membership, changed owners, time and outcomes;
  original current40/41 bytes/rights/private and historical facts remain intact on any supported upgrade.

An unused generic enumerator is not this deliverable. Existing legacy tests are inherited only at their
actual dependency pins. No required cold/scale/platform, migration, fairness, independent or P02 gate closes
from this contract. NPC-01 remains the next new inhabited behavior after PLAN-STATE decision/review.

## Documentation review

- **Plan/spec:** resolves membership/absence, supported composition and service-age semantics without
  adding a format or changing phase order; preserves all original PLAN-STATE and P02 obligations.
- **Reference:** the timeless scheduling reference receives the corresponding design invariants,
  explicitly separate from existing complete-owner and legacy interval bindings.
- **Accuracy/completeness:** source-checked directory capture/read/replay, ordinary process ordering,
  current frame requirements and the prohibition on profile9 transport in the sealed current composition.
- **Checks:** [packet evidence](p02/partition-state/membership-cold-load/RESULT.md); no implementation of
  this future partial index or independent acceptance is claimed.
