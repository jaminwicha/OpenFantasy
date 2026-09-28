# Retained failures and corrections

- `qualification/build-01.log`: the new journey helper lacked the `OpenFantasy.Core.Care`
  namespace import. Adding it resolved compilation; build-02 and build-03 passed.
- `qualification/focused-01.log` and its TRX: both new care/reward cases incorrectly read
  `WorldSnapshot.EncounterActions` after encounter release. Exploration deliberately exposes no
  active encounter actions. The assertion now reads retained `EncounterLifecycle.Actions` on both
  sides. This changed the test's history access, not simulation admission or game rules. Both
  feedback cases already passed. The corrected four-case run and evidence are in focused-02.
- `qualification/pending-source/` preserves the actual pending backup exported before the first
  test failure; it is not the accepted native input. `pending-source-02/` is the backup from the
  passing run. Neither is overwritten by the broader regression.

- `qualification/regression.log` and its TRX: VSTest could not bind its local communication
  socket under the sandbox (`SocketException (13): Permission denied`); no tests executed. The
  unchanged command was retried with the required local test-runner permission as regression-02.

No deadline, acceptance threshold, source statistic, inventory quantity, currency rule or replay
validation was relaxed. Native pending headless and rendered checks passed on their first attempts.
