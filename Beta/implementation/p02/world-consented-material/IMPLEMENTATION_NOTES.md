# Complete-world consented material: implementation notes

## Inputs and preservation

- Assignment: `ASSIGNMENT.md` in this packet. Accepted decisions: `../consented-production/DESIGN_REVISION.md`.
- Input is the working tree preserved by `PRESERVATION.json` (checkpoint
  `codex/checkpoint-before-world-consented-material-20260929T055115Z`, snapshot `2096b9f`), not HEAD alone.
  The 19 staged predecessor files are unrelated and untouched. No git, index, branch or agent operation is used.
- Baseline before any edit: `qualification/build-*` first run, exit 0, 0 warnings, 0 errors.
- Validation uses only the three packet runner commands. Test names contain `Consented` so the
  `focused` filter selects them.

## Chosen shape (recorded before code edits)

### 1. Consent terms versus execution freshness

`ConsentedMaterialTerms` binds world, branch, sender, recipient, lot kind, lot identity, definition
identity and version, source and destination storage, quantity and the approved lot revision. The
production, custody and both storage revisions leave the terms. The executor reads the actual current
owners, checks control, ownership, stock, protection, reservation and combined capacity, and captures
execution revisions from current state for the raw custody planner. A candidate is pinned to the
exact owners it was decided against (`ConsentedMaterialTransferPlan.ApplyTo` and the world step's
expected world revision). ConsentedCustody V1 is not edited.

### 2. World boundary: paired participant commands

Two ordinary `WorldActionCommand` submissions in one advancing batch, one per participant:

| Role | Actor | Intent | Permanent identity role |
| --- | --- | --- | --- |
| Offer | sender | `OfferMaterialIntent(terms, expected recipient presence revision)` | `Command` |
| Acceptance | recipient | `AcceptMaterialIntent(offer command id, offer content hash, terms)` | `Command` (the acceptance identity) |
| Split child | none | derived from the offer command | `SpawnedEntity` |

Each submission is decided with its own principal, session and actor, exactly as every other world
action. Neither is derived from the other's payload. The pair has no persisted offer: an offer
without its acceptance in the same batch (or the reverse) is refused and nothing is published. This
is a Core paired command boundary, not an asynchronous Host offer protocol.

The pair is resolved by `WorldConsentedMaterialResolver` inside `WorldStepProcessor.DecideImmediate`:
current control of both actors, both presence revisions, both bodies' manipulation, spatial reach,
no reading hold, advancing step only, then the shared `ConsentedMaterialTransferStep`. Both actors
spend their action and their movement opportunity for the step. Each actor has at most one action per
batch, so neither can use the stock in the same batch.

### 3. Evidence owner and binding

`WorldConsentedMaterialHistory` (owned by `WorldCoordinatedContinuation`) is the ordered,
bounded, per-segment evidence list. A record holds: world sequence, both action indexes, the offer
command and acceptance (with their submitted principal/session claims as historical evidence), the
moved lot identity, the actual moved interval, the lot revision spent, and the content hashes of the
original and candidate inventory and production owners.

Well-foundedness: inventory and production hashes never include the world evidence, and the evidence
never includes the world root hash or a journal publication hash. Dependency order is
owners -> evidence record -> evidence history -> world root -> journal. No hash is an input to itself.

Production validation receives the projected ordered custody movements of that evidence
(`ProducedCustodyMovement`) as explicit input. It replays each imported or locally produced lot
through its retirements and movements by lot revision, tracking custodian, prefix cursor and split
children, and requires the live lots to equal the replayed partition exactly.

### 4. Explicit interpretation

- World profile 28 = coordinated world plus consented material evidence. Selected only by
  `WorldConsentedMaterialAdmission.CreateFresh`; profile 27 and older never gain it.
- Snapshot codec V28, step profile 28, action codec V15 (the two new intents).
- Production context capability `SupportsConsentedWorld` with its own context profile number and
  `ProductionCodecV11`. Codecs V1-V9 and contexts 1-7 keep exact bytes and refuse the capability.
- The coordinated cut closes the evidence list with the material, care and resource owners.

### 5. Standalone ordered adapter

The standalone ordered profile (`ProductionCodecV10`, context 8) stays experimental and unfrozen. It
adopts the corrected terms and the same decision step. It is not the world binding.

## What the chosen shape became

The shape above was recorded before code edits. The delivered work differs; the differences are
gaps, not revised requirements.

| Shape element | Delivered |
| --- | --- |
| 1. Consent terms and execution freshness | Implemented and executed as designed. |
| 2. Paired participant commands | Intents, their action codec V15 and the paired decision exist. The decision is `WorldConsentedMaterialPlanner`, a public planner over the world's actual owners. It is **not** called by `WorldStepProcessor`. The offer intent also carries the observed recipient presence revision and the acceptance the observed sender presence revision. |
| 3. Evidence owner and binding | `WorldConsentedTransferEvidence` and `WorldConsentedMaterialHistory` exist with the acyclic binding. They are **not** owned by `WorldCoordinatedContinuation` and no snapshot encodes them. Production validation is `ProductionSnapshot.ValidateCustodyEvidence`, an explicit call; continued validation does not invoke it. |
| 4. Explicit interpretation | **Not implemented**: world profile 28, snapshot codec V28, step profile 28, `SupportsConsentedWorld`, `ProductionCodecV11`, fresh admission and the coordinated cut. Every step profile refuses the paired intents instead. |
| 5. Standalone ordered adapter | Kept, unfrozen, with the corrected terms and step. |

## Findings during the work

- **Proof infrastructure.** Every complete coordinated world in the repository is built from
  `OpenFantasy.Hosting` embedded content (`RestorativeJourneyAdmission.CreateEmbedded`) and exercised
  by `OpenFantasy.Hosting.Tests`. `OpenFantasy.Core.Tests` references Core only, has no
  `InternalsVisibleTo`, and contains no profile 21-27 world. This packet owns no Hosting path or
  test project file, and its runner executes Core tests only. The coordinated journey, the
  coordinated cut and the construction join inside a coordinated world therefore cannot be executed
  under this packet's ownership and commands.
- **Raw movement request.** `ToRawMovementRequest` cannot throw for any valid terms value:
  `ExpectedRevisionSet` compares kind and identity, the terms constructor keeps lot and storages
  distinct, the branch revision addresses the terms' own branch and `TransferLotIntent` needs only
  a positive amount and different storages. No reachable bug was found and no catch was added. An
  executed case covers terms whose lot aliases scope, actor, principal and session identities.
- **Overflow.** Revision overflow in the shared step is refused as unavailable by both planners. The
  ordered replay lets it propagate to the decoder, which already reports it as an invalid value.
- **Old worlds never validated produced custody before a cut.** Ordinary admission of a
  non-continued production checks output partition and lot history, not custodians. The evidence
  replay is strictly stronger and is where a complete-world profile must enforce custody.
- **Hosting hard-codes the coordinated profile.** `ProfileVersion` and `Coordination` occur 45 times
  in 17 read-only Hosting files, including journal recovery and the SQLite authority. A new world
  profile needs matching Hosting changes by their owner.

## Work record

See `WORKER_RESULT.md` for files, executed results, preserved failures and remaining gaps.
