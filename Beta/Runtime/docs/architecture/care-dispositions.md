# Pending-care decisions

## One request and one terminal result

[CareSnapshot](../../src/OpenFantasy.Core/Care/CareSnapshot.Dispositions.cs) owns an acknowledged
request and its single terminal result: a real treatment or a
[CareDisposition](../../src/OpenFantasy.Core/Care/CareDisposition.cs). A patient may cancel its
request; the named helper may decline it. Neither closure revokes the underlying agreement or a
separately authored recovery permission. A disposition grants no health, consumes no dose, advances
no time, clears no debt and does not acknowledge an encounter outcome.

The care owner preserves these exact relations:

```text
TotalAcknowledgments = TotalTreatments + TotalDispositions + PendingCount
Revision = TotalAcknowledgments + TotalTreatments + TotalDispositions + Ordinary.Revision
ArchivedAcknowledgments = ArchivedTreatments + ArchivedDispositions
PendingCount is zero or one
```

[WorldCareActions](../../src/OpenFantasy.Core/Worlds/WorldCareActions.cs) authenticates the exact
request, episode, care revision and deciding participant. The ordinary command envelope checks
world, branch, control and presence revision. Cancellation and decline are nonbodily decisions:
zero HP, missing medicine and distance do not prevent them. Reading and excavation holds still apply.
A successful decision publishes its immutable disposition and receipt in one care transaction.
A competing stale terminal decision receives a separate refusal; it cannot replace the first result.

[General recovery](general-recovery.md) remains a separate exploration operation after request closure
and encounter completion. It needs its own permission, eligible helper, real dose and charged work.
A later defeat can create another request; the same defeat cannot be acknowledged again through
another agreement or after archival.

## Chronology, archives and formats

The continuation keeps two distinct bounded frontiers per patient. `CarePatientWitness` retains the
latest actual treatment for resource chronology. `CareClosedDefeatWitness` retains the exact latest
closed acknowledgment and exactly one treatment or disposition, preventing defeat reuse. Cancellation
cannot replace resource evidence with fictional healing. Full historical records and permanent
identity occupation remain authenticated by coordinated archives and journal replay.

Terminal headroom is reserved before acknowledging care. Disposition-only history counts as care
progress. Cuts carry an outstanding request once, preserve cumulative totals, and require subsequent
records to follow the complete closing world boundary. The compact frontier is not a substitute for
historical authentication or an unlimited archive.

[WorldCareDispositionAdmission](../../src/OpenFantasy.Core/Worlds/WorldCareDispositionAdmission.cs)
selects profile 38 over an unplayed recovery world, pinning the fixed disposition law even with an
empty history. [CareCodecV5](../../src/OpenFantasy.Core/Care/CareCodecV5.cs) owns the corresponding
inline/continued encoding, totals and closed-defeat witnesses. Its independently bounded continuation
is at most 64 KiB; legacy V3/V4 retain their 4,096-byte limit. Root/step 38 and action 21 retain the
existing care batch kind and schema-nine durability. Older profiles and codecs refuse the new
interpretation. Opening a lived save is not migration.

## Host scheduling and control

[LocalWorldSession](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.cs) adjudicates an
already queued care decision before selecting automatic host treatment. Refused new decisions cannot
starve treatment: the host can select the accepted helper operation in the remainder of that cutoff.
It never retries the rejected command. Successful acknowledgment and treatment remain separate
transactions. No fresh choice is generated without a ready gameplay port.

Human helpers receive explicit treatment and decline choices. A host helper policy can select only
its own lawful treatment; it never conscripts a player helper or invents a decline. Automatic decline
requires a separate explicit policy. Pending care permits safe restoration of its patient's or helper's
human control only when other accepted-work restrictions allow it. Restoration preserves the request,
starts the port unready and requires fresh readiness before execution.

## Observation and input

[LocalCareView](../../src/OpenFantasy.Hosting/Simulation/LocalCareView.cs) gives the patient its own
cancellation and gives the helper its own treatment/decline choices. Only the helper sees its captured
dose terms. Other actors receive no private decision. Closure views distinguish treatment, cancellation
and decline, including retained closed-defeat evidence after a cut.

Observation body version 4 explicitly carries `SupportsCareDispositions` even without an active request.
Legacy false/null additions are omitted. Strict canonical decoding checks participant ownership,
request/episode consistency, closure status and private dose exposure; presentation never authorizes
an action. A replica replaces a pending offer with the observed terminal result.

The Actions wheel exposes relevant care choices with icons and explanatory detail. Inspection commits
nothing. Each choice captures its actor, request, episode, revisions and terms; a changed observation
invalidates the captured choice rather than silently substituting another. Cancellation wording states
that no treatment was given and that encounter acceptance remains separate.
