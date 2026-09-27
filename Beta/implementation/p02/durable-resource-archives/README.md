# Durable resource archives and care

Baseline `c3ed6316d4f36286cd85447e744e0330b57305ef`; recoverable pre-change pointer
`codex/checkpoint-before-durable-resource-20260927T221400Z`. The
[before-change manifest](before-implementation.json) records preserved specifications, prior milestones
and the unrelated predecessor index. This packet records a host integration increment, not a phase exit.

## Implementation

- Explicit schema 7/profile 25 binding; schemas 2–6 retain exact tables and interpretations.
- INSERT-only archive rows close revision N and reference publishing commit N+1. The existing exclusive
  writer verifies exact prior head bytes, admitted scope and publishing before-hash, then commits archive,
  batch, receipts, identities, head and scheduled checkpoint together. Preview performs no writes.
- Complete recovery consumes actual stored archive rows against deterministic replay, verifies bytes
  with the previous retrieved anchor and rejects missing, altered, unscheduled or trailing evidence.
- Privileged archive retrieval returns immutable bytes with publication/source-head pins under the
  same writer gate. Consistent new-file backups use the owning connection, including its WAL state.
- Care genesis admits only fresh care/material/resource owners; acknowledgment and treatment use their
  exact Care commit kind. Existing controller checks and permanent identity/receipt retention remain.
- Archive count and bytes have separate explicit development limits (1,024/256 MiB). Accepted-journal
  limits and other owners' limits are unchanged. A limit is a host stop, never an actor rejection.

## Verification

[Verification](qualification/verification.json) records a clean solution build and full 1,355 Core + 698
Hosting/client = 2,053 passing managed cases, zero failures/skips. Final production sources and Host
binaries match the crash-qualified bytes; all 1,032 C# source hashes remain unchanged during broad
regression. Focused checks also cover the two real rewards at 255 batches (both success and later-command
rollback), and structurally conserved crafted stock rejected as a fresh genesis.

Six external SIGKILL probes and 30 normal/recovery/duplicate/historical Host operations pass with
unchanged fixtures, exact database contents and both raw/crafted one-dose effects. An independent
native critic found no blocking runtime defect and improved the counterexamples; [review](REVIEW.md)
retains corrections. Build 04's test-constructor error, the underfunded paired-fixture run and the first
incorrect Host path invocation remain recorded. No threshold or runtime rule was relaxed.

All 23 frozen specs, 55 earlier milestones and the unrelated 19-file predecessor index are preserved.
The verified increment is preserved on `codex/milestone-durable-resource-archives`, resolved by that
branch. The development checkout remains `codex/runtime-foundation`.

[qualification/crash-evidence.tar.gz](qualification/crash-evidence.tar.gz) preserves all 136 raw crash
fixture/run artifacts, including consistent SQLite copies, original boundary signals and outputs.
The [packaging manifest](qualification/crash-evidence-manifest.json) records each byte hash; unpack
under an empty directory to reproduce the named fixture/run tree. The independent Python checker
validates framing, hash chains, reference/receipt links and exact dose retirement, not damage math.

## Remaining campaign obligations

The ordinary session scheduler, player/helper offers and native aftermath treatment still need their
actual bindings. Ordinary non-aftermath care remains a distinct eligibility/time policy. Other care
and material collection bounds, global journal lifetime, checkpoint-tail recovery, production scale,
roster/rule migrations, physical input and human acceptance remain open. A bounded durable resource
archive does not establish unlimited game lifetime or completion of the full implementation plan.

Architecture references are updated in the same change: [archive continuation](../../../Runtime/docs/architecture/resource-archive-continuation.md),
[persistence](../../../Runtime/docs/architecture/persistence-and-history.md),
[resource history](../../../Runtime/docs/architecture/world-resource-history.md) and
[aftermath care](../../../Runtime/docs/architecture/aftermath-care.md).
