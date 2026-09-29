# Durable consented world: worker result

Fresh explicit profile 28 now runs through the existing SQLite complete-world owner, with schema
selection, archive accounting, publication and recovery bound end to end. The substantive journey and
both automatically published cuts exist and were executed through the real Host owner. The final
focused (17 of 17) and legacy (38 of 38) selections are accepted; one earlier focused run failed and is
retained. Process loss, public paired ingress and broad integration are not proven here.

See [implementation notes](IMPLEMENTATION_NOTES.md) for design, measurements and source findings.

## File changes

Source (all within ownership, no schema, limit, Core or existing test edited):

- `Beta/Runtime/src/OpenFantasy.Hosting/Worlds/SqliteWorldAuthority.cs`: profile `27 or 28` selects schema eight.
- `Beta/Runtime/src/OpenFantasy.Hosting/Worlds/WorldJournalRecovery.cs`: schema checks name `27 or 28`;
  a schema-eight head must keep its genesis profile.
- `Beta/Runtime/src/OpenFantasy.Hosting/Worlds/WorldDatabase.MaterialCareArchives.cs`: archive publication
  accepts a profile `27 or 28` genesis with matching scope.
- `Beta/Runtime/src/OpenFantasy.Hosting/Worlds/WorldJournalPolicy.cs`: joint archive accounting for `27 or 28`.

New tests (`Beta/Runtime/tests/OpenFantasy.Hosting.Tests/Care/`):

- `ConsentedWorldDurableAccess.cs`, `ConsentedWorldDurableJourney.cs`, `ConsentedWorldDurableTests.cs`.

Architecture: `consented-world.md` (new Durable Host binding section, revised application boundary)
and `coordinated-world-continuation.md` (shared schema-eight binding).

## Executed evidence

All evidence is in `qualification/`. Both builds passed with zero warnings under warnings-as-errors.

| Check | Evidence | Result |
|---|---|---|
| Build | `durable-build-20260929T094328Z.json` | Accepted, 28.4 s |
| Focused | `durable-focused-20260929T094402Z.json` | **Failed**: 16 passed, 1 failed of 17, 319.7 s. Retained. |
| Build after correction | `durable-build-20260929T094952Z.json` | Accepted, 10.3 s |
| Focused after correction | `durable-focused-20260929T095011Z.json` | **Accepted**: 17 passed of 17, 347.4 s, sources unchanged |
| Legacy | `durable-legacy-20260929T095013Z.json` | **Accepted**: 38 passed of 38, 421.6 s, sources unchanged |

The one failure was a test selection error in the company case, described in the notes. The other 16
cases passed in that first run: the journey with reopening, both fault boundaries, all 12 corruption
cases and the mixed-profile/expected-genesis case. The focused and legacy runs were started together
after the correction; they share the machine, so their wall times are not comparable with the first run.

The legacy 38 are 29 `ConsentedWorldPublicationTests`, 8 `DurableMaterialCareArchiveTests` and 1
`ResourceArchivePolicyTests`. **The filter term `WorldJournalPolicyTests` selected no test.** The
profile-27 joint archive policy cases live in `WorldStorage/MaterialCareJournalPolicyTests`, which that
filter does not match, so this worker did not execute them after changing `WorldJournalPolicy`. The
profile-27 policy path was exercised only through the 520-dose durable journey, which passed.

Besides the three allowed commands, this worker ran read-only `ls` on the packet, export and
qualification directories to locate evidence. Nothing else was executed.

What the passing cases executed, by deliverable:

