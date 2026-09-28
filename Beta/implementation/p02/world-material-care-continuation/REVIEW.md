# Care-frontier integration review

Scope: standalone continued care and explicit legacy-world exclusion, plus fresh finite-supply stress
preparation. This is not review acceptance of complete-world material/care continuation.

Fable implemented the domain unit with the requested model/context/effort. It stopped near its finite
estimate cap, reporting four tests and missing coverage honestly. Raw results remain private; the
[record](delegation.json) preserves model, context, usage and disposition. No automatic paid retry.
Astra reviewed the diff, supplied missing adversarial tests, and owns all executed checks and integration.
An independent native reviewer (`world_continuation_contracts`) performed a bounded read-only audit;
this was a distinct native review, not a claimed Fable review or a resumed external session.

## Findings and disposition

1. `WithOrdinary` checked only the ordinary owner's append order. It could insert a new use before
   completed aftermath care. The independent critic found the same defect as the lead. The retained
   `chronology-red` execution demonstrates the missing exception. The append path now checks complete
   cross-care positions while reusing the already preserved aftermath records.
2. `ValidateSuccessor` preserved existing aftermath records but allowed a chronologically valid pair
   to be backfilled before them when ordinary care was admitted. `chronology-pair-red` demonstrates
   this. New aftermath records must follow the prior maximum position across both care modes.
   Ordinary commands may still occupy successive ordinals of one later world sequence.
3. The frontier's watermark is the latest care operation, not the actual closing world revision.
   The future coordinated world anchor must enforce its own closing sequence, including material-only
   cuts where care does not progress. The architecture reference records this binding obligation.

The independent reviewer found no further source defect in counters, pending retention, patient
witnesses, revocation persistence or exact-context codecs. Executed test results, rather than this
source review, establish regression status. Full-world archive, material, crash and native gates remain
open. Fable's incomplete tests and the lead's failed red regressions are retained, not waived.
