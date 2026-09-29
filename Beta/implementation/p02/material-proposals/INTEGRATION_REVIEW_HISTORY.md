# Saved proposal integration review

Status: completed worker under integration; no runtime acceptance. The original worker snapshot
is preserved in INTEGRATION_CHECKPOINT.json. The observations below distinguish confirmed gaps,
provisional concerns and missing qualification; the independent critic and repair have returned; final lead qualification is pending.

## Contrasting cases for the integration review

- `CountedAuthorityState.CanControl` binds principal **and session**. Current code has no admitted
  rebind operation. Reopening with the same binding proves disconnect/reopen, not actual session
  rollover. Fresh forged sessions, genuine current lookup and immutable terminal retry content are
  distinct cases. A reconstructed alternative genesis is not a lived control transition.
- The old `ConsentedMaterialApproval` is a simultaneous-submission contract. Inspect any saved-grant
  adaptation for invented fresh sender commands/deliveries, reused creation identities, or skipped
  control. Creation permission must come from an actual earlier authenticated command and replay;
  neither a copied identifier nor a structurally decoded payload proves it.
- A complete step admits one action per actor. Two recipients accepting distinct proposals from the
  same sender in one batch must not consume the sender's action opportunity twice. Also inspect a
  sender's other selected action/movement competing with acceptance. Sequential single-command Host
  examples alone cannot establish correct Core batch accounting.
- A coordinated cut changes representation, not permissions or goods. A pending grant must survive
  that transformation; terminal/reused identities stay occupied even after their closed history is
  no longer inline. Proposal acceptance must reserve transfer-history capacity before selection and
  preserve produced-custody evidence, physical rollback and source ancestry.
- Proposal existence and outcome lookup follow current delivery checks. Compare unauthorized reads
  against existing and absent identities, including a principal legitimately controlling both actors.
  Returned proposal terms do not grant access to the other actor's private command stream/world root.
- Expiry must use actual advancing world time and its documented boundary/order. Administrative
  publication or a reading hold must not accidentally advance an expiry clock. Creation receipt bytes
  stay fixed after expiry, withdrawal, refusal or acceptance.
- Existing profile bytes/semantics, assertions, fixture history and journal/archive budgets remain
  intact. `qualification/check_preservation.py` pins all 477 pre-existing test/fixture files from the
  accepted source revision, plus 23 frozen specs, checkpoint and unrelated predecessor staging.
  This guard does not test behavior or replace codec, actual Host/recovery or native qualification.

The immutable assignment retains the full asynchronous outcome and broader integration path. Partial
components or fresh-world proofs cannot close saved-offer completion or the original retained-world
medicine journey. No production, UI/native, content, art or campaign gate is relaxed here.

## In-progress observations to resolve against the final deliverable

These observations are provisional readings of unfinished code, not accepted findings:

- The proposed profile29 admission currently excludes simultaneous-pair evidence. Review this explicit
  choice against the complete compositional design: preserving profile28 bytes is necessary, but does
  not by itself establish that the successor supports every needed transfer route or an eventual lived
  transition. Do not silently treat an easier alternate interpretation as complete integration.
- The resolver supplies the saved creation grant/recorded sender delivery to the old simultaneous
  approval calculation, after current-binding checks. It invents no new sender command, but the old
  type's documented current-delivery semantics need reconciliation with the saved-permission contract.
  Verify actual replay provenance and the Host's independent current recipient-delivery check before
  considering this boundary sound; comments alone cannot change authority semantics.
- Check structural time consistency as well as replay: an open proposal already due at the admitted
  world's advancing step, or an Expired outcome before its expiry step, must not be admitted merely
  because individual frame hashes and world revision ranges are valid. Host replay remains the proof
  of actual historical timing, while directly impossible snapshot states should be rejected locally.

- The first resolver draft checks ExpectedActorRevision only on acceptance. Generic WorldActionResolver
  checks it for every ordinary command, but the new proposal branch bypasses that resolver. Creation,
  decline and withdrawal therefore need explicit stale-presence cases in the final-source review;
  verify whether the worker later adds a guard or documents a deliberate, justified different contract.
- The current processor expires offers after the advancing boundary, so an action decided from count
  E-1 may complete before expiry at E. Test and document this exact ordering rather than assuming an
  off-by-one defect from the DTO wording. Structural admission still needs the impossible-state checks
  above. Candidate validation uses the eventual clock before that expiry pass; a new strict snapshot
  check must account for this internal validation boundary without weakening final published-state checks.

- The unchanged WorldMaterialCareArchiveAnchor retains only the consented-production capability, not
  the saved-proposal capability or the bounded open-proposal roster. Check final code for a continued
  root that drops/gains the proposal interpretation, silently drops a carried open grant, or introduces
  a grant with CreatedSequence at/before the boundary that the actual closing world never held.
  Ordinary Rebase preserving fields and full SQLite replay are useful positive/authentication proofs;
  they do not themselves prove structural continuation rejects those forged successors. A bounded
  carry context should avoid retaining a closing root/ancestor chain, and must preserve old anchor bytes.

- Expiry clarification from source and the draft reference: tactical processors also advance
  AdvancingSteps, while the proposal owner records expiry at an exploration boundary. A due but
  unterminated open record is therefore reachable and its query/acceptance status is already expired.
  Do not reject all such records as impossible or move expiry earlier without an explicit timing design.
  An Expired terminal outcome before its own absolute expiry count remains a separate impossible-state
  question. The initial broader concern above is narrowed by this evidence.
