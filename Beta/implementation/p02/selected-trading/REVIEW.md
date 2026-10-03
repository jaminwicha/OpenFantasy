# Independent selected-trading review

A native independent critic reviewed the changed client contracts and the new native fixture source.
No external provider request, build or test was executed by the critic. The review is separate from
the UI and native-fixture authors; its findings do not substitute for execution or human acceptance.

## Findings

1. **P2, stale panel input can address another actor.** Ask/Cancel originally read a fresh selection
   while the clicked control belonged to the previously rendered character. Owner return between
   drawing and clicking could redirect a nonbinding request/cancellation to the home actor, because
   inquiry operations are allowed while input is paused. Bind the displayed presentation and reject
   a scope mismatch before issuing the query; also refuse a draft belonging to another generation.
2. **P2, a late reply can suppress the reopened visit's initial refresh.** An obsolete completion
   correctly withheld its result but still replaced the new visit's initial-read marker. With a
   previously cached null inquiry and the same actor, the newly waiting request could remain hidden
   indefinitely. A completion may alter polling state only for the visit that still owns it.
3. **P2, shutdown must observe facade tasks.** Frame processing stops while the game closes. Joining
   lower session tasks alone does not necessarily join the asynchronous client wrappers that validate
   their results. Drain those wrappers to terminal completion without writing to closed UI.

The independent follow-up verified all three fixes in source. Displayed-scope guards precede queries,
the actual `FarmMarketQueryLifetime` owner preserves new-visit demand across obsolete replies, and
shutdown joins its tracked facade tasks. Changing stalls also requests a fresh read. The new delayed
task tests exercise late completion, changed generation, failed-read suspension and two independently
faulted facades during shutdown. Their first execution passed all four. The real actor-cycle test
also refused a stale draft request. The first managed aggregate retained two separate unfunded-fixture
failures; source review and the passing lifecycle cases do not erase those failures. Final evidence
must match the tested source and binaries. No additional concrete defect was found in this follow-up.

## Scope review

The new native source uses real Trade-only control, the grantee's actual principal/session with the
operated actor and witness, real purchases/sales, finite source intervals, currency conservation and
original-genesis replay. Inspection by known identifier is not history discovery. No complete archive
browser or independent PrivateHistory-only UI is claimed. Full production, physical-input, human and
art gates remain separate. The critic found no additional concrete commerce authority widening.

## Lead execution follow-up

The two managed failures came from unfunded test setup. The selected-case owner now earns the
existing quest reward through ordinary verified commands; the revision counterexample likewise earns
funds and proves that confirmation was executable before actual movement invalidates its review.
All nine new managed cases and all256 selected client/control cases pass. No money is minted and no
runtime eligibility rule or protected test is changed.

The first native run failed before its first Ask input: the open stall was still completing its normal
initial inquiry read. The new probe now waits for that read and the actual enabled Ask control, then
clicks once. Settling and the merchant response share the original five-second budget; the20-second
mailbox and210-second whole-journey limits are unchanged. The failed response retains the correct
selected actor, open stall and200-coin purse. Passive response fields expose inquiry/Ask readiness.
This is probe synchronization, not a retry policy or an increase in accepted latency.

Lead image inspection then rejected the static footer's single-player pause claim in the shared world.
The redundant sentence was removed; the existing dynamic time label, button and offer card already
state the correct policy. A fresh four-player journey passes with the corrected rendered footer.
[Exact scene-text comparison](FOOTER_CORRECTION.json) permits reuse of the earlier home/refusal
command-and-recovery evidence only after checking every other source and every binary unchanged.
Its old footer appearance is excluded. Nine named images are inspected in
[the image record](INSPECTED_IMAGES.json); scrollable supplemental details are not claimed all-visible.