| # | Executed |
|---|---|
| 1 | Fresh profile-28 creation in schema eight; recovery and expected-genesis opening; profile-27 genesis refused as the expected genesis of this save. |
| 2 | One transaction for root, journal, both receipts, identities, archive and head, read back row by row. Checkpoint scheduling is unchanged but no checkpoint boundary other than genesis is asserted by these tests. |
| 3 | Raw gift, bulk craft, partial produced gift, recipient recraft and use, onward gift, across two owner-published cuts and a real close and reopen between them. 850 commits, 507 pressure gifts. |
| 4 | Exact reopened root bytes; producer, output intervals 0-3, 1-2, 2-3 and 3-4; recraft and consumption ancestry; joined owner hashes; both receipts of seven pairs; stored identity rows including offer, acceptance, retired split child and refused pair. Replayed batches and seven reused identities refused with the root unchanged. Wrong delivery refused for each actor on existing and missing outcomes. |
| 5 | Passed in the second focused run only; it is the case that failed in the first. Existing company fixture through SQLite: a four-actor two-pair batch and a refused pair (obsolete lot revision), before and after reopening. Success spends each member's movement; refusal elapses one step, keeps both selected movements and changes no material owner. The physically refused pair with material rollback runs in the main journey on the existing receiver-headroom fixture. |
| 6 | Injected `IOException` at `BeforeCommit` and `AfterCommit` on the pair that publishes cut 0; head, both receipts, five identities and the archive are all absent or all present after reopening. |
| 7 | Twelve corruptions plus a mixed step profile, each refused by recovery, expected-genesis recovery, archive selection and state selection with one exact message. Valid controls open and select. |
| 8 | Both architecture pages updated. |

## Export location

The focused check exports to the private path recorded as `fixtureExportPath` in its JSON. Use the
export of the accepted run:

`/var/folders/np/sh0qs32n2xlf150ph1vlcvrc0000gn/T/openfantasy-durable-consented-rrxwpw7f/durable-focused-20260929T095011Z-exports`

The failed first run exported to the sibling `durable-focused-20260929T094402Z-exports` with identical
genesis, before, after and final hashes. This worker did not check the export against
`qualification/verify_process_recovery.py`, which was added during the run and is not this worker's file.

It holds `before.sqlite3`, `after.sqlite3`, `final.sqlite3`, `genesis.ofc`, `before.ofc`, `batch.ofc`,
`after.ofc`, `final.ofc`, `archive.ofc`, both command and receipt files, `identities.json` and
`metadata.json`. Applying `batch.ofc` to `before.sqlite3` (revision 592) publishes revision 593 and
archive ordinal 0. Each focused run writes a new export directory; world identities are deterministic.

## Remaining gaps

- **Process loss is not proven.** Exception injection is not SIGKILL. No kill command or native window
  was run by this worker. Astra's separate real-process validation against the export is still owed.
- **Public paired ingress is not implemented or claimed.** `Commit` is privileged and its delivery
  reconstruction is not authentication. These tests qualify local receipt authorization only.
- **Scheduled checkpoints under profile 28** are not separately asserted beyond exact recovery, which
  verifies every checkpoint the policy scheduled.
- **The added head-profile check has no test that reaches it.** Dispatch already refuses a step of
  another profile, and schema eight holds no migration, so no valid or corrupted journal in these tests
  arrives at a head whose profile differs from its genesis. It is defensive and unexercised.
- **`LocalWorldSession` and the restorative controller/view** still name profiles up to 27. They are
  outside this unit's ownership and were not edited. A profile-28 world is reachable through
  `SqliteWorldAuthority` only, not through the interactive local session.
- **Archive reopen cost.** Full replay of 850 commits costs several seconds per verification.

## Unchanged adjacent obligations

Not closed by this unit: saved offers and the asynchronous proposal lifecycle; NPC decisions; UI and
native presentation; the retained-world transition and its 520-dose history; production history scale;
and the remaining format matrix (maximum-size roots, fixed provenance of opaque imports, protected stock
through a cut, V10 input bytes under a complete-world context).

## Proposed, not executed

- `MaterialCareJournalPolicyTests`, and a profile-28 policy case beside it, since the legacy filter
  does not select that class.
- Broad Hosting and Core integration, which Astra owns.
- Real-process kill validation against the exported fixture.
- A profile-28 audit of `LocalWorldSession` intent guards before any interactive exposure.
