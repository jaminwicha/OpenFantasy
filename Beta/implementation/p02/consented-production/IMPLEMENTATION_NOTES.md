# Consented production transfer: implementation notes

## Scope and inputs

Assignment: `ASSIGNMENT.md` in this packet. Material dependency only: bilateral approved gifts of
actual produced (`TransformedLot`) and raw (`CountedSourceLot`) stock with exact provenance and
continued history. Host, UI, offer lifetime, durable world admission and native acceptance are not
part of this unit and are not claimed.

Owned paths used:

- `Beta/Runtime/src/OpenFantasy.Core/Production/**` (extensions; old formats and rules unchanged)
- `Beta/Runtime/src/OpenFantasy.Core/Authority/ConsentedMaterial/**` (new)
- `Beta/Runtime/tests/OpenFantasy.Core.Tests/Production/**`, `.../Authority/ConsentedMaterial/**`
- `Beta/Runtime/docs/architecture/consented-production.md` (new)
- this packet's `IMPLEMENTATION_NOTES.md`, `WORKER_RESULT.md`

Owners inspected before design (read only unless under the owned paths): `ProductionSnapshot`,
`ProductionContinuationContext`, `ProductionContinuationValidation`, `MaterialContinuationValidation`,
`MaterialContinuationReplay`, `ProductionClaimValidation`, `ProductionIdentityRegistry`,
`MaterialOperation*`, `ProductionMaterialCodec`, `ProductionCodecV4`/`V9`, `MaterialTransferPlanner`,
`MaterialTransformCommitter`, `InstantCraftPlanner`, `ProductionContinuationBasis`,
`Authority/ConsentedCustody/*` (V1, unchanged), `CountedAuthorityState`, `CustodyTransferPlanner`,
`WorldCoordinatedAdmission`, `WorldMaterialCareArchiveAnchor`, `WorldSnapshot`.

Validation: only the two packet runner commands (`run_worker_checks.py build|test`).
No git, index, commit, subprocess agent or other shell use.

## Outcome

Implemented as designed below for the standalone ordered profile. Runner evidence:
`build-20260929T052638Z` (0 warnings, 0 errors) and `test-20260929T052725Z` (317 passed, 0 failed,
0 skipped). Tests outside the runner filter were not run. The complete-world binding and the other
open items are listed in `WORKER_RESULT.md`.

## Chosen compatibility and integration design (recorded before code edits)

1. **Approval contract, new and separate.** `Authority/ConsentedMaterial` defines
   `ConsentedMaterialTerms/Command/Acceptance/Approval` and `ConsentedMaterialCodecV1` with their own
   frame tags. Terms add lot kind, exact definition identity and version, and the production
   revision beside the custody, lot and both storage revisions. ConsentedCustody V1 types, tags and
   bytes are untouched; no V1 value is converted or reinterpreted as a produced approval.
2. **One shared decision step.** `ConsentedMaterialTransferStep` (Production) decides one approval
   against the actual inventory and live produced lots. The planner and the ordered replay call the
   same step, so a recorded operation is re-decided rather than trusted. Raw stock reuses
   `CustodyTransferPlanner`; produced stock mirrors its rules (genuine reservation and equipped stock
   unavailable, manual flags travel) and adds combined raw+produced capacity and lot bounds.
3. **Evidence in the production owner.** `MaterialConsentedTransferOperation` (kind 10) retains the
   command, acceptance, both verified deliveries and the actual moved lot. Command and acceptance are
   distinct permanent identity roles; split children keep the role of their owner kind.
4. **Explicit capability and version.** `ProductionContinuationContext.SupportsConsentedTransfer`
   is selected only by `ProductionSnapshot.ContinueConsentedMaterial`, has context profile number 8
   and encodes as `ProductionCodecV10`. It requires ordered material history and excludes
   construction, terrain, transport, excavation and complete-world profiles. It can neither be
   gained nor dropped by another cut. Profiles 1-7 / codecs V1-V9 keep exact bytes and rules.
5. **Coordinated world binding (not implemented or qualified here).** V9 has no ordered material
   history and its validation freezes transformed custody, so it rejects any moved produced lot; the
   world admits only `SupportsCompleteWorld` contexts whose hash equals its anchor, and a consented
   context differs in profile number and hash. A later explicit complete-world profile must retain
   the same operation record and re-decide it with the same step; that profile, its per-lot custody
   validation and world admission are remaining obligations, stated in `WORKER_RESULT.md`.
   Architecture impact: new contract page `consented-production.md`; the index row in
   `docs/architecture/README.md` is outside this packet's ownership and is proposed to the lead.
6. **Host obligations kept explicit.** Permanent identity occupancy across cuts, rejected-identity
   and receipt visibility, offer lifetime, concurrency between external history and a decision, and
   reconnect are supplied by the later Host binding; the core takes occupancy as an input.
