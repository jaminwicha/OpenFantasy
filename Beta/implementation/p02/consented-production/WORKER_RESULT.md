# Worker result: production-aware consented transfer

Status: standalone material dependency implemented and passing the packet checks. The
coordinated complete-world binding is **not** implemented; see incomplete obligations. No
full-game, native, Host, asynchronous offer, retained-world or durable admission pass is claimed.

## Evidence (packet runner only)

| Run | Result |
| --- | --- |
| `qualification/build-20260929T051852Z` | exit 0, 0 warnings, 0 errors (core edits only) |
| `qualification/build-20260929T052638Z` | exit 0, 0 warnings, 0 errors (with new tests) |
| `qualification/test-20260929T052725Z` | 317 passed, 0 failed, 0 skipped; `sourceUnchanged: true` |

The test filter is `Production|Authority|Custody`. It includes the 13 new tests and the existing
production/authority/custody tests, which passed unchanged. No fixture file was edited. Tests
outside that filter (world, construction, terrain, geography, hosting) were **not run** by this
worker although shared owners they use were edited; the lead's broader regression must cover them.

## Changed files

New source, `Beta/Runtime/src/OpenFantasy.Core/`:

- `Authority/ConsentedMaterial/ConsentedMaterialLotKind.cs`, `ConsentedMaterialTerms.cs`,
  `ConsentedMaterialCommand.cs`, `ConsentedMaterialAcceptance.cs`, `ConsentedMaterialApproval.cs`,
  `ConsentedMaterialCodecV1.cs`
- `Production/ProductionCodecV10.cs`
- `Production/Continuation/ConsentedMaterialTransferStep.cs`, `ConsentedMaterialTransferPlan.cs`,
  `ConsentedMaterialTransferPlanner.cs`

Modified source, `Beta/Runtime/src/OpenFantasy.Core/Production/`:

- `ProductionSnapshot.cs`: `ContinueConsentedMaterial`; content hash selects V10 for the capability.
- `ProductionIdentityRegistry.cs`: acceptance, participant, storage, lot and pin roles.
- `ProductionMaterialCodec.cs`: version 10 selection; versions 4-8 refuse the capability.
- `Continuation/ProductionContinuationContext.cs`: `SupportsConsentedTransfer`, profile number 8.
- `Continuation/MaterialOperation.cs`: kind 10 and `MaterialConsentedTransferOperation`.
- `Continuation/MaterialOperationHistory.cs`: capability and distinct acceptance identities.
- `Continuation/MaterialOperationCodec.cs`: `material.consented-transfer` payload.
- `Continuation/MaterialContinuationReplay.cs`: re-decides each recorded approval.

New tests, `Beta/Runtime/tests/OpenFantasy.Core.Tests/`:

- `Production/ConsentedMaterialJourney.cs` (fixture), `Production/ConsentedMaterialTransferTests.cs` (10)
- `Authority/ConsentedMaterial/ConsentedMaterialCodecTests.cs` (3)

Documents: `Beta/Runtime/docs/architecture/consented-production.md`, this packet's
`IMPLEMENTATION_NOTES.md` and `WORKER_RESULT.md`. ConsentedCustody V1, World, Hosting, client,
specs, handoff, instructions and the runner are untouched. No git or index operation was performed.

## What the tests establish

- Craft -> partial gift (split child) -> whole gift -> cut -> recipient consumes and recrafts ->
  cut -> two onward gifts to a third owner -> cut -> reopen. Producer batch, output index,
  intervals and recraft ancestry are asserted; 5 produced = 3 live + 1 consumed + 1 recrafted; every
  closed segment decodes, validates against its own inventory and re-encodes to equal bytes.
- Reuse of earlier command, acceptance, retired child and batch identities in either role is refused.
- Forged sender/recipient deliveries and claims, non-owner storages, wrong world/branch, unbound
  acceptance, five stale revisions, wrong kind, wrong definition identity and version, excess and
  maximal quantities; every refusal returns the same owner instances with unchanged hashes.
- Split child collisions for produced and raw stock, including acceptance equal to the child.
- Raw and produced contents share storage capacity, including a raw transfer the raw owner alone
  would accept.
- Reserved and equipped produced stock is unavailable; manual flags travel on both split halves.
- `ApplyTo` refuses changed control, policy, capacity and advanced history.
- Five independent mutations of valid V10 bytes are refused by decode plus validation.
- V3, V4 and V9 encoders/decoders refuse consented history; V10 refuses a V4 context; raw consented
  custody frames and material frames do not decode as each other.

## Incomplete obligations

1. **Complete-world binding is not built.** Required: an explicit complete-world capability that
   retains `MaterialConsentedTransferOperation`, per-lot produced custody validation in
   `ProductionContinuationValidation` using `ConsentedMaterialTransferStep`, its snapshot codec, and
   world admission, anchor and action binding. Until then no lived world can gift produced goods.
2. **No composition with construction, terrain, transport or excavation profiles.** The reserved
   stock test admits a reserved produced lot explicitly; it does not run an actual construction
   completion inside a consented segment.
3. **Untested combinations inside a consented segment:** gathering sources, unilateral
   `MaterialTransferPlanner` and `MaterialSourceDeliveryPlanner` operations.
4. **Host obligations remain open:** permanent identity registry and its concurrency, rejected
   identity and receipt visibility, offer lifetime, reconnect, reach and durable commit. One test
   shows a segment alone accepts an identity closed by an earlier cut. No material receipt type exists.
5. **Proposed edit outside ownership:** add an index row for `consented-production.md` to
   `Beta/Runtime/docs/architecture/README.md`.

## Design differences to review

- A wrong world is refused as `NotAuthorized`; the raw V1 authority throws for it.
- Refusals use `WorldActionRejection`, adding `RevisionConflict`, not `CommandRejectionReason`.
- A produced transfer advances the custody revision and both storage revisions by one.
