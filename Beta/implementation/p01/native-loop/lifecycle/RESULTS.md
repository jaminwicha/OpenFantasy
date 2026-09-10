# Native lifecycle results

The coordinator executed the critic-authored harness on 10 September 2026 UTC using the pinned macOS ARM64 Godot .NET editor. **All eight native lifecycle/reopen stages passed**, along with the two exact tool-version checks. There were no native/script error logs or nonzero exits.

Each startup action ran twice with a fresh save. Both removal cases required multiple open attempts before the asynchronous owner released its complete database: four and five attempts respectively. The probe therefore exercised actual overlapping startup/ownership, rather than only inspecting a save whose opening had already completed and closed before the test began. Every subsequent fresh process acquired the lease on its first attempt.

All six complete observations recovered revision **0**, advancing steps **0**, player wood **8**, and no reading hold. Every canonical state hash was `2e4323e70faab23fb0796c75a0f094a37e4300a7966185a2a3ecd2ba31a8c58d`. Actual native SQLite was **3.53.4** with exclusive ownership; native assertions also required WAL/FULL/foreign keys. All four closed database files had the same bytes and hash.

The run began at `2026-09-10T01:14:09.341458+00:00` and completed at `2026-09-10T01:14:15.326778+00:00`. The native reported managed runtime was **.NET 10.0.12**, while the portable projects target net8.0, and the display driver was headless. The original checker report, native stage JSON and four saves are retained under `evidence/`. `VALIDATION.json` binds their hashes, the reviewed client/session inputs and the actual native client output assemblies.

CS-01 (competing opening/close adoption) and CS-02 (tree-exit session/lease release) now have successful native regression evidence in addition to static review. CS-03 (readiness callbacks after host failure) remains closed by the separately retained guarded-callback review and managed host-failure tests; this run does not claim native fault injection or focus-event coverage.

The historical client/session review manifest remains unchanged. Its `FarmGame` source hash preceded the camera framing adjustment, and its `Bootstrap` hash preceded this lifecycle dispatch branch. The final source was re-inspected for the shutdown/readiness ownership guards and bound in this validation manifest. Neither of those subsequent edits changes the reviewed host timing or authority semantics. Historical managed-test binary hashes remain evidence for their original run, not claims about later native builds.

No pre-fix native failure is claimed. The three original findings were obtained by static independent critique; the retained native harness was added after the coordinator corrected them. This bounded pass does not establish rendered quality, other desktop exports, power-loss behavior or exhaustive thread interleavings.
