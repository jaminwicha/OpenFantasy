# Party withdrawal commitment checkpoint

2026-10-03. Parent: `8366b36f2c43d1af286c47170167166e27049923`.
Status: standalone component implemented and checked; the party-withdrawal feature remains incomplete.
Work is direct; no orchestration or external model request occurred.

## Outcome and boundary

The core can retain a complete declared encounter side and each actor's own exact withdrawal command,
origin, route, speed and walked prefix. Choosing or progressing one actor never chooses or moves an
ally. Cancellation/interruption bookkeeping removes a choice while retaining the person in the
required roster. All-arrived bookkeeping is deliberately distinct from authority to end the fight.
The body and timing owners retain actual position and debt; this component records committed-choice
evidence without owning another clock or simulation.

The component is not installed in `WorldSnapshot`, a command processor, save root, host or client.
It does not make party retreat playable or weaken `AllyRemains`. Existing profile34–40 saves,
root/action/step formats, Full Wait, authentication, default adventure admission and launch behavior
remain unchanged. The exact integration obligations and unresolved version selection are in the
[decision](../../PARTY_WITHDRAWAL_DECISION.md). They remain necessary to finish this feature and
the original cooperative expedition; this checkpoint does not replace either goal.

## Verification and review

[Final focused run](checks/withdrawal-contract-final.trx): 30 passed, zero failed/skipped; 18 new
withdrawal cases and 12 existing retreat/profile/attribution cases. Tests compile under the project's
warnings-as-errors setting, with no reported warnings. They cover independent movement evidence and
tariffs; immutable input ownership; missing/duplicate/foreign members; stale revisions and commands;
teleports/extra samples; incomplete arrival; cancellation that retains membership; round-trip
continuation between every sample; malformed counts/rules/order/speed/progress; alternate action
kinds; exact delegated attribution; frame size; and legacy cross-format refusal.

The maximum domain fixture has 128 members, 32 recorded samples per member and speed 1. It encodes to
72,424 bytes within the component's 200 KiB bound, with hash
`af2575791fc1bdbf5fda5237f19aee9a31c099774edf6dfeb1a0d6655226d425`.
This measures only that structural fixture. It proves neither full-world envelope capacity, valid
world membership, production party size nor runtime performance. No native/human, network, save
migration, full-world continuation or independent critic acceptance is claimed.

Lead review checked that public restore/record APIs cannot be confused with authority: the component
requires external authentication and complete-world admission, `AllArrived` is not `CanDepart`, ended
choices cannot shrink the required set, and restored attribution does not establish current permission.
Derived expected feet and readiness evidence do not replace their world owners. The core's immutable
objects and deterministic ordering have no Godot dependency. Full activation must still authenticate
against inventory/controller state, validate complete side membership and couple movement with actual
debt and terminal history. Independent architectural review remains an explicit unrun integration gate
while the owner prohibits orchestration.

The first build failed because a canonical frame helper expects `ReadOnlyMemory<byte>[]`, not
`byte[][]`. The exact typed conversion was corrected; [the failed log](checks/withdrawal-contract-01.log)
and the intermediate 24-case pass are preserved. No acceptance threshold, old rule or test was removed.

## Next coherent increment

Connect the model to complete-world command admission, due-order resolution, collision/interruption,
arrival decisions, cancellation, incapacitated allies, controller return and collective termination.
Prove explicit interpretation selection and lived-save history continuity before activation; use the
earned shared adventure lineage, not an unmarked replacement world. Host offers and the existing
wheel then expose only actual own-character choices and safe party status. The same saved expedition
must exercise real enemy turns, debts/injury, departure, care/commerce and changed-place return.
The decision retains the broader rescue, participation and 1–4-player obligations and all original gates.

## Documentation review

- **Plan/spec:** [CURRENT_ROADMAP](../../CURRENT_ROADMAP.md),
  [AGENT_HANDOFF](../../AGENT_HANDOFF.md) and [PARTY_WITHDRAWAL_DECISION](../../PARTY_WITHDRAWAL_DECISION.md)
  route RQ-029/RQ-079/RQ-099 and P02 through the complete feature. The component is marked unbound;
  old saves and frozen specifications remain preserved. Independent review and full exits stay open.
- **Reference:** [party withdrawal](../../../Runtime/docs/architecture/party-withdrawal.md) explains
  actual component ownership, APIs, canonical bytes, timing/position derivation, strict boundaries and
  complete-world integration obligations. The reference index and existing retreat page link it while
  retaining the live v2 party refusal. Status/measurements remain in this packet and the living plan.
- **Accuracy/completeness:** lead compares source and tests against both existing retreat state/resolver
  and root codec dispatch, required-decision handling and command attribution. No encoder or domain
  record is described as a world publication, save transition or actor authorization. The selected
  design accounts for all actors without silently implementing a free guard or abandoning incapacity.
- **Checks:** focused 30-case run passes; scoped documentation link/field and whitespace checks are
  recorded in `checks/documentation.json`. Those mechanical checks do not replace the source review
  or still-unrun independent, runtime, native and human acceptance gates.

Preservation: `codex/checkpoint-before-party-withdrawal-20261003` retains the clean pre-change source.
`codex/milestone-withdrawal-commitments-20261003` records this passing component increment only.
The unrelated 19 staged predecessor files remain excluded from the commit.
