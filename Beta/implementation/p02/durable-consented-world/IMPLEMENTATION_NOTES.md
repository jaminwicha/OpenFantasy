# Durable consented world: implementation notes

Progress and measurements for this unit. Timeless contracts live in the two owned architecture pages.

## Binding decision

Schema eight is reused unchanged for the explicit 27/28 family. Its `world_material_care_segments`
table stores scope, ordinal, boundary/publication sequence, boundary hash and bounded opaque closing
bytes. No column or constraint is profile specific, so no schema, table or migration was added.
The stored genesis selects the interpretation; the schema number never does.

Four owned source joins changed. Each names `27 or 28`; none admits a range or a greater profile.

| File | Change |
|---|---|
| `SqliteWorldAuthority.cs` | Fresh creation maps profile `27 or 28` to schema eight. |
| `WorldJournalRecovery.cs` | Genesis and final-state schema checks accept `27 or 28`. Added one check: a schema-eight head must keep its genesis profile. |
| `WorldDatabase.MaterialCareArchives.cs` | Archive publication accepts an accepted genesis of profile `27 or 28` with matching scope. |
| `WorldJournalPolicy.cs` | Joint archive accounting applies to `27 or 28`; both still refuse the resource archive family. |

Unchanged: every limit in `WorldJournalPolicy` and `WorldDatabase`, all schemas, `WorldCommitPreparation`,
`WorldMaterialCareArchiveReplay`, `FindReceipt`, the single commit transaction, expected-genesis opening,
Core, and all existing tests and fixtures. `ReadMaterialEvidenceForHost` still requires profile 26.

The added genesis-profile check is defence in depth. Replay already refuses a step whose profile differs
from the current state (`ValidateDispatch`), and schema-eight journals cannot hold a profile migration.

## Test design

New files, all under `Beta/Runtime/tests/OpenFantasy.Hosting.Tests/Care/`:

- `ConsentedWorldDurableAccess.cs`: a nested accessor in the existing partial class. It lends the
  existing private `ConsentedPotionFixture`, `Pair`, `RawTerms` and `CraftCommand` unchanged.
- `ConsentedWorldDurableJourney.cs`: one class fixture that lives the world through `SqliteWorldAuthority`.
- `ConsentedWorldDurableTests.cs`: 17 cases (5 methods) in a separate class, so the legacy selection
  does not run them a second time.

The journey uses the existing potion fixture with receiver headroom, fresh profile-28 admission and
`CareWorldSession(SqliteWorldAuthority)`. Order of accepted commands:

1. Existing approach, defeat, acknowledgment, treatment, extra wait, victory and release.
2. A valid pair refused by physical reservoir storage, with selected movement for both members.
3. Raw split gift of one unit, then the whole child returned.
4. Bulk craft 12 berries to 4 potions; produced gift of 3; recipient recraft of 1; self-use.
5. Whole-lot raw gifts until the owner itself publishes cut 0. Close and reopen with expected genesis.
6. Onward produced gift of 1; recraft and self-use of the last child unit, which retires the child.
7. Whole-lot raw gifts until the owner publishes cut 1.

The pressure is 507 real accepted paired gifts. No threshold is shortened, no segment is sealed by the
test, and no row is injected on a success path. `CutAndDecode` is not used. The journey asserts that the
archive count in the database equals the cuts the owner returned. The raw gift candidate is chosen by
`Preview`, which publishes nothing, because the recipe berries must stay whole for the bulk craft.

The 520-dose retained world and `DurableMaterialCareJourney` are untouched.

## Measurements (first focused run, `durable-focused-20260929T094402Z`)

| Fact | Value |
|---|---|
| Final revision | 850 commits |
| Joint archives | 2 (boundary 592 published at 593; second near the head) |
| Pressure gifts | 507 |
| Closing archive 0 | 426,254 bytes |
| Root after cut / final root | 49,448 bytes |
| Genesis | 61,640 bytes |
| Database before / after cut / final | 2,424,832 / 2,482,176 / 3,981,312 bytes |
| Selection wall time | 319.7 s of the 600 s limit (347.4 s in the second run, beside the legacy run) |
| Journey and reopen test | 18.6 s |
| Fault cases | 7.1 s (before commit), 12.0 s (after commit) |
| Corruption cases | 0.2 s to 52.7 s each; four full verifications per case |

The second focused run exported the same genesis, before, after and final hashes and the same command
identities as the first. The journey is deterministic; only file timestamps and paths differ.

Recovery replays the complete journal. Each corruption case pays for up to four complete replays of an
850-commit world whose transfer evidence grows to 255 records per segment. This is reopening cost under
full replay, not a production scale result.

## Failure retained

`durable-focused-20260929T094402Z` failed one case of 17. The company test's second refused pair asked
the existing `RawTerms` helper for multi-unit stock in the first member's bag. Two real gifts had already
reduced that lot to one unit, so the helper found nothing. The correction selects the second member's
untouched fresh lot. It is a test selection fix. No runtime rule, threshold or assertion was relaxed.
The failing log, TRX and JSON remain in `qualification/`.

## Source findings

- `FindReceipt` checks the supplied delivery against the command principal and current control before
  any database read. A wrong delivery gets one fixed refusal for existing and missing outcomes.
- A command carrying another participant's command identity under the caller's own principal reads the
  caller's own stream and finds nothing. The table key is `(principal, command_id)`.
- Dispatch failures thrown by `Prepare` occur before the guarded publication block, so a stale or
  reused batch does not force reopening. Injected faults inside the block do.
- A substituted genesis of the other interpretation with matching checksums fails at journal
  continuity, before any profile rule. The mixed step profile fails at dispatch inside replay.
- Refused pairs reserve cause, command and result identities permanently and spawn no child.
