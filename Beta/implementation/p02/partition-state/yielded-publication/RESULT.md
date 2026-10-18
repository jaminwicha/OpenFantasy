# Yielded authority publication

The sole SQLite authority now supports unpublished construction preparation across bounded read calls.
A handle binds the whole selected batch to its originating authority, captured interval and exact head.
Each read releases the gate; publication checks the same dependencies and commits the entire boundary.
The synchronous batch wrapper drains this same path under its original outer lock. Default gameplay,
profiles, schema14, pure-C# ownership and all core rules are unchanged.

Source baseline `1c552c6755e378d466b92fdce63c59c5ddedf49a`; [checkpoint](CHECKPOINT.json) preserves relevant
WIP. Milestone `codex/milestone-yielded-publication-20261018` names this accepted owned commit.

## Contract and evidence

BeginStoredConstruction captures unpublished preparation; ReadStoredConstruction loads at most the
requested owners; PublishStoredConstruction evaluates and publishes only a complete current handle.
An incomplete publication attempt leaves it available for more reads. Invalid read budgets consume
nothing. Cancellation is idempotent and publishes no time, identity or receipt. Foreign authorities
cannot read, publish or cancel the handle. A changed head or captured interval invalidates it; a
consumed/cancelled handle cannot publish again. Read failures retire it and fence the authority.
Restart restores committed world state and requires fresh preparation, not a serialized cursor.

The [publisher review](PUBLISHER_REVIEW.json) confirms the transaction/write-set/journal/checkpoint
body is unchanged after whitespace normalization. The full candidate, final-installation/legacy-limit
fallback and publication remain synchronous under the gate. No SQLite transaction spans read calls.
No second publisher, automatic retry, new saved state or changed command/time law is introduced.

Seven new cases use the original five-resident/two-builder fixture with actual harvested node regrowth,
funding and lawful routes. One-owner reads execute on explicitly different managed threads within a
five-second observation bound. The test verifies no terrain reads during preparation, exact read
counts, unchanged complete state/absent receipts before publication, one complete ordinary-equivalent
boundary and refusal of reuse. Partly and fully loaded handles reject a real intervening advancing
commit; fresh preparation then works. Cancellation/foreign access, a failed second read, restart and
before/after publication faults preserve the exact required old/new world and original genesis/history.

[Retained artifacts](ARTIFACTS.json) pin actual SQLite lineages, snapshots and original combined-case
measurements. Keep them immutable and use complete scratch copies for inspection. Prepared handles
are intentionally absent from saves. Existing tests retain empty/held/refused batches, legacy-limit
fallback, real final installation, intervening movement, storage failure and checkpoint prerequisites.

## Validation and boundaries

[Validation](VALIDATION.json):35 distinct affected cases pass,7 new, in66.669seconds wall time. No skips
or failed tests. The separate geometry preflight passed in5.966seconds and overlaps the main set;
it is not added to the total. Preflight02 builds with zero warnings/errors, discovers35 affected cases
and one exact geometry case, and pins1967 source/content/project/binary inputs. All managed runs retain
the180-second process-group bound. Thread observation adds a five-second assertion, not a longer gate.

[Dependencies](DEPENDENCIES.json) identify the two Host production files and test changes. Core sources,
publication body, storage formats and journal policy are unchanged. Earlier real6000-step checkpoint,
611-case qualification and cost measurements retain their original pins and limits; they are not new
current-source executions or a current-performance claim. Prior full-Core/native reliability failures
remain unresolved. No native, full-Core, independent critic, human or scale acceptance is claimed.

This implements a yielded authority boundary, not the ordinary live session's scheduling policy.
TickAsync still reads/previews complete state, and CommitSelectedBatch uses a complete decision for
trading observation and coherent view publication. The [due-work map](../../../PLAN_STATE_DUE_WORK_MAP.md)
now names readiness/pause/stop, new input, accepted-cutoff ownership, invalidation and projection as
the next integration requirements. Do not bypass those contracts by adding another timer or dropping
selected commands to meet a budget. Final CPU time, general obligation discovery/membership, oldest-due
age, durable preparation, current40/41 migration, cold/platform/scale and independent review remain open.
The complete P02 table and181-requirement/P00–P14 scope are unchanged.

## Packet discipline and friction

Entry instructions/handoff/roadmap/milestone sections were read for this packet and then retained in
working context. One source batch still exceeded output capacity; subsequent reads were bounded.
No explicit session-compaction API or full packet rollout token accounting is exposed. API-call count
and average input context are unavailable, not zero; no credit-saving claim. Orchestration stays paused.

Initial build/discovery passed; before any test run, local review strengthened the thread-release
witness to assert a different executing thread rather than relying on Task.Run scheduling. Its earlier
source and preflight are retained in [PREPARATION_REVIEW.md](PREPARATION_REVIEW.md). This source-freeze
refinement is not a failed run or automatic retry. Run inventory: one geometry preflight, one affected
storage suite, zero native runs, zero fixture/runner/production test failures and zero runs over180s.
Existing failure evidence and documentation obligations are preserved.

## Documentation review

- **Plan/spec:** update the [due-work map](../../../PLAN_STATE_DUE_WORK_MAP.md),
  [decision](../../../PLAN_STATE_DECISION.md), [roadmap](../../../CURRENT_ROADMAP.md),
  [handoff](../../../AGENT_HANDOFF.md) and [milestones](../../../GIT_MILESTONES.md). The authority API is
  qualified separately from the still-unbound live loop; no phase exit or requirement is removed.
- **Reference:** update [scheduling](../../../../Runtime/docs/architecture/due-work-and-scheduling.md),
  [construction locality](../../../../Runtime/docs/architecture/construction-locality.md),
  [ownership](../../../../Runtime/docs/architecture/partition-ownership.md) and
  [persistence](../../../../Runtime/docs/architecture/persistence-and-history.md) with authority affinity,
  cancellation/staleness/failure semantics, lock/transaction lifetime, complete publication and restart.
  The dependency closure is unchanged; its existing all-actor/gathering/full-admission reference remains accurate.
- **Accuracy/completeness:** lead compared production Begin/Read/Publish/Cancel, the unchanged publisher,
  actual cross-thread and intervention/fault evidence, and the ordinary session's remaining full-state
  consumers. References describe timeless contracts rather than phase progress. Independent review
  remains unrun while orchestration is paused; no independent or human acceptance is invented.
- **Checks:** run `python3 Beta/tools/check_documentation.py --work-record Beta/implementation/p02/partition-state/yielded-publication/RESULT.md --files` with [OWNED_DOCS.json](OWNED_DOCS.json), plus scoped
  whitespace and protected-index/roadmap checks. Terminal document results are retained in
  [DOCUMENTATION_CHECKS.json](DOCUMENTATION_CHECKS.json).
