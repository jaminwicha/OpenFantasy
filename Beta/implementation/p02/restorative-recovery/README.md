# Ordinary care recovery qualification

This packet qualifies profile26 ordinary-care publication through actual external process
interruption. Application rules and storage formats remain unchanged. Architecture documentation
is maintained in the same change; execution evidence and the continuation proposal belong here.

## Scope

The fixture matrix uses actual admitted journeys, selected commands and durable before/after saves:

| Fixture | Publication under interruption |
| --- | --- |
| Raw self-use | Player spends one real source dose and receives its capped restoration |
| Crafted self-use | Player gathers ingredients, crafts a dose, then consumes its actual output |
| Helper assistance | Controlled helper spends its owned dose under the patient's existing permission |
| Patient withdrawal | Patient revokes the grant without spending medicine or changing resources |
| Assistance at archive capacity | One use and one exact closing-root resource archive publish together |

For each case, the external runner executes an ordinary control and kills separate Host processes
before and after SQLite commit. Reopening must yield the complete prior or successor database,
including receipts, identity reservations, archives and checkpoints. A pre-commit interruption can
then execute the original batch once. An already accepted batch cannot execute again. A separate
managed check exercises authorized exact receipt lookup on those same recovered databases, rejects
changed command content and wrong delivery, and proves no new state or database changes.

The external runner retains the established 30-second Host-call and five-second kill bounds.
The four ordinary fixtures preserve original actor statistics; the archive fixture uses explicitly
authored stress statistics to earn its 256 effects through actual combat and care commands.
Expected gameplay states come from the managed journeys; the independent Python checks inspect
canonical frames and complete stored tables. This is storage/recovery qualification, not an
independent reconstruction of all gameplay laws, a power-loss test or a production performance claim.

## Architecture and remaining dependency

The owner requires the architecture reference to remain current as code evolves. The existing
[maintenance hook](../../../AGENTS.md#architecture-reference-and-maintenance-hook) already applies
to Codex and Claude. This change backfills the source-backed distinction between SQLite publication
and caller acknowledgment in [ordinary care](../../../Runtime/docs/architecture/ordinary-care.md),
and clarifies material/care continuation obligations in the
[consumption](../../../Runtime/docs/architecture/item-consumption.md) and
[archive](../../../Runtime/docs/architecture/resource-archive-continuation.md) references.
Timeless reference prose contains no phase progress or qualification claims.

[Material and care continuation](MATERIAL_CARE_CONTINUATION.md) records why the next implementation
must address production batches, consumed claims, care history, resource effects and dependent
physical owners together. It recommends a bounded verified live-right frontier with retrievable
historical provenance. It is a proposal, not a completed continuation protocol. No collection limit
is raised or weakened, and no saved profile changes interpretation.

## Verification and preservation

[Verification](qualification/verification.json) records a clean final build and **six passing managed
cases**, with no failures/skips: five actual fixture journeys and one receipt test over all fifteen
completed recovery databases. This is a targeted qualification, not a new full solution regression.
The application and client sources remain unchanged.

[External recovery](qualification/crash-run-01/result.json) passes **10 actual SIGKILL probes,
61 Host calls and 11 deliberate corruption refusals**. All fifteen normal/before/after journeys match
the exact expected durable tables; the managed follow-up proves authorized receipt lookup, altered
content and wrong-delivery rejection on those same databases. Maximum observed Host-call time is
19.78 seconds under the unchanged 30-second bound; this does not establish production performance.
Fixture and execution hashes remain fixed throughout the run.

[Independent source review](REVIEW.md) found one execution-dependency pinning gap, corrected before
the external run. [Failures and corrections](FAILURES.md) retain its disposition and setup diagnostics.
No acceptance gate was weakened. The architecture reference, project instructions and ecosystem
verification/technical-design skills retain the same-change documentation maintenance rule.

Pre-change checkpoint `codex/checkpoint-before-restorative-recovery-20260928T043500Z` resolves to
`8a8276380f9f728d0151d6747438c1e2e773bef9`.
[Preservation inputs](qualification/preservation-before.json) pin frozen specs and unrelated staged
predecessor changes. [Milestone preservation](qualification/milestone-preservation.json) verifies all
61 prior refs against retained baselines, correcting the initial inventory query without overwriting
it. The checkpoint was created with a clean successor tree.

[Complete evidence archive](qualification/crash-evidence.tar.gz) retains fixtures, working databases,
sidecars and reports. Its [manifest](qualification/crash-evidence-manifest.json) verifies every member.
After all processes/tests completed, duplicate SQLite main/WAL/SHM files were removed only after that
comparison; canonical frames, metadata and text reports remain directly readable. Restore into an
empty directory and verify its members before replay. The runner accepts that restored `fixtures`
directory and a fresh output directory; retained historical reports use their original absolute paths.

Full material/care and journal lifetime, due-actor Wait combat items, broader native crafting and
NPC supply, production platforms/scale/multiplayer, physical input, art/audio and human acceptance
remain open. This packet does not close a whole phase or the complete game specification.
