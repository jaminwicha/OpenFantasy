# Durable consented world implementation

Implement the next actual Host dependency at `d88e0392464806f53afc2e668a6c959ac7c7f8d6`.
Fable 5.1 (`fable[1m]`), 1M context, Max effort; 2,400 seconds / $24 client-estimate cap.
Astra owns integration/commits. This is substantial implementation plus tests, not another broad
review or a retry. Read current AGENTS/handoff and task-relevant references only.

## Required deliverable

1. Enable fresh explicit Core profile 28 through the existing SQLite complete-world owner, with
   schema selection, archive accounting/publication and deterministic recovery coherent end to end.
   Schema eight's existing table already holds bounded opaque world roots; reuse it for the explicit
   27/28 family if the unchanged physical layout permits. Preserve exact genesis-selected profile,
   scope, admission, old schema/profile behavior and expected-genesis opening. No silent migration,
   fallback, arbitrary greater-profile allowlist, new inventory owner or schema/table by habit.
2. Preserve the one transaction for complete candidate, journal, BOTH command receipts, permanent
   identities, archive, checkpoint and head. Retain every journal/state/archive bound and rejection.
   Privileged `Commit` receives host-selected inputs; its reconstruction of submitted delivery
   claims is NOT public authentication. Do not claim or implement an asynchronous proposal service
   or network ingress in this unit. Existing receipt lookup must still check the independently
   supplied current delivery before disclosure. Neither principal's lookup authorizes the other.
3. Add substantive SQLite tests using fresh real fixture admission and actual commands: raw gift,
   real bulk craft -> partial produced gift -> recipient recraft/use -> onward gift, across TWO
   automatically published coordinated cuts and real close/reopen. Reuse the existing meaningful
   fixture recipes/source stock and actor actions; do not patch lived roots, shorten thresholds,
   hand-seal a segment and call it a durable cut, inject rows as a success path or replace the 520-dose
   retained world. `CareWorldSession(SqliteWorldAuthority)` can execute helpers through the real Host.
   Do not use its in-memory `OccupiedIdentities` as the database's proof. Drive actual history pressure
   to publish cuts and read authentic archives back through `ReadMaterialCareArchiveForHost`.
4. Verify exact reopened root, original producer/output intervals and consumption/recraft ancestry,
   all joined care/resource/material owners, both exact immutable receipts, and permanent identities
   including original offer, acceptance, split child after retirement and a refused pair. Reusing old
   identities or replaying the original batch must fail without moving stock again. Wrong independently
   supplied delivery for EACH actor must refuse lookup before exposing an existing/missing outcome;
   current authorized lookup remains a positive control. This qualifies local receipt authorization,
   not transport authentication or the future two-party submission API.
5. Execute a real successful four-actor/two-pair batch and a refused pair through SQLite, before and
   after reopening. Preserve atomic material/physical rollback and normal refusal time semantics;
   use the existing complete fresh company/rollback fixture, not wolves granted player hands.
6. Fault-inject at actual BeforeCommit and AfterCommit boundaries for a paired transfer that also
   publishes a coordinated archive. Reopen and prove all-or-nothing head, both receipts, permanent
   command/result identities and archive. Export a self-contained disposable fixture (before DB,
   exact batch, after root, receipts, identity list, metadata) through an explicit environment variable
   for Astra's separate real-process kill validation. Exception injection is useful but is NOT SIGKILL
   proof. No native windows or kill command is assigned to this worker.
7. Corrupt disposable DB copies: missing/modified archive, reference ordinal/hash/scope, a recomputed
   self-consistent archive hash with bytes inconsistent with actual replay, missing/altered command or
   identity row, mixed profile/step bytes or wrong expected genesis. Actual recovery and historical
   selection must refuse the intended corruption, with valid controls and exact stage/type/reason
   where material. No blanket exception catches or assertions accepting arbitrary exceptions.
