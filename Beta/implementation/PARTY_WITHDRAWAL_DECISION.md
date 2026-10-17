# Party withdrawal: integration decision and delivery plan

Recorded 2026-10-03; Core evidence updated 2026-10-17. This supplements RQ-029, RQ-079 and RQ-099 and the P02 cooperative expedition
in the [current roadmap](CURRENT_ROADMAP.md). The frozen full spec and its gates remain intact.
Status: component, profile41 Core and explicit copied40→41 SQLite transition are implemented;
[world binding](p02/party-withdrawal/world-binding/RESULT.md), [save transition](p02/party-withdrawal/save-transition/RESULT.md)
and [durable recovery](p02/party-withdrawal/durable-recovery/RESULT.md) retain their bounded evidence.
Host/client selection, presentation, remaining capacity/interaction proofs and native/human qualification
remain open. Opening a save never activates the new rule implicitly.

## Player outcome and selected direction

Players withdraw through real ground on their own readiness boundaries. Each controls their own
choice and can see which party members still need to choose or reach safety. Enemies can act between
samples; no real-time decision deadline is added. The fight ends only when the complete withdrawing
side can lawfully leave. A leader's click never moves another human, discards a downed ally, clears
everyone's debt or fabricates a victory.

Use **independent actor commitments with a collective departure condition**. Keep required side
membership separate from current choices. This supports multiple simultaneous routes, interruption
of one route, independent cancellation and restoration without erasing obligations to other actors.
It also leaves body, clock, condition and permissions with their existing canonical owners.

Do not merely remove `AllyRemains` from retreat v2. Its continuation owns one route and its resolver
ends the entire episode when that actor clears contact. Reusing that terminal behavior for a party
would abandon allies and cancel their fight. Do not teleport the party after unanimous voting, add
a wall-clock escape timer, convert other humans to NPCs, or reinterpret saved v2 commands.

The [architecture reference](../Runtime/docs/architecture/party-withdrawal.md) distinguishes the domain
component from its Core command admission and complete-candidate publication. Neither is a storage
commit or an authenticated connected-client path by itself.

## Resolver contracts before runtime activation

The implementation must resolve these against executable scenarios before runtime activation:

| Boundary | Required behavior / resolution |
| --- | --- |
| Arrival before allies | Retain the actor, position and debt in the active episode. Start with ordinary explicit decisions at subsequent due boundaries; there is no automatic free Wait. Evaluate an explicit interruptible guard choice only if native play shows repeated decisions are burdensome. That would be a separate retained action, not an implicit scheduling bypass. |
| Cancellation | End only that actor's commitment; preserve membership, position and paid debt. Specify admission at a held tactical boundary without advancing time or allowing an extra action before readiness. A new combat choice must not coexist with an incompatible standing route. |
| Incapacity | Do not filter the person out. Interrupt the affected route and keep the remaining party in the episode. Healing, carrying, rescue, abandonment or surrender need their own lawful actions/outcomes; none may be silently invented by retreat. P02 validation must include the clear refusal and continued playable combat/recovery path. |
| Collision | Revalidate each actual due sample in deterministic actor order against current bodies and the complete root. A route may interrupt when another body blocks it. No body swapping, reservation-based teleport or stale advisory placement. |
| Opponents | Preserve ordinary choices, damage, range and timing. Departure requires every withdrawing member actually beyond contact under the admitted law. Multi-side encounters must not be terminated by a two-side assumption. Refuse unsupported scope explicitly until a participant-exit law is available. |
| Control changes | A finite accepted walking obligation retains original attribution. At its safe completion boundary, original-owner return must not be blocked forever by another person's unfinished route. Arrival bookkeeping does not authorize new inputs or an endless delegated guard. |
| Defeat or other outcome | Cancel incompatible commitments with retained command identities, then apply the existing genuine outcome/care contract. Do not turn an actual defeat into a successful departure. |
| Full Wait | All still-required undecided humans hold world/tactical advancement. Disconnect/controller recovery uses existing explicit policies. No deadline, forced vote or synthetic human choice. |

