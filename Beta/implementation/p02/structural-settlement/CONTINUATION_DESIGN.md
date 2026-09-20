# Durable consequence continuation: retained integration map

**Refinement, 2026-09-19:** the [durable continuation implementation](../consequence-continuation/README.md)
reserves the selected interval at trigger and charges it only at terminal publication, including refusal.
The original clock-at-trigger proposal below is retained as design history. Deferring the charge preserves
exact capped effort, exposure and gathering bindings without a duplicated predecessor or inverse transition.
Use the [timeless contract](../../../Runtime/docs/architecture/consequence-continuation.md) for the implemented
owner. Broader affected closure and physical consequence gates remain open.

The following is the original reviewed design dependency, not an accepted feasibility gate.
The atomic harmless-settlement slice has no pending root and no intermediate save. General collapse
still requires retained bounded work and automatic host continuation. This map preserves the native
agent investigation so later work need not repeat it.

The trigger spends one selected advancing interval and reserves the original command identity. It
retains stable physical state and unfinished final excavation work/source ownership, with one exact
pending command, semantic dependency basis, complete immutable policies and resolution cursor. It
must not emit Applied before physical consequences exist. A continuation uses an explicit empty-action,
empty-movement internal batch kind, increments world revision only and resumes without actor input,
client readiness or wall-clock catch-up. Completion publishes all affected terrain, physical members,
actors/effects, custody and identities with the original command's sole terminal receipt.

Concrete contracts:

- `WorldStepBatch` and `WorldStepFrameCodec`: add a profile-specific continuation. HeldInteraction is
  unsuitable because it requires a discrete action and represents a different time condition.
- `WorldCommitPreparation`: account exhaustively for each original action: terminal result or exact
  retained pending command. Completion may terminate only its predecessor's pending action. Never
  relax the omitted-outcome check generally. Existing result IDs include the completion sequence.
- `SqliteWorldAuthority`: retain the existing one-transaction write of commit, outcomes, identities,
  head and checkpoint. Pending exact retries require current-control verification and byte equality;
  a null terminal receipt cannot make an accepted pending command look like an occupied-ID error.
- `WorldStorageRecords`, exact database schema and connection admission: schema 3 commit kind 3 already
  means profile migration. A distinct continuation kind needs a fresh exact schema, while preserving
  older schema definitions. Root-owned pending state needs no independent pending table.
- `WorldJournalRecovery`: replay every retained cursor and deferred receipt through identical preparation.
  Reserve Command once at trigger, Result/SpawnedEntity once at completion. Read-only reopen and history
  must never advance work or write. Existing full-ledger checks remain required.
- `LocalWorldSession`: service durable work before client readiness, reading holds and input/NPC selection.
  Remove the unconditional same-commit `Single` receipt assumption only for exact pending transitions.
  Keep awaiters attached until terminal completion; stopping cancels the waiter, not durable work.
  Preview fallback cannot select another advancing step while a consequence remains pending.
- Presentation exposes resolving state separately from dialogue reading hold. Restart can reattach an
  exact retry but must not require a new actor request to resume the owner.

Effort needs particular care: excavation spends 400 before the step's capped 10 recovery. Recomputing
that debit after recovery changes outcomes near the cap. Retain the intended transition once. Likewise,
the basis pin cannot hash its own changing cursor/root revision; retain fixed semantic dependencies and
complete policies separately. Initial isolated trigger selection avoids same-step actors changing a job
basis after capture; broad simultaneous selection needs an explicit contract.

The existing envelope remains 250,000 commits, 65,536 terminal outcomes, 128 MiB accepted journal and 16 MiB
root. Revision-based checkpoints permit unchanged advancing counts. Qualification must include automatic
resume from a real reopened pending root, exact retries, rejected conflicting requests, crash cuts before
and after trigger/continuation/final commits, full ledger/identity comparison and exact historical reads.
No existing deadline, state bound or acceptance threshold is waived by this design.
