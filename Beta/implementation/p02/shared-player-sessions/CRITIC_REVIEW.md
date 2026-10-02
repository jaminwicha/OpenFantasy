# Independent shared-session critique

Native read-only critic `shared_sessions_critic` reviewed the actual changed host/connection/trading
source and tests. No builds or tests were run by the critic, no source was edited, and no external
provider was called. It found two actionable source-traced defects:

1. Side-zero observers could repeatedly submit unavailable combat choices, selecting rejected
   decision cutoffs instead of permitting the real participants' queued resolutions to progress.
2. Hostile opportunity selection depended on live ports, so closing the targeted resident's port
   suppressed an eligible NPC initiation while another ready player kept the world running.

The lead confirmed both paths. Scheduling now refuses combat input from actors without an eligible
unfilled due decision; all admitted player residents remain targets of declared hostile opportunities
independently of port membership. Added tests exercise observer spam during real queued combat and
closing the target after actual walking into the chief's reach. Qualification reports establish the
executed results; this source review is not itself a test pass.

The critic also checked simultaneous decision batching, authority serialization, controller checks,
inquiry isolation and close/reconnect ownership, reporting no further substantiated defect. A speculative
combined batch identity conflict was not reported as a separate finding. One independent review at this
coherent boundary is retained; it does not close networking, native, independent-time or human gates.

A targeted follow-up confirmed world-resident threat selection and found that out-of-phase lifecycle
requests could use the same starvation path. Admission now excludes unsupported phase requests before
queueing and excludes outcome readings from actors with no unread outcome, except genuine recorded
former readers. Care retains the patient adjudication semantics described below. The
observer regression tries entry and premature outcome acknowledgment as well as Wait. Exact committed
retries are retrieved before those checks. The pre-existing legacy premature release in Confrontation
still receives its recorded refusal; no old fixture/assertion was changed.

The broad retained regression exposed an overstrict admission check: a genuine reader's stale outcome
selection after completion must obtain its durable refusal, not an adapter exception. The original run
is retained (566 passed, one failed). The lead admits a recorded former reader's episode unchanged and
lets the authority reject it, then continues the already-due cutoff once so repeated stale readings
cannot stop exploration. The new shared journey submits three such readings and checks their exact
Unavailable receipts plus three advancing steps. The existing FarmSubmissionSeam test is unchanged.
A separate WIP checkpoint preserves the pre-correction state; passing evidence is required below.

After the correction, build and all 24 focused cases pass. The broad repeated run reached its unchanged
600-second limit without a terminal test report. The same unmodified FarmSubmissionSeam case then
passed in isolation (3.63-second process, 60-second diagnostic bound). That proves the specific
compatibility repair, not the cause of the broad timeout. The next broad run uses the same selection,
binaries and 600-second limit with detailed console logging. No failed result is replaced or erased.

The detailed broad repeat passed all 567 cases in 406.07 seconds, within the same 600-second bound,
with source unchanged. It includes the unchanged compatibility case. The previous timeout remains
unexplained; no shared-machine contention or test-logging cause is asserted. Current qualification
still requires its remaining Core, native and durability evidence before acceptance.

The retained outcome family found two more overstrict care-admission failures. Reading does not remove
an existing patient's ability to submit care before the next completion cutoff, and a late patient care
selection after completion still receives a durable refusal. The lead removed that accidental policy
change: genuine outcome care choices retain their own cutoff, while rejected care outside Outcome
continues the already-due world cutoff once. The new shared journey now repeats both stale readings
and stale care three times, proving six normal advancing steps and no new care obligation. Existing
outcome assertions are unchanged. The pre-correction state is in CARE_CHECKPOINT.json.

A bounded follow-up on the care repair found five remaining issues: observer refusal could overtake
another ready patient's queued care; repeated acknowledgment could starve already accepted treatment;
the care hold did not bind the requested episode; generic staging exceptions let observers delay stage
progress; and checking only Last could reject older genuine readings before durable adjudication.
The lead moved protection into scheduling instead of duplicating Core lifecycle legality. Current-episode
patient care is prioritized, accepted treatment owns its cutoff, the retained legacy exception requires
its own initiator/episode, and historical/unknown selections reach ordinary authority rejection. The
new shared tests cover stage spam, observer traffic at completion, competing queued patient care,
accepted helper work despite queued acknowledgments, and stale/unknown selections without extra time.

The first added care fixture correctly failed because its observer had no scenario in which it could
be a patient. The corrected fresh calibration declares both the agreement and its own neutral-helper
scenario and binds both in its admission identity. No production catalog or law changed. One test run
after a failed fixture build is explicitly invalidated in INVALIDATED_FOCUSED.json; subsequent focused
and broad invocations now require a successful current-source build and exact actual binaries before
execution. That old passing result is not qualification evidence.

The next broad run completed with 566 passes and two failures in the unchanged CareActionOfferTests.
Those tests preserve the original patient's already-presented stale offer as its own rejection cutoff,
without simultaneously treating the patient. The critic recommended a durable bounded exception:
matching original patient/agreement/episode, only while world revision equals Pending.WorldSequence.
The lead implemented this boundary; a committed rejection closes it, while failed preflight without
a commit cannot defer treatment. New shared-care cases exercise both uninterrupted and reopened
worlds, competing observer traffic, one preserved refusal and subsequent helper priority. Qualification
of this correction passes build, 26 focused cases, 34 retained outcome cases and all 569 broad
Hosting cases in 580.86 seconds under the unchanged 600-second bound. Earlier failures remain
evidence, not weakened tests; fresh native/durability qualification is recorded separately.

The first retained-journal authentication invocation stopped before tests because the thin adapter
omitted RETAINED_NATIVE.json. The lead supplied a byte-identical copy from the prior accepted packet,
with both source/copy hashes recorded. The unchanged verifier still authenticates every referenced
artifact and runs all named families. No runtime, test, fixture expectation or deadline changed; the
original failed setup report remains retained.
