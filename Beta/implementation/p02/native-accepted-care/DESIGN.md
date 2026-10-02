# Native accepted care — narrow design

Status: design, written before implementation. Not evidence.

Approach: reuse the qualified NativeEncounterOutcome observation/receipt chain without changing
Core/Hosting law or ordinary client sources.

1. `NativeAcceptedCareFixtureTests` exports an unplayed, uncalibrated
   `CargoOutcomeWorldAdmission.CreateEmbedded` world (closed SQLite save + exact genesis bytes +
   manifest). No HP calibration, forced damage, grant or prepared encounter.
2. A separate opt-in scene `NativeAcceptedCare.tscn` with probe scripts under
   `client/scripts/NativeAcceptedCare/**` drives ordinary native input only (selection, movement,
   first-wolf entry, public Wait/action choices, E/A care request) and records each submission's
   pre-input terms, command bytes and receipt, plus helper/stock/HP observations and bounded
   post-draw captures. The window is unfocusable only inside this disposable background probe.
3. `check_native_accepted_care.py` runs the active process, then a separate reopen process, and
   invokes `NativeAcceptedCareResultTests` (managed authentication by full replay from exact genesis
   of every saved journal entry) and emits `status: passed` only after all assertions plus the
   named `managedAuthenticationManifest`.
4. Result tests authenticate: genesis identity, native submissions = saved journal, Wren's separate
   own-controlled treatment action (actor, item/lot interval, exact dose retirement, patient HP
   increase), same-episode continuation, later outcome, spent dose not replayed after reopen, and
   alteration counterexamples (helper attribution, dose attribution, acknowledgment-only).