This table preserves broader rescue, recruitment, evolving participation and mixed-clock obligations;
it does not claim they are implemented or reduce the full specification to the simplest party fixture.

## Versioning and lived-save continuity

Source review selects an **explicit new complete-world interpretation with a versioned retreat
facet**, retaining one retreat owner. This refines the earlier candidate of changing only the nested
component. The Core binding selects profile41, retreat codecV2 and cancellation action codec24.
No SQLite schema or lived transition is selected merely by these format additions.

The reasons are concrete. `WorldSnapshotCodecV35` encodes profiles 35–40 with the legacy retreat
codec, its 4 KiB field bound and its reserved maximum. `WorldSnapshot.WithState` preserves the retreat
rules pin. `PlayerWorldCompatibility` compares profile and original admission but carries no separate
withdrawal interpretation. Reinterpreting the same profile and admission would leave that check unable
to distinguish the new law. Root selection and the nested facet must agree; structural decoding alone
cannot grant new permissions. Do not introduce another simulation or duplicate position/readiness.

The [persistence reference](../Runtime/docs/architecture/persistence-and-history.md#interpretation-and-admission-identity)
explains the underlying contracts. The [source review](p02/party-withdrawal/continuity-review/RESULT.md)
records the investigation and its limits.

| Source constraint | Required implementation |
| --- | --- |
| Resource history and coordinated anchors retain `AdmissionVersion` | Preserve original admission lineage. Do not call a fresh-admission helper or recreate resource history over earned progression. The new profile, rules pin and typed transition identify the changed law separately. |
| Proposal transition pins a lived head and preserves admission and time | Reuse its publication discipline, not its 27→30 request/processor. Withdrawal requires its own exact source/target transition; opening a save never selects it implicitly. |
| Schema12 recovery admits only the explicit40→41 journal transition, once, with original admission and genesis retained | Preserve the exact source/target dispatcher and historical replay; the SQL kind list alone never authorizes another migration. Older schema and migration meanings remain distinct. |
| Joint anchors validate capability continuity; older resource-only anchors also pin profile | Audit the actual coordinated profile40 lineage before/after cuts. Never weaken another archive family's admission or treat all anchors as interchangeable. |
| The complete root is bounded at 16 MiB | Reconcile nested read bounds, encoder reservations and worst-case complete roots. A 200 KiB standalone party bound is not proof of root headroom; retain the total budget. |
| Accepted control recognizes the legacy active command; safe owner return depends on its hold | Extend retained-command admission and controller return together. Arrival bookkeeping must not indefinitely retain delegated control. |
| Host/client routes use closed profile sets | Cover batch codecs, scheduling, offers, observations, invitation compatibility and original-genesis reopening. A decodable root is not a working connection. |

Use a quiet exploration boundary for the initial transition: no active/outcome encounter, legacy
retreat continuation or pending excavation. Refuse an in-flight source instead of cancelling work.
This transition precondition does not restrict ordinary later combat or saving active withdrawal.
Retain the legacy terminal record, receipts and identities. Establish how live controller allowances
and pending material/care work survive the copied operational rows before publication; never clear
them to make an upgrade eligible. A temporarily unsupported boundary must have an explicit refusal
and a lawful route to eligibility through ordinary play, without losing the save.

Bind the request to world/branch, exact head revision/hash, advancing steps, original admission,
existing anchor, selected rules and a historically unused cause. Publish one nonadvancing transition
through a preserved staging copy and replay from original genesis before selecting the destination.
Determine whether a joint cut is necessary from actual owner changes: the proposal transition needs
one to change material/consent interpretation; copying that cut automatically for retreat is not
justified. Prove subsequent ordinary cuts and historical reads whichever route is selected.

Before activation, prove unchanged old snapshot/action/step bytes and replay, explicit new pins,
incompatible-reader refusal, complete-state budgets and protocol compatibility. Inventory, actors,
construction, history and the earned adventure save retain identity and state. A fresh-world-only
implementation does not complete this continuity requirement.

Do not activate a new default admission or update the client capability handshake until the complete
world can restore, replay and reject unsupported/mismatched interpretations correctly. The original
party refusal remains active while this work is incomplete.

## Coherent implementation increments

1. **Commitment representation — implemented, bounded checks recorded.** Immutable complete-side
   membership; independent exact commands, routes and progress; canonical restore and corruption
   rejection. This is a retained component for the intended core, not a second simulation or host.
2. **Complete-world transaction and continuity — in progress.** The Core binds actor-command
   authority, due-order paid movement, interruption/cancellation, collective exit and controller-hold
   semantics, with explicit codecs. Bounded managed scenarios cover 1–4 humans, blocked movement
   after an ally arrives, actual damage/incapacity, and revoked delegated walking with safe owner
   return before the other route finishes. Two-person original-genesis replay restores every walked
   boundary. These are Core proofs, not SQLite or network approval. Complete remaining genuine-outcome,
   inherited-system and whole-root capacity cases with the explicit durable admission/history
   transition. Include the old earned adventure save, actual cuts, controller allowances and restart
   across intermediate movement/arrival/failed-sample boundaries, with no rule/budget waiver.
   The [saved-world transition](p02/party-withdrawal/save-transition/RESULT.md) supplies explicit
   copied40→41 publication and original-genesis replay of the earned shared opening, preserving
   controller allowances and prior receipts. The [durable recovery increment](p02/party-withdrawal/durable-recovery/RESULT.md)
   proves ordinary inherited/subsequent cuts, pending material proposals and paid movement/cancellation/
   departure recovery, including six actual storage-process kills. Pending aftermath care belongs to
   an unresolved outcome and must refuse migration until ordinary resolution makes the save eligible;
   it is not an exploration-state facet to silently migrate or clear. The subsequent interaction checks
   [qualify this distinction, genuine outcome and blocked-route recovery](p02/party-withdrawal/interaction-closure/RESULT.md). Complete-root capacity and
   host/client/native integration remain distinct obligations; ordinary launcher selection is unchanged.
3. **Host and presentation.** Own-character withdrawal offers and inspection show actual path/cost,
   party readiness and blockers without exposing another actor's private route or command. Use the
   existing wheel, confirmation/cancellation and immutable observations. Arrival, interruption and
   declined/changed choices need clear feedback. UI never supplies implicit consent or authority.
4. **Joined journey and qualification.** The ordinary two-player opening continues into the same
   saved expedition: buy/carry, gather to enter, each chooses withdrawal, genuine enemy turns occur,
   physical departure retains debts and injuries, care/sale/return follow. Then test 1–4 participants,
   restart/cut/reconnect/control changes and representative latency. Inspect rendered captures and
   obtain human usability/Full Wait acceptance. Required independent architectural critique remains
   unrun while owner steering prohibits orchestration; do not fabricate or waive it.

## Validation and rejection cases

Every required actor must be represented, including downed actors. A subset, outsider, duplicate,
foreign world/branch/episode, reused command, stale component/actor/timing revision, wrong original
controller or missing permission must fail before changing any coupled state. Every prefix and exit
must agree with actual bodies, roots and readiness. Interrupted motion pays its attempted boundary;
cancelled future motion gains no distance and refunds no past cost. Terminal records and archived
history preserve all original actions and cancelled choices.

Round-trip and malformed-data checks are necessary but do not prove saved-world integration. Keep
separate positive/counterexample tests for domain structure, complete-world admission, persistence,
authenticated host behavior and native/human play. Use existing runners and one coherent journey;
do not replace this plan with a series of unrelated profile or UI micro-checkpoints.
