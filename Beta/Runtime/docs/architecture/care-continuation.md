# Care continuation

[General recovery](general-recovery.md) uses the same bounded chronology through Care V4, with
distinct definition/use and continuation markers. Recovery permissions and zero-before treatment
witnesses survive cuts; legacy Care V1–V3 refuse that interpretation.

The explicit [pending-care decision law](care-dispositions.md) adds Care V5 dispositions and a separate
latest-closed-defeat frontier. Its treatment witness remains independent; older interpretations retain
their acknowledgment/treatment-only relation and unchanged byte bounds.

## Bounded domain frontier

[CareContinuationContext](../../src/OpenFantasy.Core/Care/CareContinuationContext.cs) captures an
actual closing [CareSnapshot](../../src/OpenFantasy.Core/Care/CareSnapshot.cs). Its `Continue()`
operation opens a new bounded record window without advancing the cumulative care revision,
restoring health, consuming material or accepting an action. Completed ordinary uses and completed
acknowledgment/treatment pairs leave the active window. Their cumulative totals remain exact.

The context retains scope, the closing care hash and revision, an ordinal, exact catalogs and
permission grants, persistent revocations, a closed-position watermark, the sole pending
acknowledgment and the latest completed treatment for each patient. Patient witnesses are bounded
by the admitted agreement catalog. No context retains a previous snapshot, parent context or
recursive history. Current HP and MP remain owned by actor progression; a historical treatment
witness is not an alternate physiology owner.

The pending acknowledgment stays in the active collection and counts once. Its later treatment
must close that exact obligation, preserve the original defeat/timing relation and follow the
closed boundary. Ordinary use cannot occur while the obligation remains pending. Latest patient
treatment witnesses prevent reuse of an older defeat or a nonadvancing patient resource revision
after earlier records leave the window.

Catalogs and original permission scopes remain exact. A revocation persists through every cut and
contributes to the ordinary-care revision once. New operations follow the closed world-sequence
boundary; ordinary command ordinals still distinguish operations inside subsequent publications.
A closure never permits insertion into an already closed publication.

`CanCapture` distinguishes care progress from representation work. An inline owner may be closed
even when empty, because a material-only boundary can require an initial care context. Reclosing
an unchanged continued owner is refused. A coordinated owner can retain that existing care context
when care has not progressed. `ValidateClosure` checks the explicit transition; normal successor
validation preserves one context and its append-only records.

The public `Acknowledge`, `Treat` and `WithOrdinary` methods construct structural successors. They
preserve the continuation and care chronology; they do not authenticate a command, spend a dose or
change HP. `WithOrdinary` checks ordering against aftermath records as well as ordinary command
positions. `ValidateSuccessor` rejects backfilled completed pairs even when a proposed final history
would be internally chronological.

## Canonical interpretation and authority

[CareCodecV3](../../src/OpenFantasy.Core/Care/CareCodecV3.cs) encodes continued care. Decoding requires
the exact externally supplied context and canonical agreement with it. The structural context
cannot authenticate its own historical truth. V1 and V2 keep their original formats and refuse
continued owners; an inline parent cannot silently absorb a continued ordinary-care child.
Existing catalog, history and overall byte bounds remain unchanged.

The bounded frontier does not retain every old command or entity identity. Complete world/Host
authority must still check the permanent identity ledger and authenticate the closing world through
actual accepted commands and retrieved archives. Context hashes identify evidence; they do not
create it. A context alone cannot mint a dose, authorize a helper or prove historical HP changes.

## Complete-world binding

Care continuation is a standalone domain contract. World profiles25/26 retain inline care; their
[world admission](../../src/OpenFantasy.Core/Worlds/WorldSnapshot.cs) refuses a continued care owner.
The explicit [coordinated world](coordinated-world-continuation.md) joins care closure with material
claims and [causal resource history](world-resource-history.md), archives the exact closing root and
publishes the resulting owners in one durable transaction. Profile27 selects this interpretation at
fresh genesis; opening a lived profile25/26 save preserves its existing interpretation. Resource-only rotation preserves the complete
care record window and does not invoke this protocol.

The care watermark records the latest care operation, which can predate the closing world revision.
The coordinated world anchor separately enforces that closing revision, including material-only
cuts that retain an unchanged care context. New acknowledgments, treatments, uses and revocations
follow the world boundary; the exact carried pending acknowledgment and closed revocations remain valid. The component watermark cannot replace that world boundary.

That composition must preserve pending aid and reserve its treatment/consumption headroom, account
for every potential writer before a selected batch, and retain full rollback. Structural care
continuation does not establish material provenance, durable archive authentication, unlimited
storage, native behavior or production-scale performance.
