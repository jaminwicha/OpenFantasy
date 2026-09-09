# Independent custody code review

Reviewer: portable-core agent. Date: 2026-09-09. Coordinator owns the reviewed custody implementation; this reviewer does not edit runtime files. Review is scoped to the finite branch-local counted-lot planner, not full gameplay custody or production readiness.

## Findings delivered to the implementation owner

| ID | Priority | Finding | Required resolution | Status |
|---|---|---|---|---|
| CUST-01 | P2 | `CustodyTransferPlanner.Plan` initially checked only its four mandatory revision targets, silently ignoring any extra `ExpectedRevisions`. A caller could include a stale additional lot/storage expectation while receiving a successful candidate. | Validate every declared expectation whose target is supported by the complete supplied group, or reject unsupported extra expectations explicitly. Add stale and unknown-extra regression cases. | Closed; code correction and relevant regression independently verified |
| CUST-02 | P2 | Split-child collision checks initially covered world/branch, lots, storages and selected command identities, but omitted source identities already present in source intervals. A newly created child could alias a known source reservoir identity. | Include all known source identities in collision checks. Clarify definition-identity namespace policy and retain a source-ID collision regression. Whole-world registry validation remains separate. | Closed; code correction and relevant regression independently verified |

## Reviewed behavior that is coherent

The single-origin interval model is correctly limited to unchanged counted units. It keeps origin identity independent of transfer command and branch, partitions a prefix during partial transfer, preserves the remaining lot identity and creates at most one documented child. Complete supplied custodian contents are validated before capacity is used. Duplicate lot/storage identities, over-capacity contents, overlapping same-origin intervals and inconsistent source definitions reject at snapshot construction.

Planning validates branch scope, mandatory revisions, source custodian, reservations/equipped state, available quantity and finite destination capacity. Whole transfers keep identity; split transfers conserve source interval union. Immutable source records and copied private dictionaries/read-only collections prevent direct mutation through returned data. Candidate application checks both base revision and canonical base hash and returns a replacement value; it does not publish a durable result. Canonical hashing includes world/branch, snapshot revision, storage capacities/revisions, lot identity/custodian/definition/version, source interval, lot revision, protection and optional reservation, with sorted storage/lot collections and explicit framing.

Favorite/heirloom/user-reserved designations are preserved through explicit transfer. This does not grant authority to override protected automatic ingredient selection, legal ownership, equipment rules or another job's reservation. Authentication, global identity registry checks, custody authorization, complete authoritative snapshot preparation, durable receipt/retry application, general material transformations and the world transit allowance ledger remain future responsibilities.

## Final disposition and executed evidence

**No remaining concrete P0/P1/P2 defect was identified within this bounded custody increment.** Both findings were corrected by the coordinator and independently re-reviewed. `ValidateDeclaredRevisions` checks every declared target and rejects unknown targets; `ContainsEntity` now includes source, definition and reservation identities. Regression coverage passes for both corrections.

The reviewer independently executed the stable coordinator-built Debug assembly using SDK 10.0.401/runtime 8.0.31, net8.0, `--no-build --no-restore` and the custody namespace filter: **24 passed, 0 failed, 0 skipped**. The runner reported 44 ms test duration; this is not a performance qualification. The [TRX execution record](test-results/custody-independent.trx) retains the actual individual outcomes. The reviewer also ran the independent Python custody oracle and compared its stdout byte-for-byte with the retained fixture; they matched.

Reviewed tests cover successive splits near UInt64.MaxValue, exact interval/quantity conservation, source/destination/lot/branch revision overflow, capacity rejection with existing receiver stock, scope/custodian mismatch, required and extra revisions, competing candidate rejection, protected/equipped/reserved states, known identity collisions, immutable copies, canonical bytes and bounded input validation. The [input and validation manifest](REVIEW_VALIDATION.json) records exact source, test, oracle, contract, toolchain and assembly hashes. The separate [source evidence](SOURCE_EVIDENCE.json) preserves the Wren/recipe linkage checks and inspected predecessor files.

The final coordinator contract correctly distinguishes local overlap checks from global admitted-source uniqueness, conserved counted lots from heterogeneous crafting claims, immutable candidate adoption from durable commit, and finite working-group limits from shipping-world limits. Parent source hashes may change later; this verdict binds only the captured inputs. Full-suite counts reported by the coordinator are not presented as this reviewer's independent execution.
