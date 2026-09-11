# Exact recorded-world reconstruction

11 September 2026. This P01 increment supplies the first composed-world historical reconstruction
service, supporting RQ-054 and RQ-088. It does not close either requirement, P01/P03 or time travel.
The full historical design and all 181 requirements remain mandatory.

## Implemented boundary

`SqliteWorldAuthority.ReadRecordedStateForHost(revision)` reconstructs the complete immutable state
at an exact recorded commit, including genesis. It returns the verified source-head revision/hash,
the selected snapshot and its content hash. A returned result remains unchanged when live work
continues. Repeating a query after later commits preserves the selected state while truthfully
reporting the newer source head used for that preparation.

Held dialogue, rejected commands and profile migrations retain their real commit boundaries.
Two commits with the same calendar time are not conflated. Queries of a pre-upgrade commit retain
that commit's old admitted profile; newer recipes, tools or rewards do not leak backward. Existing
worlds require no migration, extra table, new serialization or rewritten history.

The headless operator `--inspect-recorded-world <composed-world.sqlite> <commit-revision>` exposes
the source pin, exact state hash/date/profile, actor positions and a bounded stock summary. This
requires privileged local access and a closed gameplay save under the existing exclusive writer
policy. It grants no player visibility, travel, fork, receipt or authority over a historical actor.
Unsigned decimal revisions are explicit; future, negative and overflowing revisions fail.

## Correctness and cost

History and recovery share one replay implementation. It verifies the entire bounded journal,
checkpoints, terminal receipts, identity reservations and current head before publishing a result,
including evidence later than the requested commit. It does not execute durable writes or client
effects. Public queries serialize with the existing writer and compare the reconstructed head to
the live pin. Corrupt reads require recovery; invalid future requests leave a healthy host usable.

This is an O(retained commits) correctness baseline with the existing recovery limits. Opening
the operator also performs ordinary readiness recovery. It is synchronous privileged work, not a
frame callback, background streaming solution or qualified production latency. The retained
6,000-commit fixture separately measures opening and historical reconstruction and proves that
commit 5,999 is reconstructed exactly rather than replaced by checkpoint 6,000.

Arbitrary interior milliseconds and partial-interval exposure, generated prehistory, ancestry,
branch creation/transfer, checkpoint acceleration, cancellation/progressive preparation and
representative history/archive workloads remain required. The existing `HistoryRequest` remains
the broader temporal/address contract; this deliberately named recorded-commit API does not
pretend to implement those unsupported semantics or round a requested date. No gate or threshold
was changed. No independent critic, native time-travel experience or human acceptance is claimed.

## Evidence and failure disposition

The initial focused run passed five tests and failed four corruption fixtures: each attempted
an external SQLite write while the authority's exclusive database connection remained open.
SQLite correctly rejected those writes with `database is locked`; the tests had not reached
their intended corrupt-journal assertion. The corrected fixtures close the writer, corrupt the
saved database, and exercise historical replay directly before readiness. Production locking,
timeouts and corruption rejection were not relaxed. Both TRX reports are retained.

The corrected focused run passes 24 cases, including the nine new history cases and affected
migration tests. New coverage compares complete canonical snapshots in reverse/repeated query
order after restart; preserves live inventory, receipts and source bytes; distinguishes same-date
held/closed states; retains prepared results across later commits; rejects future boundaries and
uncertain/disposed sessions; and refuses corrupt suffix, checkpoint, receipt and identity evidence.
Existing production/quest/construction upgrade tests now query both sides of their actual upgrade.

The first integration run stopped on Godot's editor-startup error, `EditorSettings not instantiated
yet` for Android SDK settings, despite exit zero. A subsequent unchanged import passed. Its precise
startup cause remains unconfirmed; the original failed report remains in `qualification/failures/`.

The second integration run passed build, all tests, fixtures, host inspection, import, native
history, input, inventory, lifecycle, gathering and production, then exposed a quest-probe race.
The probe pressed Talk in the same frame it released movement. The new history operator reconstructs
commit 156 with player revision 148; command 157 expected 147 and its persisted receipt is
`RevisionConflict`. The [analysis](qualification/failures/quest-revision-analysis.json) retains that
exact state and source hashes. The probe now observes stable arrival and matching displayed actor
revisions before one deliberate press; it never resubmits a rejected action. The five-second Talk
assertion, total timeout and authoritative rejection remain unchanged. Player-facing moving/action
handoff UX remains an acceptance concern; this correction only establishes the intended stationary
interaction. Input and failure reports now retain the captured Talk revisions.

Code `913a5f2` introduces reconstruction; `1d26210` records the probe correction and actor-revision
diagnostics. The [verification summary](qualification/verification-summary.json) composes the
retained successful checks with targeted native quest/construction followups and final tests on
the rebuilt binaries. This is explicitly **not one uninterrupted full verifier pass**. Only the
probe and operator diagnostic source changed after the integrated run; the manifest records all
685 final inputs. The rebuild stamped the new Git revision into assembly metadata, so tests were
rerun without rebuilding to match the actual native binaries.

The native history journey performs four real operator queries and three invalid-boundary refusals
against the farm's disposable save, with unchanged database bytes. The final test reports retain
544 Core and 311 Hosting passes, zero skips. The targeted quest journey covers prepare/upgrade/reopen;
construction covers continuation, completion/reopen and a fresh connected loop. Earlier successful
native input/offer/lifecycle/gather/production evidence is explicitly attributed to its source
revision. No new art, rendered review, device, Release/export or human qualification is claimed.
