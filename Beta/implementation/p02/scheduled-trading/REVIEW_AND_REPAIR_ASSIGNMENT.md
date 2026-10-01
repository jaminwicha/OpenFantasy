# Independent integration review and completion

This is a new bounded assignment reviewing and completing the delivered scheduled-trading increment,
not a retry of the successful first request. Use exactly `claude-opus-5-5[1m] --effort max`, at most
3,600 seconds / $30 client estimate. No nested agents, model fallback, paid retry, installs, staging
or commits. Astra integrates. You are not alone: preserve all other changes and the predecessor index.

First create `REVIEW_AND_REPAIR_RESULT.md` with an in-progress note. Independently inspect the returned
implementation against the original ASSIGNMENT and current architecture. Record pre-change findings
and evidence, distinguishing lead hypotheses from your independently found issues. Challenge the
whole relevant integration, especially private knowledge, current control, exact terms, scheduler
arbitration, pause/hold/recovery, bounded reconsideration and continuation. Do not merely approve the
existing test count or limit the review to the three lead notes. Then implement the necessary bounded
corrections and missing acceptance contrasts. Astra reviews your changes; describe the independent
review as a review of the input delivery, not an independent review of your own subsequent repairs.

## Evidence and review targets

The preserved delivery is recorded in DELIVERY_CHECKPOINT.json. INITIAL_CHECKS.json pins the final
worker build and 173 distinct managed passes: 50 exchange and 127 session cases, with four new cases
overlapping both selections. No initial run failed. Core source, all 532 pre-existing test files,
23 frozen specs, source database and predecessor staging remain exact. The worker report names
remaining gaps. LEAD_REVIEW_NOTES.md records hypotheses from the draft; recheck against the actual
returned source and reproduce defects before fixing them. Do not weaken old assertions or checks.

1. **NPC reconsideration.** An unchanged own-holding/balance key appears to suppress a shopper forever
   after a remote/unavailable or out-of-stock response. An alive NPC must resume useful trading when
   circumstances become favorable. Prove actual departure/arrival or return to reach without mutating
   the shopper's wallet merely to perturb a request key. Also prove a legitimate merchant stock/funding
   change can restore trade when relevant. Use bounded deliberate reconsideration and/or genuinely
   available evidence; do not scan hidden merchant stock from the shopper, auto-refresh distant price
   memories, issue a command every tick or silently retry a refused acceptance under new identities.
   Distinguish a new NPC decision from exact command retry and from new player confirmation. Keep the
   policy finite, deterministic where it affects selected inputs, documented and restart-safe. Prove
   both actual NPC buy and sell directions; the delivered cases exercise only selling.
2. **Action boundaries.** `ValidateTradeSelection` appears to admit paid/market commands during a
   reading hold, after which batch construction can throw outside preview handling. Reproduce through
   a real session and hold, then ensure invalid selection refuses without publishing a forbidden batch
   or faulting the session. Exercise paid acceptance and a market command with a later lawful contrast.
   Check care/encounter/consequence boundaries where the same failure mechanism applies. A nonbinding
   inquiry may remain queued over a hold; prove that contract separately from actor commands. Cover its
   actual expiry and cancellation/stop semantics at the relevant boundary without changing time rules.
3. **Live composition.** Check profile-33 gifts/care instead of claiming ordering among unreachable
   controllers proves composition. The existing material-selection and NPC gift controller gates
   still name only profile 30 while the core explicitly admits saved proposals in 30/32/33. Add the
   necessary host bindings and actual session contrasts for gifts alongside configured trading,
   including conflicting partner work. Preserve meaningful consent and care semantics, older supported
   profiles and unsupported-profile refusals. No Core or codec change is needed merely to bind already
   admitted host actions. Inspect dependent preparation/selection/view gates so a nominally enabled
   feature is not hidden or rejected at another layer. Keep the scope to this application integration;
   do not redesign unrelated controllers or claim full NPC cognition.
4. **Complete the risk evidence.** Cover a world-refused merchant issuance, a refused NPC acceptance
   with no per-tick command flood and later useful behavior, and the participant view of an offer
   superseded by another shopper's actual fill. Existing FIFO contention already passes; preserve it
   and check the documented service bound against every exclusion, not just paid acceptances. Test
   private/foreign projection and transaction-owner conservation where the new behavior could change
   them. Retain actual finite funding, gifts, routes, ordinary work, cuts and reopening. A helper call,
   direct root edit or test-only selector shortcut cannot replace a session-cutoff proof.
5. Reconcile timeless architecture with actual behavior and its limits. In particular audit claims
   that every unconfigured/older-profile batch is unchanged, and that a service bound accounts for all
   ways a merchant can be busy. Preserve frozen specs and existing gates. Record facts, fixes, exact
   evidence, failures and genuine remaining obligations in your result; do not narrow the assignment
   to the paths already passing or turn unrun mandatory contrasts into acceptance.

## Ownership and tools

You own the same hosting `Simulation/` and `Worlds/` seams required by the original assignment and
the new `tests/OpenFantasy.Hosting.Tests/WorldExchange/Scheduled/` files. You may modify those newly
delivered tests/helpers or add cases; all 532 pinned older test files remain read-only. Core, client,
predecessor, content, ecosystem and frozen specs remain read-only. No native launch or focus changes.

Architecture ownership: scheduled-trading.md, world-markets.md, world-paid-exchange.md,
runtime-and-authority.md, presentation-and-interaction.md, material-proposals.md, ordinary-care.md,
aftermath-care.md and README.md. Use the immutable copies in the private task directory as input
snapshots. This packet's only owned report is REVIEW_AND_REPAIR_RESULT.md. Handoff, assignments,
lead notes, pins, wrappers, original RESULT and previous evidence remain read-only.

Read/Glob/Grep for discovery, Edit/Write for owned deliverables. The only Bash invocations are the
exact wrappers below. Do not use `ls`, diagnostic shell, pipes or substitutes merely because a native
client may permit a read-only command. The first worker reported two ungranted read-only listings;
no mutations or denials were reported, but that is not evidence of a strict command sandbox.

Run serially, with Bash timeout 600000, observing the terminal report before starting the next:

python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/scheduled-trading/run_integration_checks.py build
python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/scheduled-trading/run_integration_checks.py new-host
python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/scheduled-trading/run_integration_checks.py session-host
python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/scheduled-trading/run_integration_checks.py focused-host

Begin checks early enough to correct failures inside this finite request. Preserve failing reports
and observe unchanged thresholds; no background handle is a pass. Never open the retained source
SQLite directly; use copies. If a tool is denied, stop that path and report it, without bypass or
broader permissions. The lead will review the actual changes and relevant exact-source evidence.
