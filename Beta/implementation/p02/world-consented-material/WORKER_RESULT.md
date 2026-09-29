# Worker result: complete-world consented material

**Status: incomplete. The assignment's deliverable is not met.** The consent contract is corrected
and proven, production has evidence-driven custody validation, and the world has a paired decision
with well-founded evidence. No world profile publishes a paired transaction, so there is no
coordinated-world journey, no coordinated cut with evidence and no complete-world admission. This
is a partial component result and must not be accepted as the integration unit.

No whole-game, Host, native, asynchronous offer, migration or retained-world personal-dose pass is
claimed. The original failed acquisition world is untouched and still needs an explicit admitted
transition.

## Executed evidence (packet runner only)

All runs are retained in `qualification/` with logs, TRX and source pins.

| Run | Result |
| --- | --- |
| `build-20260929T055306Z` | Baseline before any edit: exit 0, 0 warnings, 0 errors. |
| `focused-20260929T060536Z` | **Preserved failure:** 64 passed, 1 failed. A new test crafted three times and then gifted from the exhausted raw berry lot. The test setup was wrong, not the contract; it was corrected to two crafts. |
| `focused-20260929T061108Z` | 68 passed, 0 failed. |
| `build-20260929T061024Z`, `build-20260929T061804Z` | **Preserved failures:** one compile error in `ProducedCustodyPartition` (null-coalescing a collection expression) and one missing test import. Both corrected. Every retained build not listed in this table as a failure exited 0 with 0 warnings and 0 errors. |
| `focused-20260929T061903Z` | **Preserved failure:** 68 passed, 6 failed. The new world test fixture allocated input mass per batch instead of per output unit, so its recipe was refused before any world case ran. Corrected in the fixture. |
| `focused-20260929T062009Z` | 74 passed, 0 failed. |
| `core-20260929T062208Z` | Full Core regression before the construction case was added: **1,661 passed**, 0 failed, 0 skipped, 3 m 42 s, `sourceUnchanged: true`. |
| `build-20260929T062701Z` | **Preserved failure:** a nullable dereference in the new construction case, corrected. |
| `focused-20260929T062728Z` | 75 passed, 0 failed, including the construction case. |
| `core-20260929T062811Z` | Full Core regression on the settled source after `build-20260929T062757Z`: **1,662 passed**, 0 failed, 0 skipped, 2 m 29 s. The runner exited 0, which it does only with unchanged source pins. This is 1,642 preceding cases plus 20 new ones. |

No Hosting, native or client test was run by this worker. Shared Core owners were edited
(`WorldStepBatch`, `WorldActionCodec`, `ProductionSnapshot`), so the lead's Hosting regression is
required before integration.

## What is proven

Consent contract (`ConsentedMaterialConsentTests`, `ConsentedMaterialTransferTests`,
`ConsentedMaterialCodecTests`):

- Terms hold the goods, parties, storages, quantity, definition pin and approved lot revision only.
- An unrelated craft and unrelated stock entering and leaving both bags advance the production,
  custody and both storage revisions; the unchanged approval then applies with the exact prefix
  interval, for produced and for raw stock.
- Spending from the approved lot, crafting from the approved raw lot, an acceptance restating older
  terms, a reserved lot and insufficient actual capacity each prevent application with untouched
  owners. When the unrelated unit leaves again the same approval applies.
- Changed or removed control and changed ownership withdraw approval of the same unchanged terms
  (`IsApprovedByBothParticipants`), and the ordered segment refuses the replaced admission before
  deciding. The ordered profile pins its admission, so these are shown against replaced owners, not
  as a change inside one segment.
- A decided candidate is refused as stale after any owner change; the same approval decided again
  against the current owners applies.
- Exact limits: at 512 operations a forged approval is `NotAuthorized` and a valid one
  `Unavailable`; after a legitimate cut the same valid approval applies and identities closed by the
  cut are refused in either role. At 4,096 combined live lots a produced and a raw split are refused
  and whole movements apply; one below the bound a split applies and reaches the bound.
- Balanced invalid states with valid controls, in the ordered profile: swapped custodians and a
  suffix moved instead of the prefix decode as structure and fail authority validation with the
  exact replay message; moved stock without its recorded approval does the same.

Custody partition (`ConsentedCustodyEvidenceTests`), on stock crafted by the real planner:

- Ordered evidence admits a split gift, the recipient's real recraft with ancestry to the moved
  interval and original producer, and an onward whole movement.
- Swapped custodians, suffix for prefix, a whole movement under split evidence, removed evidence
  and unapplied evidence are each refused with the exact partition message although every state
  conserves all units and passes ordinary admission.

World decision (`ConsentedWorldMaterialTests`), in a real admitted production world (profile 3)
whose stock is crafted through `WorldStepProcessor`:

- A split gift needs both current controls, both bodies, reach and current presence, and records
  evidence whose owner hashes equal the actual original and candidate owners.
- The host-controlled resident gives onward under the same rules.
- Control is decided per participant before any availability; forged submissions naming absent
  stock are `NotAuthorized`.
- Held interaction, stale presence, a body without manipulation, residents out of reach, a stale
  lot revision and excess quantity are refused with their exact reasons.
- Every step profile 1-27 refuses both intents, older action codecs refuse the new bytes, and
  world admission refuses a consented ordered production context.
- After real construction in a profile 5 world the installed reserved output is refused and the
  unreserved byproduct moves, with the recipient brought into reach by real movement samples.

