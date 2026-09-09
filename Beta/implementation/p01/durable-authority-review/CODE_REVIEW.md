# Independent durable counted-authority review

9 September 2026. Reviewer: `p00_core`. Production code remained read only. The reviewer owns only the adversarial tests in `Runtime/tests/OpenFantasy.Hosting.Tests/Review/` and this evidence directory.

## Reviewed boundary and result

Inspected the four Hosting application/storage classes, the added bounded local-input and operator-report helpers, project references and package pins, their Core authority/command/custody dependencies, and the counted authority contract. The shared net8.0 Hosting library is consumed by the net10.0 CLI and net8.0 Godot client. The portable Core remains independent of SQLite and Godot.

The reviewer built and executed **nine independent adversarial cases**, then repeated the same nine against the final Debug build after both findings were fixed, using `--no-build --no-restore`. Both runs passed all nine with zero failures or skips. Their scope is required-write/schema admission, corruption detection, exact unsigned persistence, historical identity collision, current delivery checks before retry lookup, and competing submissions. No unresolved concrete blocking defect was identified in that reviewed scope. This is not an independent execution of the full Core/Hosting suite, process-kill fixtures, native Godot smoke or desktop exports; the coordinator retains those results separately.

## Findings

| ID | Severity | Finding | Disposition |
|---|---|---|---|
| DA-01 | P1 | Application ID, format version and `quick_check` did not exclude unexpected SQLite triggers. A `BEFORE UPDATE world_head` or `BEFORE INSERT occupied_identity` trigger using `RAISE(IGNORE)` could suppress a required write; ignored affected-row counts then allowed an Applied acknowledgement before recovery discovered an inconsistent journal/head/identity registry. | Closed. The owner added exact table-definition/autoindex validation excluding triggers/views/other objects, plus exactly-one affected-row guards on all required inserts/head updates. Three independent cases install suppression triggers for head, journal and identity writes and confirm rejection with no committed journal or identity rows. The owner fixed production before the reviewer executed these regressions: the original exploit was identified by static reasoning, not an executed pre-fix red run. |
| DA-02 | P2 | Schema/application-version admission initially ran before exclusive lifetime ownership was acquired, leaving a validation-to-lock gap in which another connection could change the inspected schema. | Closed. The owner now repeats application ID/version checks and performs exact schema validation after acquiring exclusive lifetime ownership, before integrity/recovery checks. Creation also validates its generated schema under ownership after initialization commits. The initial run preceded this ordering correction; the final nine-case no-build rerun passed against the corrected build. Ordering was verified by inspection; no race-stress execution is claimed. |

## Behavioral inspection

The durable service validates world, claimed principal and the supplied current delivery's actor control before exposing retry lookup. A fresh command additionally requires its immutable origin session to match that delivery. Exact retries compare canonical bytes and return their original receipt before fresh gameplay revision validation; differing bytes under the same principal/command key do not overwrite the first result. World scope is bound by this single-world database. Session and branch are deliberately outside the deduplication key.

Fresh decisions use current persisted control/source/receiver admission and the existing Core planner. Expected targets are restricted to the branch, lot and two storages before private revision checks. Rejections retain the current custody state and still obtain one positive world commit sequence. Successful splits preserve interval quantity/protection and reserve the command, result and new child identities in the same transaction. Genesis/current admission and the historical occupied-identity table jointly reject collisions against identities no longer represented by a new child proposal's immediate context.

The connection uses one serialized owner, explicit immediate transactions, disabled pooling and exclusive WAL ownership. Required changes to canonical state, exact command/receipt bytes, sequence and identity rows are transactional. In-memory state and returned success occur after commit. Exceptions during the write/commit/acknowledgement boundary mark the instance as requiring reopen rather than generating a gameplay rejection. Retrying under a new identity is not the recovery strategy.

