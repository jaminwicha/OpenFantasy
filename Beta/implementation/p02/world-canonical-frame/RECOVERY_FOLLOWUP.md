# Remaining recovery cost

The serialization optimization removes 1,569 duplicate world encodes on the exact retained revision-
1569 ledger. The [comparison](qualification/recovery-comparison.json) records unchanged inspection,
source/database preservation, all instrumentation counters and limitations. Cumulative replay
allocation fell 24.2%; admission call counts did not change. Inclusive timings overlap, and separate
runs on a shared machine cannot isolate wall-time savings. Peak/live memory was not measured.

The uninstrumented normal command still exceeded 30 seconds. The full crash matrix has not passed,
and its original deadline is unchanged. Before/after process-kill and native continuation remain
required. Preserve every failure rather than treating a later isolated completion as qualification.

## Next bounded candidates

Resource admission consumed 6.666 seconds in the new instrumented run and 1.623 GB cumulative thread
allocation across 2,092 calls. Inspect its internal cost before introducing cross-root admission reuse.

1. `RestorativeRemedyDefinition.Version`, `RestorativeUseDefinition.Version`, `CareAgreement.Version`,
   and `CarePermission.GrantVersion`/`Version` encode and hash on every read. These sealed owners have
   immutable properties, and causal validation repeatedly reads them. The prepared
   `qualification/profile_care_versions.py` instruments these getters in a disposable copy. Measure
   their contribution before considering a lazy digest retained by each exact immutable owner.
   Keep original first-read/refusal semantics and thread-safe publication. A new permission after
   revocation needs its own complete version; its unchanged grant version is a distinct fact.
2. `WorldResourceAdmission.ValidateRestorativeUse` linearly searches completed uses for each resource
   batch; `ValidateOrdinaryDose` scans consumed items. `WorldRestorativeAdmission` also scans consumption
   rows per use. A bounded lookup index built once within each complete validation could avoid
   repeated searches while still executing every causal check on every root.

These are source-based hypotheses, not measured substage results or accepted implementations. Prefer
exact immutable-owner digests or scoped indexing over a broadly keyed admission certificate if
measurement supports them. Preserve duplicate-ID refusals, exact identity equality, deterministic
batch ordering, absence/error cases and immutable ownership. An index or digest does not skip
admission. Arbitrary public roots, actual retrieved archive bytes, source issuance, controls, care
permissions/revocations, chronology, seeded attack draws, rewards and capped healing still need all
existing checks. Avoid a general ECS, global cache or new authority.

Measure exact bytes/receipts/identities and total allocation/time against this same ledger, with a new
checkpoint before code changes. New source requires relevant adversarial tests and independent review
before the unchanged external recovery campaign. Broad validation certificates remain a more complex
alternative needing complete dependency/invalidation proof, not the default next step.

## Executed getter diagnostic

`qualification/care-version-profile-01` completed on the same preserved ledger and returned the exact
same inspection. Original source and database hashes remained unchanged. It added getter-level
instrumentation to a disposable copy, with no change to retained application binaries.

| Immutable getter | Calls | Inclusive time | Cumulative thread allocation |
| --- | ---: | ---: | ---: |
| Ordinary use version | 470,335 | 1.528 s | 850,440,520 bytes |
| Remedy version | 267,835 | 0.576 s | 237,851,832 bytes |
| Permission grant version | 199,920 | 0.416 s | 187,148,888 bytes |
| Agreement version | 4,132 | 0.013 s | 3,841,888 bytes |

The complete permission-version getter was instrumented but had no recorded calls in this journey.
Outer timing stages overlap these getters. The command completed in 26.749 seconds under the
separate diagnostic bound; that is not a pass of the unchanged external crash campaign, and the
original 30.021-second timeout remains authoritative failure evidence.

The measured next candidate is **lazy canonical digests on exact immutable care definition/grant
owners**, before a cross-root admission certificate or unmeasured indexing refactor. Preserve the
existing first-read encoding/exception boundary, defensive construction of supported forms, exact
bytes and safe concurrent publication. A revoked permission is a different immutable owner; its
complete version and original grant version must remain semantically distinct. Do not reuse by
identity alone or by a subset of fields. Test cold/repeated/concurrent reads, encoded equivalence,
constructor-input mutation resistance and permission revocation, then measure the same real ledger.
No getter cache has been implemented by this packet.
