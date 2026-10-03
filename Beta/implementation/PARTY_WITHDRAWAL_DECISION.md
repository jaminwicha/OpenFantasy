# Party withdrawal: integration decision and delivery plan

Recorded 2026-10-03. This supplements RQ-029, RQ-079 and RQ-099 and the P02 cooperative expedition
in the [current roadmap](CURRENT_ROADMAP.md). The frozen full spec and its gates remain intact.
Status: component contract implemented; runtime law, migration, presentation and native/human
qualification remain open. This is not approval to activate a new rule in an existing save.

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

The [standalone component reference](../Runtime/docs/architecture/party-withdrawal.md) records the
implemented model. It contains neither actor authorization nor a complete-world publication path.

## Decisions to settle in the resolver increment

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

The preferred investigation is explicit versioning of the retreat owner/component inside the complete
world, rather than another broad profile solely to add a route collection. This is a candidate, not a
verified compatibility conclusion. `WorldSnapshotCodecV35` currently dispatches directly to the old
retreat codec, validates its field bound and reserves its maximum size; all three must be reconciled.
Structural decode alone must never expand an existing saved law's permissions.

Before selecting that approach, prove unchanged old snapshot/action/step bytes and replay, explicit
new interpretation/rules pins, forward refusal by incompatible readers, complete-state size budgets,
protocol compatibility and an explicit journaled transition for lived worlds. Inventory, actors,
construction, history and the earned adventure save must retain identity and state. If the current
profile contract genuinely requires a new version, document that exact reason and implement the
minimum explicit transition; do not accept a fresh-world-only shortcut as completed continuity.

Do not activate a new default admission or update the client capability handshake until the complete
world can restore, replay and reject unsupported/mismatched interpretations correctly. The original
party refusal remains active while this work is incomplete.

## Coherent implementation increments

1. **Commitment representation — implemented, bounded checks recorded.** Immutable complete-side
   membership; independent exact commands, routes and progress; canonical restore and corruption
   rejection. This is a retained component for the intended core, not a second simulation or host.
2. **Complete-world transaction and continuity — next.** Wire the component to authenticated action
   admission, due-order resolver, interruption/cancellation, collective exit and existing controller
   handoff. Resolve the table above. Add the chosen explicit codec/admission/history transition and
   prove replay through every intermediate movement/arrival/failed-sample boundary. Include the old
   earned adventure save, downed/blocking allies and permission changes, with no rule/budget waiver.
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
