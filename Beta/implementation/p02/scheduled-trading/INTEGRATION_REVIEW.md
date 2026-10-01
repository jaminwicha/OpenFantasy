# Scheduled-trading integration

Status: accepted as a bounded local-host increment. Milestone:
`codex/milestone-scheduled-trading-20261001`. Starting revision: `257f460033c500f520888f9505b5cb1f6395a5b0`.
This does not close the full market, native, migration, multiplayer or campaign gates.

## Retained behavior

- Authenticated participant views expose exact fixed quote terms, observable issuer revision and lifecycle
  without credentials, private stock bases, other inventories or superseding-party details. Background
  historical lookup reuses authenticated archive replay and the shared bounded inspection slot.
- A transient shopper inquiry is separate from a merchant's durable quote and the shopper's later acceptance.
  Configured merchants answer through their own control at normal session cutoffs; there is no player auto-accept.
- Opt-in NPC shoppers use their real holdings and funds. Bounded attempts can reconsider a desire after lawful
  circumstances change; exact refused decisions remain decided. One showing belongs to each attempt even if
  separately consented transfers replace a lot without changing aggregate holdings.
- Paid acceptance reserves both participants against movement/work. Gifts, trading, ordinary work and aid
  share arbitration. Saved gifts and ordinary-care gates bind their admitted paid/market profiles; real gift
  acceptance/decline and patient permission revocation are exercised in the market world.
- The session retains ordinary market-world excavation. It refuses unrepresentable queued input safely and
  displays an externally published root even when the current cutoff commits nothing, so a reading hold's
  dialogue can be closed through its public view.
- Actual trade, finite reward/sale funding, refusal, supersession, expiry, pause/hold/cancel, archive cuts and
  reopening use the existing authority. No Core rule, profile, codec, schema, content admission or asset changes.

The timeless [architecture](../../../Runtime/docs/architecture/scheduled-trading.md) describes ownership,
privacy, bounded decisions, complete service exclusions and persistence. Dependent care, gift, excavation,
market, authority and presentation references are updated together.

## Implementation and independent review

The [first assignment](ASSIGNMENT.md) returned a useful implementation with 173 distinct passes, preserved
in [initial evidence](INITIAL_CHECKS.json). It remained unaccepted because important binding gaps were real.
A fresh [review-and-repair assignment](REVIEW_AND_REPAIR_ASSIGNMENT.md) independently critiqued that input
before making its own repairs. Its [report](REVIEW_AND_REPAIR_RESULT.md) distinguishes lead hypotheses,
independent findings, red/green evidence and unexercised behavior. Astra reviewed the returned repairs;
the reviewer's own later code is not claimed to have its own independent external review.

The critic confirmed permanent inquiry suppression, held-input faults and inactive gift/care bindings,
and independently found restart identity reuse, permanently refused showings and broader cutoff hazards.
Nine additional scenarios and the corrected host behavior returned with 287 distinct passes.

Astra's four additional cases found and repaired three integration defects:

1. Equal-quantity lot replacement could publish a second showing inside one attempt. The showing identity
   now belongs to the attempt, with a later bounded attempt able to show the replacement.
2. A caller's externally opened hold could be safely refused yet remain absent from the cached view.
   Cutoffs now refresh changed observed roots even with no publication, with both queued and idle contrasts.
3. Core admitted ordinary excavation in profiles 32/33, but the host's old guard excluded them. The binding
   now admits it; the new market-world journey walks, earns effort, digs and preserves market/paid state.

The final source retains the earlier meaningful assertions. The exact core, old tests, source save and
frozen specifications are unchanged; no gate or threshold was weakened.

## Final verification

[FINAL_CHECKS.json](FINAL_CHECKS.json) verifies one final build, 1,466 source files and 11 binaries against
all reports and the current checkout. **291 distinct managed cases pass**: 179 focused authority/resource
cases plus 140 session cases, minus 28 overlaps; the 63 exchange cases are a subset. Seventeen scheduled
cases are new. All reports are terminal, with no skips. Core was not rerun: its 707 files and prior accepted
Core evidence remain unchanged. All 532 pre-existing test files, 23 frozen specs, the original retained
SQLite bytes and the unrelated 19-file staged predecessor patch remain exact.

The packet retains failures, not only the green run:

- The critic's initial three behavioral failures and its build/test expectation corrections are described
  in its report, with their original reports and logs.
- Lead build attempts exposed missing namespace imports (one repeated before correction). These are tooling
  mistakes, not runtime evidence; their failed reports remain in `qualification/`.
- The first lead excavation setup reached the unchanged planner expansion bound. It was replaced by actual
  bounded steps along authored approach waypoints, without changing routing limits, terrain or teleporting.
  This does not qualify long-distance route planning.
- `new-host-20261001T052318Z-668d6` reproduces all four lead failures before the source fixes; the final
  `new-host-20261001T052813Z-da291` passes all 63 cases, including those unchanged reproductions.

The original source database was only read as bytes or copied; no connection was opened against it.
All checks were headless and required no foreground Godot focus.

## Delegation and preservation

Both requests returned verified `claude-opus-5-5` with 1,000,000 context and requested Max effort, with
verified pre/post synchronization and no permission denials. The initial run took 1,965.071 seconds and
reported $13.1508518; the review/repair took 2,549.543 seconds and reported $13.931321. These are client
estimates, not billing or proof of net savings. The first worker reports two read-only shell listings
outside its task examples; no out-of-scope mutation is evidenced. The second used the granted wrapper
commands. Both jobs are terminal. No automatic paid retry, model fallback or relay was used.

[Dispatches](DISPATCH.json), [review dispatch](REVIEW_AND_REPAIR_DISPATCH.json), original assignment pins
and [lead checkpoints](LEAD_REPAIR_CHECKPOINT.json) preserve the actual inputs and deliveries. The latest
pre-repair snapshots are `d375253` and `bf946bf`; both are labeled WIP, not acceptance. The milestone
above preserves the later reviewed result. The lead owns the integration commit on `codex/runtime-foundation`.

[Ecosystem evidence](UPSTREAM.json) records commit `e876ab4`, the maintained technical-design lessons,
guarded installation backups and all 71 verified alignment entries. Only the two owned source/manifest
files were committed; unrelated ecosystem work was preserved. Codex and Claude installed skill copies match.

## Remaining work and next dependency

The next application dependency is [native trading](NATIVE_FOLLOWUP.md): explicit fresh market content
composition, contextual icon-led controls, rich inspection, a readable finite quote window and separate
confirmation, native input/focus/reopen proofs and human comprehension. The source-backed six-berry supply
and earned reward can start that journey; there is no new native interface in this milestone.

The critic's residuals remain except where the lead proof above closes them: queued-boundary change and
view recovery are proved for reading holds, not care/lifecycle/encounter changes. Market-world permission
revocation is exercised, but injured-actor self-use, helper aid and an NPC medicine offer there remain
unexercised. NPC funding recovery and arrival are proved; stock replenishment and departure/return are not
separate proofs. Not every scheduling exclusion is exercised, and inquiry expiry is tested with an authored
one-step window rather than 32 consecutive reserved cutoffs. No starvation-free scheduler is claimed.

Retained-world market migration, full legacy content import, merchant routes/wages, the two-town cargo/escort
loop, full demand/cognition, performance/scale, process interruption, native devices/art/audio/human acceptance
and P00–P14 stay open under the [full integration plan](../spot-exchange/INTEGRATION_PLAN.md) and frozen spec.