8. Update owned architecture pages as timeless contracts describing the actual binding and remaining
   public-ingress boundary. Put progress/measurements in this packet. Report unchanged adjacent
   obligations: saved offers, NPC decisions, UI/native, retained-world transition, production history
   scale and remaining format matrix. Do not close those from this narrower durable dependency.

Read: `../consented-format-qualification/DURABLE_BINDING_NOTES.md`, `INTEGRATION_REVIEW.md`,
`../consented-custody/INTEGRATION_PLAN.md`, the architecture consented-world/coordinated-world pages,
existing `DurableMaterialCareArchiveTests`, `ConsentedWorld*Tests`, `CareWorldSession`,
`ConsentedCompanyFixture`, World storage/replay/policy code. Existing helper `CutAndDecode` seals
in memory; it is NOT a durable publication path. Existing native/retained-save history stays intact.

## Ownership

You are not alone in this checkout. Preserve others' changes. No Git/index/branch mutation, model
switch, recursive agents, network/package/toolchain changes or scope expansion. Pure readable C#;
stable `OpenFantasy` names. Do not edit Core, old tests/fixtures, client or existing schemas/limits.
Only these paths may be edited/created:

- `Beta/Runtime/src/OpenFantasy.Hosting/Worlds/SqliteWorldAuthority.cs`
- `Beta/Runtime/src/OpenFantasy.Hosting/Worlds/WorldJournalRecovery.cs`
- `Beta/Runtime/src/OpenFantasy.Hosting/Worlds/WorldDatabase.MaterialCareArchives.cs`
- `Beta/Runtime/src/OpenFantasy.Hosting/Worlds/WorldJournalPolicy.cs`
- NEW `Beta/Runtime/tests/OpenFantasy.Hosting.Tests/Care/ConsentedWorldDurable*.cs`
- `Beta/Runtime/docs/architecture/consented-world.md`
- `Beta/Runtime/docs/architecture/coordinated-world-continuation.md`
- This packet's `WORKER_RESULT.md`, `IMPLEMENTATION_NOTES.md`.

New tests can use the existing `ConsentedWorldPublicationTests` partial class for private helpers;
each new test method must start `Durable_` so the focused selection finds it. If a separate test class
is useful its name must start `ConsentedWorldDurable`. Supporting new files share the owned prefix.
Keep the 29 old world tests unchanged. If an adjacent file actually needs editing, report the exact
required correction instead of bypassing ownership. Read-only architecture inputs will be snapshotted
before dispatch so your two editable pages do not invalidate the synchronization check.

Make a minimal meaningful owned file edit first to verify native edit access. If a required operation
is denied, stop and report that exact invocation; no shell workaround or permission widening.

## Allowed checks and reporting

Runner is read-only and pins sources/assemblies. Finish code edits before build/tests. Exact allowed
Bash commands from repository root, no cd/pipes/redirection:

python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/durable-consented-world/qualification/run_checks.py build
python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/durable-consented-world/qualification/run_checks.py focused ABSOLUTE_SUCCESSFUL_BUILD_JSON
python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/durable-consented-world/qualification/run_checks.py legacy ABSOLUTE_SUCCESSFUL_BUILD_JSON

Build limit 240s, test limit 600s unchanged. Focused selects the new tests and exports the crash
fixture to `OPENFANTASY_CONSENTED_DURABLE_EVIDENCE` (private path recorded in the check JSON). Legacy selects existing managed archive/policy/world
cases; it must not opt into native windows. Astra runs broad integration and actual process loss.
Use normal Read for logs. Retain failures; no hidden reruns or adjusted bounds. Corrections and another
check within this one bounded assignment are allowed. No automatic paid retry after this run.

Return compact actual file changes, executed results/evidence, export location, exact remaining gaps
and source findings. Distinguish a proposed check from executed evidence. Request no model provider
switch. Do not claim the work complete if the two real published cuts or substantive journey is missing.
