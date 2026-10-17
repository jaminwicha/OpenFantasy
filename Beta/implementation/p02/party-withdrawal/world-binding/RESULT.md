# Collective withdrawal: complete-world Core binding

Recorded 2026-10-17 against base `7f63b944`; exact inputs are retained in
[source pins](source-pins.json). This increment serves RQ-029, RQ-079 and RQ-099 and the
[shared P02 expedition](../../../CURRENT_ROADMAP.md). It implements the selected
[party-exit law](../../../PARTY_WITHDRAWAL_DECISION.md) in portable C#, with explicit versioning.
It is not activation in the ordinary adventure, SQLite migration or full P02 acceptance.

## Implemented outcome

Every participant chooses their own physical route. Due actors walk and pay independently; Full Wait
still holds for undecided humans, opponents act normally, and a complete side must reach clear contact
before the encounter ends. Arrival is an active encounter state. A blocked or cancelled route leaves
the person required and retains paid debt. Actual incapacity interrupts a choice without abandoning
the person or fabricating a victory. One command never authorizes another human's movement.

Profile41 selects the versioned collective retreat owner, canonical root/step codecs and explicit
cancellation action24. Legacy profiles retain their existing bytes, lone-side restriction and meanings.
The new root retains one canonical body, timing, resource and control owner. The nested retreat field
reserves512KiB within the unchanged16MiB complete-root budget; the component's128-actor bound is not a
supported game population or a measured worst-case complete root.

Accepted delegated walking retains its original command/witness after revocation; revocation prevents
new choices. The finite walking hold ends on arrival, allowing original-owner return while another ally
is still walking. Arrival does not refund readiness or grant an automatic Wait. Cancellation requires
current authority and cannot cancel an arrived actor's independently queued Wait.

Fresh admission is explicitly restricted to an unplayed profile40 genesis. It is **not** a migration:
no earned save is reconstructed with fresh resource history, no SQLite schema changed, and no host or
client capability selects41. Complete-world save continuity is the next integration dependency.

## Executable evidence

The new Hosting-assembly fixture uses retained adventure content and the Core transaction processor.
It approaches and assembles through real ground movement, enters and stages an actual encounter,
encodes/decodes each submitted batch, tracks occupied identities and restores canonical complete roots.
It does not use SQLite, network authentication or an elapsed host clock. Trusted controller inputs
in the managed control scenario are explicitly not proof of the real host allowance/reconnect path.

New complete-world scenarios establish:

- A lone queued choice cannot move an undecided ally or bypass Full Wait.
- Two independent routes include real opposing attacks, partial arrival, exact restoration at every
  boundary and identical replay from original fresh genesis, including the ordinary approach.
- One-, three- and four-human departures preserve the complete actor set, material custody, HP/MP at
  the terminal boundary, every actor's readiness debt and every captured exposure/carry field.
  Opponents explicitly Wait in those count variants; the two-person scenario proves attacks.
- A legal delayed route meets an earlier arrival's actual body. The blocked sample moves nobody,
  charges one sample, ends only its route and preserves the complete required side.
- Between-boundary cancellation preserves position/calendar/debt and grants no early action. Two
  cancellations in one batch retain both ended commands and the complete required roster.
- Foreign/stale requests fail. Complete-root admission rejects omitted allies, wrong speed or progress.
- Actual damage incapacitates a withdrawing member; the terminal cause is incapacity, the ally remains
  required, debt is unchanged by that attack, canonical restoration and ordinary combat continue.
- A granted/acquired route survives actual permission revocation with original attribution. Reconnect
  refuses the walking boundary and succeeds at arrival before the ally finishes. The old grantee then
  cannot cancel; the returned owner can. These are Core decisions, not transport or durable publication.
- Old/new codec mismatch and lived-world fresh-admission attempts reject.

Ten new Core codec cases cover two sides, terminal evidence, canonical round trips, bounded128-actor
routes, alias/scope/legacy conflicts, malformed bytes/pins/trailing data and exact failed-sample tariffs.
Existing domain cases remain in the full Core suite. Earlier `core-01` recorded1,913 passes; final
source regression and affected legacy/control cases are retained in `checks/` and summarized below.

