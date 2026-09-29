# Consented counted-source transfers

The portable Core can now prepare an actual raw counted-lot transfer between two OwnerOnly
inventories when both current owners approve the exact transaction through independently verified
deliveries. One authority composes the existing custody movement planner; it does not create another
inventory or change either storage's policy. This is a standalone authority building block, with no
world, Host, asynchronous-offer or native binding yet.

The [architecture reference](../../../Runtime/docs/architecture/consented-custody.md) defines the
typed terms, command, acceptance, delivered values, candidate, receipt and bounded codec. Control,
ownership, identity freshness and item movement remain distinct responsibilities. Raw source
coverage, partial/whole-lot identity and revisions are preserved. Applying a candidate to a changed
complete counted state fails. The caller supplies complete historical identity occupancy and owns
durable publication; constructing or decoding approval data authenticates nobody.

## Implementation and review

Fable delivered nine Core files, three test files and the architecture page, without changing any
existing Core API or codec. It ran one clean Core-test build and 134 affected cases: 29 new cases
and 105 existing authority/custody regressions. Its [original result](WORKER_RESULT.md) remains
unedited. [Delegation evidence](DELEGATION.json) records verified `claude-fable-5-1`, 1M context,
requested Max effort, 1,188.563 seconds, client cost estimate $10.19470625 and no permission denials.
Context postflight verified. This records useful offloaded implementation, not net credit savings.

Astra found a receipt-matching gap: a structurally valid receipt with changed world, branch or
either principal still matched the original command and acceptance hashes. Four independently
constructed counterexamples [failed before the fix](qualification/receipt-binding-red.json).
`Records` now compares those explicit metadata fields as well as identities and hashes. It remains
a data-matching predicate, not a receipt lookup, visibility check or proof of durable authenticity.

The [independent critique](CRITIC_REVIEW.md) found further useful receipt and test gaps. The
[lead disposition](CRITIC_DISPOSITION.md) records fixes, four additional retained failing
counterexamples, explicit matcher limitations and required Host obligations. Applied receipt matches
now require binding approvals and a plausible moved identity; the codec rejects reasons outside this
contract. A real policy-owner change and manual-transfer protection flags have regression coverage.

The [final solution build](qualification/critic-final-build.json) passes with zero warnings/errors.
The [affected regression](qualification/critic-final-core.json) passes **147 distinct cases**, with
no failures or skips: 42 new cases and 105 existing authority/custody cases. Sources remained unchanged
during both runs. No whole-suite, Host or native test is claimed; no application binding or existing
runtime logic changed. The critic reviewed the earlier source; the lead reviewed and tested its fixes.

Tests exercise both OwnerOnly owners, current control on both sides, forged claims/delivery,
swapped command/terms/revisions, wrong scope/owner, historical command/acceptance/retired-lot identity,
spawn collisions, protected/reserved/missing stock, capacity and arithmetic limits, stale approval
and plan, whole versus partial transfer, and player/NPC parity. Canonical tests use an independent
frame writer, a literal header prefix and the previously retained split identity; they do not claim
an externally authored full fixture. Existing unilateral transfer remains forbidden for the same
destination. The verified scope is the actual counted-source authority, not the entire game.

## Preservation and workflow

- [Pre-change checkpoint](PRESERVATION.json) preserves the clean successor HEAD and 78 existing
  milestone refs; the unrelated staged predecessor work is excluded.
- [Worker checkpoint](WORKER_CHECKPOINT.json) preserves the completed worker's code and 134 passing
  cases before lead fixes and handoff compaction; it is explicitly a WIP integration snapshot.
- [Pre-critic-fix checkpoint](CRITIC_CHECKPOINT.json) preserves the first 138-case integration
  and the unchanged independent critique before its corrections.
- [Handoff preservation](HANDOFF_COMPACTION.json) records the original 102,007-byte body retained
  verbatim in a [sibling archive](../../AGENT_HANDOFF_2026-09-29_ARCHIVE.md), preserving relative links.
  The compact active handoff links the current work, full obligations and older evidence.
  This reduces repeated input size; it is not a measured credit-savings result.
- The original 23 frozen design files, old source/save evidence and 19 unrelated staged predecessor
  files remain outside this increment.

The verified increment is preserved by `codex/milestone-consented-custody`;
[verification](VERIFICATION.json) records final sources, artifacts and preservation checks.
Ecosystem commit `8201a6d` and `codex/milestone-handoff-receipt-discipline` preserve the reusable
receipt-validation and lossless-context lessons, synchronized to six installed targets.

## Required continuation

The [full integration path](INTEGRATION_PLAN.md) retains produced goods, shared capacity, material
history and continuation, complete-world admission, durable dual-participant receipts and retry,
asynchronous offers, reach/readiness/time, NPC policies, native controls and reciprocal trade.
The next implementation must connect these existing contracts to real gameplay rather than treating
approval serialization as a completed gifting system.

The original retained-world personal-dose objective remains open. Its rejected helper gift and
missing exact recipe source are preserved; this component neither changes that saved world nor
retroactively qualifies its acquisition route. A fresh-world demonstration is separate evidence.
Full P01/P02 exits, the two-market circuit, production scale, hosted players, supported devices,
art/audio and human acceptance remain required through the complete campaign.
