# Fable implementation result

The implementation and focused tests are written but **nothing has been built or run** (no Bash permission), so compile and test status are unverified. The permission check passed on the first edit, and all edits stayed inside the owned files.

## Files

Edited, under `Beta/Runtime/src/OpenFantasy.Core/Worlds/`:
- `WorldStepDecision.cs` — `MaterialEvidence`, optional constructor argument after `sealedResourceSegment`; shared empty collection for ordinary decisions.
- `WorldStepProcessor.cs` — `DecideWithMaterialEvidence`; the resource-cut wrapper passes evidence through; one capture hook after reconciliation.
- `WorldCareProcessor.cs` — optional collector (default null); captures only an admitted, encoded `AdministerRemedy`.

New, same directory:
- `WorldMaterialActionEvidence.cs`, `WorldMaterialOwnerPins.cs`, `WorldMaterialEvidenceKind.cs`
- `WorldMaterialEvidenceCollector.cs`, `WorldMaterialEvidencePinner.cs`, `WorldMaterialEvidenceEncoding.cs`, `WorldMaterialEvidenceOwners.cs`, `WorldMaterialEvidenceCoverage.cs`

New test: `tests/OpenFantasy.Hosting.Tests/Care/LiveMaterialEvidenceTests.cs`

## Test coverage written
- Each observed decision is compared with the ordinary decision for the same batch: candidate bytes, receipt bytes, identities.
- Aftermath: acknowledgment yields none; the administered dose yields `AftermathRemedy` with `After` equal to the final candidate pins.
- Raw self use and real crafted aid, including the craft step itself.
- Archival cut at the 255-batch boundary: evidence survives the wrapper; a refused unowned dose yields none while still sealing.
- Stale-revision refusal yields none.
- A non-26 world is refused by the explicit path.
- All journeys use the existing complete profile 26 fixtures, with excavation, construction, reservoir and liquid pins asserted present and preserved.

## Decisions you should review
- **Two root hashes.** `SourceWorldHash` is the root `DecidePrepared` actually interpreted; after a cut that is the continued root carrying the archive anchor. I added `InputWorldHash` for the caller's selected durable root. They are equal when no cut occurs.
- **Unsupported material intents throw.** The explicit path refuses batches containing `ClimbIntent`, `ScheduleRunoffTransportIntent` or `RunoffCommitmentIntent`, rather than returning a silent gap. This means a runoff commitment batch cannot go through the evidence path; relaxing it is one method in `WorldMaterialEvidenceCoverage`.
- **`WorldMaterialOwnerPins.Observe(WorldSnapshot)` is public.** Core internals are not visible to Hosting.Tests, so tests need it to compare pins. It is profile 26 only and cannot create evidence.
- **Created-identity bound is 256 per action**, my guess; exceeding it throws rather than truncating.
- **`ActionOrdinal` is `int`**, and `CreatedIds` keep reservation order (spawned first), not sorted order.
- **Space pin** uses `ExcavationMaterialSource.EncodeSpace`, which follows the support mode; the profile 26 root itself encodes space with mode 1.

## Uncertainties
- The stale-revision refusal test assumes the restorative resolver rejects a wrong `ExpectedActorRevision`; I did not read that resolver.
- The profile-gate test assumes `CareWorldFixture.Genesis` is not profile 26.
- Hash laziness relies on `WorldSnapshot.ContentHash` caching; refused-only batches do no world hashing.

## Not done
- No gathering, excavation, construction, transfer or quest-delivery test, and no multi-actor batch with ordinal gaps. The capture hook covers those kinds, but they are untested here and left for your mixed-action and geography tests.
- No architecture docs, handoff or Git changes, per ownership.
