# Integration review

Astra reviewed the four owner diffs and their unchanged encoder dependencies. A bounded independent
native critic (`world_continuation_contracts`) found no concrete runtime defect and executed no tests.
It confirmed immutable nested facts and defensive collection ownership, first-read/retry semantics,
atomic publication, separate revoked owners, unchanged bytes and retained admission/permission checks.

The six focused tests cover cold/repeated and concurrent reads, revocation, constructor mutation,
same identity with different facts and codec roundtrips. Expected values come from public codec frames.
A valid public input cannot trigger the encoder failure path; retry behavior is established by code
inspection, without adding an artificial production seam.

The critic and Astra both identified unbounded thread joins in the new concurrency test. Astra changed
readers to background threads and bounded each join by the existing 30-second reader limit before
integration qualification. The production diff remains exactly the reviewed worker code. The worker's
137-case passing report predates this test-only improvement; final evidence is recorded separately.

Reviewed runtime SHA256s:

| File | SHA256 |
| --- | --- |
| RestorativeRemedyDefinition.cs | 8a91bc75731bb6e99b2200dea16c2a5d252fbfa75cb09a8fed596fd7bb54e98c |
| RestorativeUseDefinition.cs | 4506d23aab28083d31cc8f8a5de1bbdd76bc0821ffd9c0bc1963c7b589a499ba |
| CareAgreement.cs | 499274cd6ab294b8e499fc74a1c486c3ba2a21b8d3b1fe32b0866df224c7dc3f |
| CarePermission.cs | ee345c1f21931fc2dbcb6bc9039fb42370ef4397e3aba7bbf0f067a3e9d68797 |

Retaining hashes increases each owner's live storage by one digest (two for permissions). Concurrent
first readers may duplicate computation before publication; all subsequent readers reuse the winner.
No process-wide cache, validation shortcut or authority token is introduced. Repeated reads now return
the same immutable hash reference; its existing value-equality semantics remain unchanged. Astra also
inspected the 21 reference-equality uses in runtime source and client scripts; none treats these
care digests as distinct mutable identities.

Architecture references describe ownership and grant/revocation distinction in timeless prose.
Performance, exact fixture compatibility and external recovery require executable evidence; this review
alone does not establish them.
