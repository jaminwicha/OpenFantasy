# Independent review and integration

The native read-only critic reviewed the actual Core/Hosting delta with separate ownership from both
implementation workers. No external provider request was made after the retained spending-limit
failure. This review uses Astra capacity; it is not evidence of Claude credit offloading.

## Findings and disposition

1. **Fresh-only storage could claim a migration prefix.** Schema11 initially reused the schema10
   recovery replay rule without excluding later upgrade activation. A fabricated activation naming an
   actual later root, with recomputed checksums, could exempt its prefix from capped expiry validation.
   `ControllerRecoveryReplay` now requires schema11 activation at fresh genesis with no upgraded or
   unknown-duration provenance before filtering on source revision. Schema10 upgrade semantics remain
   unchanged. The new corruption regression uses the actual later root and recomputes both checksums.
2. **Structurally impossible permission chronology.** Accepted slots could place a high-revision
   latest offer too early; distinct slots could share a commit or require more prior changes than the
   available prefix. Slot validation now constrains offer chronology. State validation sorts at most
   two retained milestones per pair, rejects duplicate sequences and checks cumulative minimum changes.
   Complete-world admission combines controller and permission revision costs. No unbounded grant
   history or historical database lookup is introduced. Full journal replay remains authoritative;
   these checks strengthen structural admission rather than claiming to authenticate a decoded state.
3. **Test compared boxed enum identity.** A provisional recovery assertion compared the same saved
   mode using `Assert.Same`. It now compares the actual reopened head bytes, controller mode and
   allowance rows after a refused premature expiry.

The critic inspected the corrections and found no additional concrete defect in the frozen delta.
The critic ran no tests. Source review confirmed archive dispatch, preserved old-profile branches,
unchanged permanent `CanControl` and unchanged private-reader authorization. Missing active takeover
and UI are the declared next dependency, not waived acceptance.

## Lead integration

The lead added real four-human/all-twelve-pair acceptance with original-genesis reopen; absent offer
and acceptance refusal; owner revocation during allowance; and explicit refusal to use accepted scopes
for owner impersonation or renewed delegation. The first build exposed a new test's use of an internal
encoding helper. The test now writes the malformed fixed-width field directly; production visibility
was not broadened. The original failed report is retained.

The first focused storage run passed fifteen cases and failed the lead's impersonation test: the
host correctly refused an unauthenticated actor command before journaling, while the test expected
a stored Core refusal. The correction separately verifies Core's explicit `NotAuthorized` decision
and the host's earlier refusal, unchanged durable head, outcome count and unoccupied rejected IDs.
No production guard was changed. The original failure is retained; the final focused runs pass all
eleven Core and sixteen Hosting permission cases.

The lead subsequently found that the global permission-command decoder could feed commands to a
player adapter without a consent selection route. Existing cutoff fallback already refuses an
unrepresentable action; an initial concern about a host crash was not established. The new explicit
preflight guard instead guarantees refusal before queueing, while retaining authenticated exact
receipt retry. Six old/new-profile offer/accept/revoke cases first failed with bounded timeouts and
then passed after the guard. The critic reviewed this final production change and recommended an
exact prior-receipt retry contrast; a seventh case proves that retry while paused.

The adapter test first assumed every healthy cutoff earns exploration time. The legacy calibration
instead legally enters confrontation with zero time. The corrected assertion requires exactly one
journal transition and that exact phase/time contrast, keeping the original deadlines. Two additional
test compilation mistakes (missing cancellation token and a misplaced import) were repaired locally.
All original reports remain. No runtime rule, fallback, timeout or prior test was loosened.

The first broad Core and storage runs completed before the adapter guard. Further queued families
were stopped before starting; no running check was interrupted or edited underneath. Core evidence
may be reused only after byte-identical source and actual Core/Core-test assemblies are checked.
Hosting qualification runs again against the final adapter and test assemblies.

The final-source storage aggregate reached its unchanged 600-second limit after 484 logged passes.
Four cases had no terminal result: the three `EventSettlementWorldTests` and the real earned-stock
construction journey in `WorksiteContentTests`. The earlier same-selection 488-case run passed in
311.05 seconds before the adapter guard; source comparison shows only that guard and its new test
file differ. This does not explain the slowdown or turn the interrupted run into accepted evidence.
The original failure remains retained. Any later repeat must retain the identical filter, assertions
and deadline, and cannot establish the cause merely by passing.

One deliberate same-selection repeat passed all 488 cases against final source/binaries in 444.86
seconds under the same 600-second limit. The four previously unfinished cases all completed. No
filter, test, runtime source, assertion or deadline changed between these two executions. The repeat
supplies a complete passing execution; it does not explain or erase the original timeout.

The full previous handoff is preserved byte-for-byte in
[`AGENT_HANDOFF_20261002_ALLOWANCE_ARCHIVE.md`](../../AGENT_HANDOFF_20261002_ALLOWANCE_ARCHIVE.md).
`HANDOFF_ARCHIVE.json` records its identity; `HANDOFF_CHECKPOINT.json` records the separate WIP
snapshot made before replacing the current index. Historical gates, failures and source links remain
available; the compact current handoff is the sole next-assignment index.

Executed outcomes, current source/binary pins and any later failures are recorded by
`FINAL_CHECKS.json` and the raw `qualification/` reports. Reading-hold, archive, backup and fault tests
are managed evidence. They do not establish actual network consent, active delegated work, encounter
input switching, device behavior, art acceptance or full-phase completion.