Opening restores accepted genesis and replays every journal decision through the same preparation logic, validating sequence continuity, before-state hashes, exact command/receipt/after-state bytes, final head and the full occupied-identity registry. Stored field types, lengths and hashes are checked before managed blob expansion. Unsigned sequence encoding is fixed-width big endian; state counters retain their original unsigned canonical bytes. Unknown or corrupt data fails closed; opening never regenerates fresh sample stock.

The tested package graph pins Microsoft.Data.Sqlite.Core 10.0.12, SQLitePCLRaw.bundle_e_sqlite3 3.0.5 and SQLite 3.53.4. The store rejects native SQLite below 3.51.3, which is the relevant upstream WAL-reset correction baseline. It also exposes actual native version/source ID and configured WAL/FULL/foreign-key/exclusive state for qualification. Packaging all supported desktop exports remains coordinator work.

## Independent cases and evidence

| Case | Observed outcome |
|---|---|
| Suppress required head update with an unexpected trigger | Refused without a committed command or identity row |
| Suppress required command insert with an unexpected trigger | Refused without a committed command or identity row |
| Suppress required identity insert with an unexpected trigger | Refused without a committed command or identity row |
| Change canonical command quantity while retaining stored snapshot hashes | Recovery rejected mismatched replay |
| Delete a split-child identity while leaving journal/state intact | Recovery rejected incomplete identity history and did not rewrite it |
| State revisions above Int64 maximum and a source interval ending at UInt64 maximum | Applied, reopened and retried with exact state/receipt bytes |
| Use a previous command identity as a later command's derived child ID | One durable ResourceUnavailable rejection; unchanged custody before and after reopen |
| Retry through a mismatching principal or session | Uniform dispatch denial before receipt disclosure; correct caller still received the original result |
| Two concurrent callers request six of the same eight units | Exactly one applied and one rejected commit, six moved/two retained, identical state after reopen |

The two retained TRX files name the same nine cases and their separate execution times. `TESTED_ASSEMBLIES.json` preserves the initial test, Hosting and Core assemblies' hashes; `FINAL_TESTED_ASSEMBLIES.json` captures the final tested assemblies after the corrected build. The manifest binds current reviewed source to that final run while preserving the initial historical execution. No test asserts a preferred wall-clock winner between concurrent callers; the committed order is what recovery reproduces.

An additional read-only audit independently matched **274/274** current runtime files against the coordinator's final input hash map, including the generated Godot script UID. `COORDINATOR_INPUT_AUDIT.json` records the exact coordinator manifest hash and audit time. This confirms file identity, not independent execution of that manifest's wider tests or semantic review of every runtime file.

## Remaining integration obligations

- `TrustedCommandDelivery` is upstream identity data, not credential verification. This store's equality checks do not implement authentication. Its admitted controller bindings currently change only through trusted initial state; revocation/rebinding and real cross-session reconnect are not implemented by this transfer-only API. A future control transition must be journaled/replayed and its receipt access policy decided explicitly.
- `InspectForHost` exposes privileged complete state. It must not become the ordinary Godot/player response. Real gameplay inventory/knowledge views require the existing permission-scoped projection boundary and acquisition semantics; native smoke is not proof of that integration.
- This is a finite counted-domain authority. Actor IDs here are participation/admission references, not full NPC state. There is no extraction, transformed provenance, cross-branch export ledger, broad terrain mutation, production job, scheduler/RNG mutation or projection outbox in these transactions yet.
- Original content import/realization targets remain separate work. These qualification inputs do not prove migration of the predecessor's characters, quests, recipes, entities or assets. Native local smoke also does not close cross-platform export or production-device qualification.
- Recovery currently visits every retained command and materializes its historical identity registry; each command retains a full after-state. That preserves this scope's history but does not qualify unbounded replay latency, archive capacity or world-scale memory. Checkpoints/deltas, disk-pressure behavior and measured workloads remain necessary.
- Process death before/after commit, second-process writer refusal, native exports and storage fault behavior require their own executed evidence. Exception rollback alone is not a process-kill test, and a process-kill test is not a physical power-loss guarantee.

These limits describe what the present code actually does and where the first playable integration must continue. They do not discard the full P01/P02 campaign requirements.
