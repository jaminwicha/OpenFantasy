# Independent critique: coordinated native permission-withdrawal increment

**Verdict:** no defect found in expected-genesis selection or client attachment. Four consequential findings and one optional group, all in fixture preparation, authentication provenance and evidence wording. Nothing was run; this is source reading only.

## Consequential findings

### 1. Preparation bypasses the live helper policy, and the reach refusal is never an authoritative receipt — High
- Preparation commits batches straight through `authority.Commit` carrying only the player's sample or nothing (`CoordinatedNativeJourney.cs:185-195`, `203-214`).
- The live scheduler adds helper policy actions on every advancing step (`LocalWorldSession.cs:558-568`).
- At the in-reach boundary the policy's visible preconditions hold (`LocalRestorativeController.cs:37-52`, `76-78`):
  - permission active, both ready, and helper aid evaluates to `None` (`Journey.cs:131-134`);
  - HP is 95463 of 100000 (`metadata.json:74-76`);
  - the retained and in-reach states are both revision 1579, with 0 readiness steps and 1 walking step (`metadata.json:25-41`).
- Policy emptiness is asserted only at the final out-of-reach boundary (`Journey.cs:142-144`), never at the in-reach state.
- The refusal "specifically for reach" is a diagnostic `Evaluate` on snapshots (`Journey.cs:140`, `489`). Preparation is asserted to hold zero command outcomes (`Journey.cs:329`), so no OutOfReach receipt exists in the ledger.
- **Unverified:** whether the helper's stable selection identity is already occupied at 1579, which would legitimately suppress the policy.
- **Action:** assert and record `SelectActions(InReach)`. If it is non-empty, prepare through the live scheduler or state plainly that preparation batches are host-authored and exclude helper policy. Label the refusal as evaluated, or retain an actual rejected helper command.
- **Related wording:** `INTEGRATION_DECISION.md:45` says preparation moves the helper away; the implementation moves the player (`WITHDRAWAL_RESULT.md:14-15`).

### 2. Tests with no input are recorded as passes — Medium
- Early returns sit at `CoordinatedNativeFixtureTests.cs:22`, `66`, `103`, `127` and `CoordinatedNativeResultTests.cs:20`; the fixture itself returns at `Journey.cs:43`.
- A missing or misspelled environment variable in the final integration run would produce a green TRX that authenticates nothing.
- `WITHDRAWAL_RESULT.md:8-10` is candid about this, but it still counts "13 runner passes".
- **Action:** use a skip, or make absence a failure in qualification mode, and have the integration script check the executed count.

### 3. Original-ledger provenance rests on metadata, not on the source — Medium
- `Authenticate` takes the retained revision and hash, actor identities, permission, stock lot and use totals from `metadata.json` (`Journey.cs:306-317`, `373`, `552`, `567-573`).
- The only fixed constants are the fixture name, the action, profile 27, at least 3 archives and 80 Potions (`Journey.cs:33`, `406`). Nothing asserts 520 uses.
- `sourceSha256` is never read back. The source is bound only inside the fixture process (`CoordinatedNativeFixtureTests.cs:23-24`).
- The result test's pins come from the same runner manifest (`CoordinatedNativeResultTests.cs:32-38`), so any self-consistent profile27 fixture would authenticate.
- No ledger digest compares the source `final.sqlite3` at revision 1579 with the fixture or the final database.
- **Action:** assert the 520-use constant and that `sourceSha256["final.ofc"]` equals `retainedHash`. Record and compare a retained-ledger digest, and pin the source digests in the integration script.

### 4. The runner report overstates scope for the coordinated route — Medium
- `check_native_restorative.py:57-59` writes an unconditional scope: "source-stat injury … native personal use, helper action, permission withdrawal".
- The coordinated route runs only `revoke` (line 51), and the fixture records `sourceStat: false`.
- A passing coordinated report would therefore list native personal use as in scope, which conflicts with the open-objective rule.
- `Bootstrap.cs:48`, `66` also admit a `keyboard` coordinated mode with no runner route or test. It would fail at `CanExecute`, so it is not a false pass, but it should be documented as the open objective's entry or removed.
- The probe's check text is similarly ambiguous (`RestorativePlayProbe.cs:153-154`).

## Optional refinements

### 5. Transaction and archive coverage — Low
- The before/after comparison covers care, production, inventory, progression, resource history and positions (`Journey.cs:463-470`, `512-544`). It does not cover quests, gathering, dialogues, construction, coordination, pacing or encounter lifecycle.
- `expected-immediate-withdrawal.ofc` is written (`Journey.cs:241`) but never read or pinned.
- Damaged-archive refusal zeroes only the first archive's state (`Journey.cs:586-589`). It does not try the other archives, a deleted row or an altered reference.
- Byte-preservation checks read the main file only, not the `-wal`/`-shm` sidecars.

## Checked with no actionable finding

- **Expected-genesis selection** (`SqliteWorldAuthority.cs:83-112`):
  - validates the canonical fresh genesis, then refuses a missing file before opening anything;
  - performs full recovery, then compares both the recovered genesis hash and the stored bytes;
  - disposes the database on every failure path.
  - The selection tests cover refusal, byte-identical saves and re-opening afterwards.
- **Client attachment** (`FarmClientSession.cs:143-182`): the default path still uses the quest companion. The care companion uses `Single`, so a missing or ambiguous permission throws and the authority is released. No test covers that negative case.
- **Runner bounds:** 30s opening, 150s probe and 180s process limits are kept, along with zero focus requests and the five legacy routes under `all`.
- **Withdrawal authentication** covers command and principal identity, permission id, grant and revision, the revocation record, and HP, MP, stock and prior uses.

## Limits

- I did not read `WorldJournalRecovery`, `WorldDatabase`, `run_integration_checks.py` beyond one line, or the architecture pages' text, because of the budget.
- `qualification/native-restorative-6zmf7nbn/` holds a save with live `-wal`/`-shm`, an empty stderr log and no probe report. It supports no native claim.
- The personal-dose native objective remains open. Nothing here substitutes for it, and no consensus or human acceptance is implied.