- The participant query only returns open records/current-segment outcomes. After a cut, the sender's
  own immutable creation receipt proves creation, not the recipient's later resolution; the sender may
  not read the recipient's private command receipt. Review permanent participant-visible resolution
  separately. The draft reference's sentence about older outcomes and authenticated archives must not
  imply a public authorized query that is actually only a privileged ReadMaterialCareArchiveForHost.
  Preserve the full shared-lifecycle outcome even if production-scale historical indexing is separate.

## Executed lead baseline, before repairs

- Full Core: `qualification/proposal-core-20260929T111626Z.json`, 1,684 passing cases in
  191.597 seconds within the unchanged 600-second limit, pinned to the worker final build
  `proposal-build-20260929T110523Z.json`. No source/assembly drift. This baseline does not accept
  the lifecycle gaps, and later runtime changes require affected checks again.
- Preservation guard passed: all 477 old test/fixture files, 23 frozen specifications,
  pre-change checkpoint and unrelated staged predecessor hash unchanged.
- `qualification/WORKER_FIXTURE_ARCHIVE.json` preserves all 32 final-worker fixture inputs in
  a verified archive. These are profile30; no actual Host process-loss run is inferred.
- Worker returned `claude-fable-5-1`, context1M, requested Max effort; verified pre/postflight,
  no permission denials, $24.020394 client estimate / 2,628.443 seconds within $40/3,600 seconds.
  Client estimates are not billing or demonstrated net savings.

The final worker selected profile30 solely to leave an old unsupported-profile29 test untouched.
The lead has not adopted that choice; evolution of a future-version example can be a justified test
change without relaxing archive budgets, assertions or old-format semantics. The old result stays
recorded. Original worker notes and evidence remain immutable.

Confirmed source gaps: the continuation anchor has no proposal capability/carry context, and the
current participant query offers no terminal lookup after archival. The source still performs
actual successful continuation and privileged replay, but those do not fulfill the missing contracts.
A bounded repair draft is prepared; it is not a second dispatched implementation.

## Independent review disposition and repair decision

The read-only critic returned successfully: Fable5.1, context1M, requested Max effort, synchronized
pre/postflight, no denials; $7.15548125 client estimate in 600.516 seconds within $12/900 seconds.
Its original findings are preserved in INDEPENDENT_CRITIC.md. This was source reasoning, not tests.

Accept P1 carry and historical-query findings and P2 impossible-time/participant checks. Enforce
uniform ExpectedActorRevision across all four commands, consistent with the existing permission
revocation action. A broader pose-independent declaration policy would need an explicit shared
contract; silently ignoring the canonical field here is not accepted.

Retain profile30: numbers are nominal and every join uses an explicit allow-list. The skipped29
number carries no runtime semantic cost. This resolves the earlier provisional concern without
renumbering evidence or changing old tests. Remove the two empty V29 comment stubs. Profile30
remains a sibling saved-proposal interpretation; a combined route/session migration is still owed.

The broad Host baseline passed all 200 pinned prior cases with no missing or skipped case:
`qualification/proposal-hosting-20260929T112037Z.json`, 390.206 seconds /600 seconds, identical
worker-final source and assemblies. The scope file's legacy prose said195 while its enforced list
already contained200 names. Correcting that prose/count does not change the filter or selection.

REPAIR_ASSIGNMENT.md is a new bounded corrective assignment based on these concrete reviewed
findings, not an automatic retry of the previous run. It retains all earlier failures/limits and
leaves full native, session scheduling, control rollover, migration and production acceptance open.

## Repair returned; lead integration tests

The repair runtime and all worker test sources exactly match `proposal-build-20260929T114625Z.json`
and its compiled assemblies. The extended archive test was included in its24 passing Host cases,
not a later unbuilt edit (the lead's earlier interim update was conservative). The independent
critic's P1/P2 fixes have executable controls. Review result/usage remain in REPAIR_DELEGATION.json.

Remaining original proofs are being completed by Astra. The pre-existing ConsentedCompanyFixture
already admits extra real human actors and source-backed inventories; it is reused unchanged.
A new shared-control fixture composes the two actors' binding before geography/encounter/care
admission, with no lived-state rewrite, stock or body change. The new tests drive SQLite submission,
actual encounter commands, earned recovery and an intentionally corrupted disposable database.
No runtime or architectural contract changed after the repair; the reference already describes
the tested laws. Original worker reports remain immutable.

The lead build `proposal-build-20260929T120030Z.json` failed because the new test referenced Core's
internal CanonicalEncodingV1 helper. The fixture now fingerprints its own explicit fresh-admission
input using public byte/hash APIs. `proposal-build-20260929T120115Z.json` passes; no production
visibility, assertion, rule or deadline was changed. The expanded Host tests are still running.


### Retained lead test failures and corrected controls

`proposal-host_proposals-20260929T120146Z.json` failed two of 29 cases (27 passed). Neither
failure justified a runtime change. The combined tactical-expiry/readiness test checked debt after
an actual exploration step had already retired it. Readiness now has a separate actual encounter
journey, inspected immediately after release; the expiry journey retains its isolated late-acceptance
control. No recovery amount, time rule or assertion threshold changes.

The attempted concurrent SQL corruption failed with SQLite error5 because the live owner holds an
exclusive lease. The revised control expects that refusal with a one-second writer timeout, verifies
the existing live recorded lookup still succeeds, then disposes the owner and corrupts the disposable
save. Both normal recovery and the exact new recorded-outcome replay must reject that corruption.
This proves lease protection and historical replay validation; it does not execute the live owner's
catch/fence under an injected hardware or I/O fault. No production access seam, relaxed lease,
reflection or altered acceptance gate is introduced. The original failed TRX/log remain preserved.
