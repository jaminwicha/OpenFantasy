# Critique: complete-world consented material (Core increment)

**Recommendation: continued WIP — do not accept yet.** The publication path is structurally sound and I found no bypass, but the rollback path the scope singles out is never executed by any test, and one evidence field is unbound at admission.

I confirmed the journey evidence: 9 passed, exit 0, `sourceUnchanged: true`, all in `ConsentedWorldPublicationTests`. Nothing was executed by me. I did not read the V28/V11 codecs, the archive segment, `CareWorldSession`, `CoordinatedLiveWriterJourney` or the V10 adapter, so I make no claim about them.

## Bugs

| # | Severity | Location | Counterexample | Minimal correction |
|---|---|---|---|---|
| B1 | Medium | `WorldConsentedTransferEvidence.cs:77-84`, `ProducedCustodyPartition.cs:75-93` | In a V28 snapshot, rewrite a produced record's command and acceptance terms consistently to another `Definition`. `Binds`, control and ownership hold; replay compares lot, revision, custodians, quantity and interval only, so altered evidence is admitted. The raw path does check definition (`WorldConsentedMaterialAdmission.cs:52`). | Carry the definition in `ProducedCustodyMovement` and compare it to `lot.Definition` in `Move`. |
| B2 | Medium (proof validity) | `ConsentedWorldBoundaryTests.cs:160-177` | The construction refusal asserts only `Unavailable`, which hold, availability, body, capacity and identity occupancy also produce. A full recipient bag would pass the test. | Add a positive control in the same state, before and after the cut: the same actors move an unreserved lot. |
| B3 | Low | `WorldConsentedMaterialAdmission.cs:49-57` | Raw evidence binds only source, definition and `MovedEnd <= UnitCapacity`. A rewritten same-length interval or lot identity is admitted, as is moved raw stock with its evidence removed. | Consistent with the stated journal boundary; pin it with a negative test, or bind the interval while the moved lot is live. |
| B4 | Low, latent | `WorldConsentedMaterialAdmission.cs:45` → `ConsentedMaterialApproval.cs:39-42` | Historical evidence is validated with current `CanControl`. I found no control-rebinding API in Core, so it is unreachable today. The first session rebind would make every retained segment inadmissible, contradicting `consented-world.md:40-41`. | Validate admitted actors and storage ownership only. |
| B5 | Low | `WorldStepProcessor.cs:291`, `WorldConsentedMaterialPlanner.cs:177` | `history.Append` and `ValidateForAuthority` throw outside refusal conversion, aborting the batch rather than refusing two commands. `Append` is guarded by cut headroom; I did not establish reachability for the other. | Convert to a paired refusal only if a concrete case exists. |

## Missing mandatory proofs

1. **Planner success, complete-admission failure.** Nothing executes `WorldStepProcessor.cs:456-481` for a pair. All three `Invalid_pair` variants are pairing or planner refusals, where no candidate ever existed. The test needs valid independent controls, a diagnostic plan that applies, and a root or physical join that refuses. It should assert:
   - both receipts are `Unavailable`;
   - inventory, production, reservoir, evidence and physical hashes are unchanged;
   - the split child is not reserved;
   - a later valid pair still publishes.
2. **Opportunities on failure.** Movement samples accompany only the successful pair. A refused pair must leave both actors able to move.
3. **World-level combined live-lot bound (4,096).** Split refused at the bound, whole applies, split applies one below. This exists only in the standalone ordered profile.
4. **Post-cut recraft and use under context 9/V11.** The journey recrafts and uses only before the first cut; after reopening it only gifts. Retirement of an imported or moved child in a continued segment, followed by a cut, is unproven.
5. **Routing outside `DecideImmediate`.** The intents are referenced only in the batch, pairing and that method. A profile-28 advancing pair during an encounter or pending consequence is unverified and untested.
6. **Interleaving.** A third actor's action between acceptance and offer, two pairs in one batch, and an acceptance naming an offer addressed to another recipient.
7. **Identity roles.**
   - No assertion that the split child is committed as `SpawnedEntity`.
   - After a cut only offer-command reuse is tested, on whole raw lots; closed acceptance and child identities are not.
8. **World-level lot mutations.** Tests alter evidence (removed, interval, producer). Swapped custodians and suffix-for-prefix on the lots exist only against hand-built component states.
9. **Old-context negatives at world level.** V9 decode of V11 bytes, a profile-27 world with context 9, and profile 28 with a non-consented anchor. The guards exist (`WorldConsentedMaterialAdmission.cs:31,38`, `WorldCoordinatedContinuation.cs:20`) but I saw no test.
10. **Broader Core and Hosting regressions** are still running.

## Verified sound by reading

- **Reservoir custody join.** `PlanReconcileCustody` is internal and selected only for produced evidence whose four hashes match the actual owners (`WorldReservoirBinding.cs:29-40`). `ValidateCustodySuccessor` requires the same revision, context and recipes and forbids added or erased operations. Public reconciliation keeps the advancing guard, and full admission still runs.
- **Pairing.** Needs the recipient's own command; acceptance defers to the offer index; one action per actor prevents competing use.
- **Context 9.** The early return in `ProductionContinuationValidation.cs:32` is covered by partition replay, which world admission always invokes.
- **Cut.** Headroom reserves the batch's offers, and the cut resets evidence only through `WithAnchor`.

## Future Host/UI work, outside this increment

- Pair deliveries are copies of command claims (`WorldMaterialPairing.cs:55-56`), and evidence stores them as deliveries. The Host must supply real ingress.
- Persisted offers, expiry, reconnect, SQLite binding and the permanent identity registry.
- Every candidate admission replays the whole segment's evidence; measure it under load.

No concrete duplicate authority was found that would justify removing the V10 adapter.
