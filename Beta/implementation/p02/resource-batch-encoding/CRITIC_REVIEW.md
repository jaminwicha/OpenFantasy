## Critic review: per-batch V1 byte retention

**No blocking issues found.** This rests on reading the current source only. I ran no tests, and without a shell I could not diff against HEAD, so "exactly two production files" and "encoder bodies unchanged" are taken from your brief.

### Confirmed by inspection

| Hazard | Result | Where |
|---|---|---|
| Deep immutability | Batch bytes depend only on get-only scalars, `EntityId`, `ContentHash`, and `Profile`/`Curve` `DefinitionId`/`Version`, which are assigned once in the constructor. `Changes` wraps a private copy. | `ResourceChangeBatch.cs:15-28`, `ProgressionCodecV1.cs:238-241`, `ActorStatProfileDefinition.cs:43`, `LevelProgressionDefinition.cs:27` |
| Cross-owner or stale cache | The batch frame takes no basis or history context, so sharing a batch across histories and the V1/V2/V3 wrappers is byte-safe. There is no static or keyed cache. | `ActorResourceHistoryCodecV1.cs:62-64, 85-88, 105-106`; `WorldResourceHistoryCodec.cs:16-18, 23-28` |
| First-use publication | The CAS loser adopts the winner's array, the array is fully written before publication, and a throwing encode caches nothing. | `ResourceChangeBatch.cs:38-44` |
| Buffer ownership | The private array only reaches `Frame`, which allocates and copies. Core has no `InternalsVisibleTo`. | `CanonicalEncodingV1.cs:39, 48-61` |
| Canonical admission | Decode builds cold batches and never seeds the cache from input bytes, so the re-encode check is still a real encode. | `ActorResourceHistoryCodecV1.cs:82, 52` |
| Archive retention | The anchor keeps progression, care, quests and lifecycle, not the resource history, so sealed batches are not pinned by the anchor chain. | `WorldResourceArchiveAnchor.cs:26-32` |

### Findings

**Medium: the new tests cannot detect canonical byte drift.**
- All four tests in `ResourceBatchEncodingTests.cs` compare new `Encode` output to new `Encode` output.
- The only independent oracle is the env-gated fixture (`ActorResourceHistoryTests.cs:37-43`) compared in `run_checks.py:32-35`, outside an ordinary test run.
- `core-01.json:1249-1264` records 224 passed and three `matches: true`. I did not check that its source hashes match the current working tree.
- Suggested check: pin a SHA-256 of `history.bin` inside the suite.

**Low: the shared-batch test is masked by the cache it tests.**
- `ResourceBatchEncodingTests.cs:49` warms the cache before `input.Clear()` at `:50`, so the assertion at `:54` would pass even if the batch aliased the caller's list.
- Aliasing is only caught incidentally by `batch.Changes[0]` at `:52`.
- Suggested check: clear the input before the first encode and compare against an independently built history.

**Low: the concurrency test does not discriminate.**
- `ResourceBatchEncodingTests.cs:63-76` uses one tiny batch, so the loser path may never run.
- Because encoding is deterministic, a plain non-atomic assignment would pass as well.
- Correctness here rests on inspection. The host does run a background loop (`LocalWorldSession.cs:66`), though I did not trace whether two threads encode the same batch.

**Low: immutability is now load-bearing and the precondition is unstated.**
- Later re-encode checks reuse the first capture: `WorldResourceHistoryCodecV1.cs:49`, `WorldResourceHistoryCodec.cs:46, 81`, `WorldResourceArchiveSegment.cs:60-61`, `ActorResourceHistory.cs:45`.
- This is equivalent today, but a future mutable member or context-dependent batch field would give silently stale bytes and hashes.
- The comment at `ResourceChangeBatch.cs:36-37` and `actor-resources.md:114-120` cover ownership, not this purity requirement. One sentence in each would fix it.

**Info: retained bytes are bounded but unmeasured.**
- From the frame layout I estimate about 0.85 KB per one-change batch and about 0.75 MB for a full 1,024-change segment.
- Each decoded head owns its own copies.

### Remaining qualification limits

- **Native byte identity:** the decisive check is the retained-world fresh-process run showing head bytes and hash identical to the pre-change pins across V27 and both archive variants. Record that equality, not only the timing.
- **Wrapper coverage:** no new test exercises shared batches through the V2/V3 wrappers. `hosting-01.json:1247-1249` records 27 passed; I did not execute it.
- **Performance:** the benefit is unestablished. Decode-heavy recovery still pays one real encode per decoded batch, and the partial codec-baseline-02 counters are not comparative evidence.
- **Handoff:** `AGENT_HANDOFF.md:65-68` correctly claims no gate or performance acceptance.
- **Later edits:** nothing here covers changes made after this review.