Final source: **1,913 Core +22 focused Hosting-assembly cases pass, zero failures/skips**,1,935
distinct cases including23 new cases (10 codec and13 complete-world cases). Core completed in121.94s
under240s; the selected world/legacy/control run completed in33.51s under180s.
[Core command/results](checks/core-14.json) and [world command/results](checks/world-regression-15.json)
pin actual execution dependencies. [Final verification](verification.json) verifies2,217 source inputs
and both output dependency sets without assuming source equality implies binary identity.

## Failures and corrections

All earlier outputs remain in `checks/`; later passes do not erase them.

| Attempt | Evidence and disposition |
| --- | --- |
| `world-01` | Test API mistakes: coordinated decode overload, outcome member, spatial equality and inaccessible mutation helper. Corrected the fixture to actual APIs; no gameplay change. |
| `world-03` through `world-05` | Collision setup failed to reach an already-arrived blocker. The intermediate pass could show a walking collision only; the stronger arrival assertion stayed. |
| `collision-06` | Fixture referenced an internal spatial query. Replaced it with public ordinary movement over diagnostic immutable snapshots. |
| `collision-07` | xUnit adapter discovery failed before any scenario, with its unchanged60-second startup threshold. No test process remained on resumption. Direct discovery completed in0.78s; `collision-08` then executed and exposed the route-selection gap. The earlier startup failure's cause is unresolved. |
| `collision-08` | Shortest/L-shaped routes did not reach the requested arrived-body case. Added bounded legal, paid detours; full queue admission and actual timed movement still decide legality. `collision-09` passes through the normal adapter. |
| `world-10` | Eight passes; three new count variants compared `ExposureProgress` object identity rather than values. Corrected the assertion to its complete immutable captured state; no field was dropped. |
| `build-11` | The new incapacity fixture used an int literal as a uint sequence default. Focused compile caught it; changed to `0U`. |

`actors-12` passes the three count variants and actual incapacity; `control-13` passes revoked delegated
walking and owner return. No retry changes a game rule, acceptance threshold, time budget or assertion
about required arrival, membership, costs or authority. No independent critic or human acceptance is
claimed; orchestration remains paused by the owner.

## Remaining integration and full-scope obligations

Implement the explicit preserved-source40-to41 journal transition with original admission/history,
actual cuts/reopening, receipts, occupied IDs, pending owners and controller allowances intact. Prove
migration refusal at unsupported boundaries, crashes and original-genesis replay. Finish genuine-outcome,
opposing-side route, inherited-system and whole-root capacity coverage as part of that integration.
Then bind host scheduling, own-character observations/offers/cancel, compatible invitations and UI,
and continue the original earned shared adventure through departure/care/sale/return. Qualify durable
1–4-player controller/recovery behavior, rendered/native input and human Full Wait/usability. These
requirements remain open; no fresh-only substitute or removal of legacy `AllyRemains` is authorized.
The full181 requirements, production platforms/performance, art/audio and other P00–P14 exits remain.

## Documentation review

- **Plan/spec:** current roadmap, party-withdrawal decision and handoff retain the full cooperative
  expedition and explicit lived-save transition. Core evidence is separated from durable/native/human
  acceptance; frozen specification files remain untouched.
- **Reference:** updated party withdrawal, encounter timing/actions/retreat, effective control,
  persistence/history and the architecture index. They explain owners, immutable state, version/byte
  bounds, actual transitions, original attribution, Full Wait and extension boundaries in present tense.
- **Accuracy/completeness:** lead compared the actual admission/resolver/choice owners, codecs,
  identity/witness registration, controller return boundary and test assertions with those pages.
  Corrected the timing table's conflation of domain confirmation with world standing choices. The
  host/SQLite selection remains explicitly absent; independent architecture review remains unrun.
- **Checks:** source pins, bounded test commands/terminal results, whitespace and the maintained
  documentation check are retained with this packet. Mechanical links do not establish factual,
  independent, native or human approval. The maintained checker passes12 affected pages;
  [document-check output](checks/documentation-01.json) and final test outcomes are retained.

## Preservation

Before implementation, `codex/checkpoint-before-withdrawal-world-20261003T193348Z` preserved `7f63b944`.
[WIP checkpoint](WIP_CHECKPOINT.json) records the later temporary-index snapshot `3d81011a`, its exact
scope and unrun gates. Unrelated19 staged predecessor files retain their original index bytes; the
untracked October5 research addendum is outside this increment. The accepted Core marker is
`codex/milestone-party-withdrawal-core-20261017`, resolved to its qualified commit without switching
this checkout. The goal, full party feature and P02 remain open.