## Limits of that evidence

- The world cases decide; they do not publish. The successor world in the onward case is assembled
  by the test from the decided candidates. That is not a world step.
- The worlds are profile 3 and, for construction, profile 5. Neither has care, an encounter
  lifecycle, terrain liquid or a coordinated archive.
- The partition cases build moved states by hand to mirror the shared step. They prove the
  validator, not a journey.
- No independent byte vector exists for the corrected approval frames, action codec V15 or the
  evidence frame. None of these bytes is frozen.

## Not done, by assignment item

1. **Item 1** is done for the standalone contract. Reservation change is shown with two worlds of
   identical identities, not as a change inside one segment, because the ordered profile pins
   reservations.
2. **Item 2** is partial. Missing: `WorldStepProcessor` integration, command receipts and identity
   reservation for both commands, spent action and movement opportunity, competing-use refusal
   inside a batch, and refusal of partial publication by the complete root admission.
3. **Item 3** is partial. `ValidateCustodyEvidence` exists and is proven on non-continued production.
   Missing: the continued complete-world capability that invokes it in place of frozen custody, and
   proof against an imported basis. The experimental V10 adapter is kept and unfrozen.
4. **Item 4** is partial. The evidence binding is specified and implemented. Missing: world profile,
   snapshot, step and archive selection of it.
5. **Item 5** is not done. No coordinated journey, cut, decoded successor or second cut.
6. **Item 6** is partial. Operation and lot limits, continuation, identity reuse and balanced
   mutations are done in the ordered profile. Real construction is completed by 200 actual work
   steps through `WorldStepProcessor` in an admitted construction world (profile 5); the paired
   decision then refuses its installed reserved output while the same participants move its
   unreserved byproduct, and the custody replay admits the real completion and refuses a movement
   of the installed lot. Missing: the same join inside the coordinated world and across a cut.
7. **Item 7** is partial. Old step profiles, old action codecs and old world admission of the
   ordered context are covered. There is no new world admission to test against old worlds.
   Authentication precedes availability in both new entrypoints. The raw request was inspected; no
   reachable exception exists.
8. **Item 8**: both architecture pages are current. `consented-world.md` lists the unprovided
   complete-world obligations as design obligations.

## Blocker for the missing proofs

The coordinated world exists only through `OpenFantasy.Hosting` content and
`OpenFantasy.Hosting.Tests` fixtures. This packet owns neither, Core tests cannot reference
Hosting, and the runner executes Core tests only. See `IMPLEMENTATION_NOTES.md`.

Required changes outside this packet's ownership, for the next unit:

- Ownership of `Beta/Runtime/tests/OpenFantasy.Hosting.Tests/Care/**` and a runner selection that
  executes those managed tests, or a Core complete-world fixture with its project file entry.
- Hosting owners must admit the new world profile wherever profile 27 or `Coordination` is matched
  (45 occurrences in 17 files).
- Add index rows for `consented-production.md` and `consented-world.md` to
  `Beta/Runtime/docs/architecture/README.md`.

## Files

New source, `Beta/Runtime/src/OpenFantasy.Core/`:

- `Production/ProducedCustodyMovement.cs`, `Production/ProducedCustodyPartition.cs`
- `Worlds/WorldConsentedMaterialIntents.cs`, `Worlds/WorldActionCodecV15.cs`,
  `Worlds/WorldConsentedTransferEvidence.cs`, `Worlds/WorldConsentedMaterialHistory.cs`,
  `Worlds/WorldConsentedMaterialPlanner.cs`

Modified source:

- `Authority/ConsentedMaterial/ConsentedMaterialTerms.cs`, `ConsentedMaterialCommand.cs`,
  `ConsentedMaterialCodecV1.cs`
- `Production/ProductionSnapshot.cs`, `Production/Continuation/ConsentedMaterialTransferStep.cs`,
  `ConsentedMaterialTransferPlanner.cs`, `MaterialContinuationReplay.cs`, `MaterialOperationCodec.cs`
- `Worlds/WorldActionCodec.cs`, `Worlds/WorldStepBatch.cs`

Tests, `Beta/Runtime/tests/OpenFantasy.Core.Tests/`:

- New: `Production/ConsentedMaterialConsentTests.cs`, `Production/ConsentedCustodyEvidenceTests.cs`,
  `Worlds/ConsentedWorldMaterialTests.cs`
- Modified: `Production/ConsentedMaterialJourney.cs`, `Production/ConsentedMaterialTransferTests.cs`,
  `Authority/ConsentedMaterial/ConsentedMaterialCodecTests.cs`

Documents: `Beta/Runtime/docs/architecture/consented-production.md` (revised),
`consented-world.md` (new), this packet's `IMPLEMENTATION_NOTES.md` and `WORKER_RESULT.md`.

ConsentedCustody V1, Hosting, client, fixtures, specs, the handoff, instructions and the runner are
untouched. No git, index, branch or agent operation was performed.

## Behavior changes to review

- The material terms frame has 14 fields instead of 18. It was unpublished.
- The five removed stale-revision refusals are replaced by lot-revision refusal and current-state
  checks. A previously refused approval can now apply after unrelated activity; that is the
  accepted correction.
- `WorldStepBatch` has one added refusal, which no existing intent reaches.
- `ConsentedMaterialCodecV1.EncodeTerms` and `DecodeTerms` are public for the world action codec.
