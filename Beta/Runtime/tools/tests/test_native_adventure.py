"""Qualify adventure evidence checks against one retained native run and corruptions of it.

Run with OPENFANTASY_ADVENTURE_MANIFEST naming a completed runner result. Missing input skips
discovery; a qualification command must provide it and inspect the actual pass/skip counts.
"""
import base64
import copy
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from check_native_adventure import (authenticate, closed_database, retained_rows, verify_journey,
                                    verify_pins, verify_reopen, verify_paused_database)


@unittest.skipUnless(os.environ.get("OPENFANTASY_ADVENTURE_MANIFEST"), "A retained native manifest is required.")
class AdventureEvidenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.manifest = json.loads(Path(os.environ["OPENFANTASY_ADVENTURE_MANIFEST"]).read_text())
        work = Path(cls.manifest["workDirectory"])
        cls.observed = json.loads((work / "keyboard.json").read_text())
        cls.reopened = json.loads((work / "reopen.json").read_text())
        cls.save = work / "adventure.sqlite"
        cls.before = retained_rows(Path(cls.manifest["sourceSave"]))
        cls.paused_before = closed_database(work / "before.sqlite")
        cls.paused_after = closed_database(cls.save)

    def test_complete_retained_native_evidence_is_accepted(self):
        verify_journey(self.observed)
        verify_reopen(self.reopened, self.observed)
        self.assertGreaterEqual(authenticate(self.save, self.observed, self.before)["authenticatedSubmissions"], 6)
        verify_paused_database(self.paused_before, self.paused_after)

    def assert_refused(self, mutate):
        changed = copy.deepcopy(self.observed)
        mutate(changed)
        with self.assertRaises((RuntimeError, KeyError, TypeError)):
            verify_journey(changed)

    def test_failed_native_run_cannot_be_accepted(self):
        self.assert_refused(lambda row: row.update(Status="failed"))

    def test_missing_care_cannot_be_accepted(self):
        self.assert_refused(lambda row: row["Adventure"].pop("Care"))

    def test_partial_images_cannot_be_accepted(self):
        self.assert_refused(lambda row: row.update(Images=row["Images"][:1]))

    def test_late_success_cannot_hide_a_draw_timeout(self):
        self.assert_refused(lambda row: row["Captures"][0].update(Seconds=5.1))

    def test_failed_draw_cannot_be_accepted(self):
        self.assert_refused(lambda row: row["Captures"][0].update(Drawn=False))

    def test_another_cargo_lot_cannot_be_substituted(self):
        self.assert_refused(lambda row: row["Trade"].update(LotId="substituted"))

    def test_return_to_another_place_cannot_be_accepted(self):
        self.assert_refused(lambda row: row["FinalFeet"].update(X=row["FinalFeet"]["X"] + 256))

    def test_duplicate_supply_identity_cannot_hide_behind_correct_total(self):
        self.assert_refused(lambda row: row["Adventure"]["Returned"].update(
            CraftedLots=[dict(LotId=row["Adventure"]["Returned"]["CraftedLots"][0]["LotId"], Quantity=1)] * 2))

    def test_paid_amount_cannot_diverge_from_returned_purse(self):
        self.assert_refused(lambda row: row.update(FinalBalance=row["FinalBalance"] + 1))

    def test_reopening_another_public_state_is_refused(self):
        changed = copy.deepcopy(self.reopened)
        changed["Adventure"]["PublicHash"] = "stale"
        with self.assertRaises(RuntimeError):
            verify_reopen(changed, self.observed)

    def test_unverified_reopening_is_refused(self):
        changed = copy.deepcopy(self.reopened)
        changed["Adventure"]["HistoryVerified"] = False
        with self.assertRaises(RuntimeError):
            verify_reopen(changed, self.observed)

    def test_receipt_bytes_must_match_the_actual_database(self):
        changed = copy.deepcopy(self.observed)
        changed["Adventure"]["Submissions"][0]["Receipt"]["Bytes"] = base64.b64encode(b"forged").decode()
        with self.assertRaisesRegex(RuntimeError, "receipt mismatch"):
            authenticate(self.save, changed, self.before)

    def test_original_history_cannot_be_replaced(self):
        changed = copy.deepcopy(self.before)
        changed["world_genesis"] = []
        with self.assertRaisesRegex(RuntimeError, "genesis changed"):
            authenticate(self.save, self.observed, changed)

    def test_stale_or_missing_dependency_is_refused(self):
        from check_native_adventure import digest
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "input"
            source.write_bytes(b"original")
            pins = {str(source): digest(source)}
            verify_pins(pins)
            source.write_bytes(b"changed")
            with self.assertRaises(RuntimeError):
                verify_pins(pins)
            source.unlink()
            with self.assertRaises(RuntimeError):
                verify_pins(pins)

    def test_immutable_reads_leave_no_evidence_sidecars(self):
        import shutil
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "original.sqlite"
            shutil.copy2(self.save, source)
            retained_rows(source)
            self.assertFalse(Path(str(source) + "-wal").exists())
            self.assertFalse(Path(str(source) + "-shm").exists())

    def test_paused_world_row_change_is_refused(self):
        changed = copy.deepcopy(self.paused_after)
        changed["tables"]["world_head"] = []
        with self.assertRaisesRegex(RuntimeError, "world_head"):
            verify_paused_database(self.paused_before, changed)

    def test_paused_recovery_generation_cannot_advance_twice(self):
        changed = copy.deepcopy(self.paused_after)
        row = changed["tables"]["controller_recovery_segment"][0]
        changed["tables"]["controller_recovery_segment"] = [(row[0], (int.from_bytes(row[1], 'big') + 1).to_bytes(8, 'big'), row[2])]
        with self.assertRaisesRegex(RuntimeError, "exactly once"):
            verify_paused_database(self.paused_before, changed)

    def test_paused_recovery_hash_must_authenticate_generation(self):
        changed = copy.deepcopy(self.paused_after)
        row = changed["tables"]["controller_recovery_segment"][0]
        changed["tables"]["controller_recovery_segment"] = [(row[0], row[1], bytes(32))]
        with self.assertRaisesRegex(RuntimeError, "checksum"):
            verify_paused_database(self.paused_before, changed)

    def test_paused_allowance_change_is_not_ignored_as_metadata(self):
        changed = copy.deepcopy(self.paused_after)
        changed["tables"]["controller_allowances"] = [(b'changed',)]
        with self.assertRaisesRegex(RuntimeError, "controller_allowances"):
            verify_paused_database(self.paused_before, changed)


if __name__ == "__main__":
    unittest.main()
