# World canonical frame: worker result

Status: **implemented and focused-tested**. The measured recovery effect is **not** verified;
no profile, full regression or external crash probe was run by this worker.

## Implementation

`WorldCanonicalFrame` (new, Core) holds the canonical bytes of one exact `WorldSnapshot`.

- Its only construction is `Capture(world)`, which calls the unchanged `WorldSnapshotCodec.Encode`.
  There is no bytes+world constructor. Encoder refusals propagate unchanged.
- The backing array is private. `ToArray()` returns a copy; `Matches(ReadOnlySpan<byte>)`
  compares without exporting. No span or memory view of the array is exposed.
- `IsBoundTo` is reference identity of the root. Equal content or hash does not bind.
- It retains its own root only: no predecessor, no global cache, no serialized flag.

`WorldStepDecision` carries an optional `CandidateFrame`. Its internal constructor throws
`InvalidOperationException` when a supplied frame is not bound to the candidate.

Each existing Core envelope check now captures a frame at the same statement, inside the same
`try` where one existed:

| Path | Frame |
|---|---|
| Encounter timing, encounter action, lifecycle | Captured where the candidate was encoded |
| Care, accepted | Captured inside the existing rollback `try`; cleared with the candidate on refusal |
| Care, refused | Captured for the fallback root where it was encoded |
| Normal step with liquid cells | Captured at the final encode |
| Movement | Preflight frame kept only with the admitted root it was captured for; both are replaced together |
| Per-action preflight | Still encoded inside its rollback path; frame discarded, since that root is not the candidate |
| Resource and joint archive wrappers | Inner decision's frame passed through |
| Paths that never encoded (pending consequence, profiles without liquid cells) | No frame; Host encodes as before |

`WorldCommitPreparation` exports the frame's bytes when one exists, otherwise encodes at the
original statement. It rechecks the binding and throws `InvalidDataException` on mismatch. The
after-hash uses the frame's digest, evaluated at its original position.

`WorldConsequenceProcessor.cs` needed no edit. Its terminal decision is returned unchanged, so it
keeps any frame. Its time-only proof root is not the candidate and carries no frame.

## Tests executed

Both prescribed commands, run serially, exactly as written:

- Build: succeeded, 0 warnings, 0 errors.
- `WorldCanonicalFrameTests`: **7 passed, 0 failed, 0 skipped** (56 s), first run.
  Results: `qualification/frame-worker.trx`.

| Test | Covers |
|---|---|
| Captured frame reproduces the encoder | Exact bytes, length and hash on a profile27 genesis; mutated export leaves later exports and hash unchanged |
| Frame binds only its exact root | A decoded twin with identical bytes is not bound; the decision constructor refuses the twin |
| Commit preparation stores captured bytes | Stored bytes and after-hash equal the encoder; frame and stored row cannot alter each other |
| Automatic resource archive | Real profile26 authority after 255 earned batches; stored head and journal hash; reopen replays the whole journal |
| Automatic joint archive | Real profile27 crafts until the automatic cut; frame names the continued candidate, not the closing root |
| Reused movement root | Two admitted moves in one batch; final frame differs from each single-move preflight |
| Movement, lifecycle, encounter, care | Refused, acknowledged and administered care; replay of every accepted batch asserts an exact frame |

The constructor refusal is tested by reflection, because Core declares no friend assembly.
`RestorativeAvailabilityTests` already uses reflection in this project.

## Unresolved concerns

1. **Savings are unmeasured.** Framed paths drop the Host's second encode, one per commit.
   Whether that brings recovery under the unchanged 30 seconds is unknown.
2. **Encodes that remain**, all outside ownership or required to stay:
   - The per-action preflight encodes a validation root that is not the final candidate. A
     one-action profile27 commit goes from three encodes to two, not one.
   - `WorldSnapshot.ContentHash` (`WorldSnapshot.cs:327`) encodes its own root on first read.
     A frame cannot seed that cache.
   - Archive sealing encodes the closing root, whose bytes the Host already holds as the prior
     head.
   - The three migration preparations still encode directly.
3. **Retention.** A decision keeps its frame while referenced. `PreparedWorldCommit` therefore
   holds the frame and its exported copy together: two candidate-sized arrays instead of one.
   `SqliteWorldAuthority.Commit` returns the decision to its caller.
4. **Public surface.** `Capture` and `CandidateFrame` are public because Hosting cannot see Core
   internals. Adding a friend assembly was outside ownership.
5. **Mismatch reporting.** The Host maps the constructor's `InvalidOperationException` to
   `AuthorityDispatchException`, like any Core refusal. A defect would surface as a refused batch.
6. **Architecture reference not updated.** The persistence and continuation pages do not yet
   describe the frame contract. Docs belong to Astra.

## Ownership

Edited or created, all within the owned list:

- `WorldCanonicalFrame.cs` (new), `WorldStepDecision.cs`, `WorldStepProcessor.cs`
- `WorldEncounterProcessor.cs`, `WorldEncounterActionProcessor.cs`
- `WorldEncounterLifecycleProcessor.cs`, `WorldCareProcessor.cs`
- `WorldCommitPreparation.cs`, `WorldCanonicalFrameTests.cs` (new), this file

Not edited: `WorldConsequenceProcessor.cs`.

Outside the list: `qualification/frame-worker.trx`, written by the prescribed test command.
Nothing was staged, reset or committed. No permission was denied. No subdelegation.

## Run evidence

- Model: the session environment states `claude-fable-5-1`.
- Effort and context-window selector: not visible inside the session. Take them from the
  client's launch record.
- Usage: the client reported $7.28 of $12 at the final test result. Elapsed time is not visible
  inside the session.
