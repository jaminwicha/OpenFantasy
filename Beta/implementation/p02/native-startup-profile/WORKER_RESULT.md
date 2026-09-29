# Native startup measurement — worker result

Status: **PENDING** (no measurement has run yet; nothing below is a result).

## Scope

- Diagnose the failed coordinated native reopen at commit 1581 on a consistent, byte-pinned copy of
  the retained failed save; the original save, sidecars and active report are read-only inputs.
- Measure full recovery and client attachment/presentation separately with instrumentation inserted
  only into disposable source copies outside Git.
- Owned files: `profile_native_startup.py`, this `WORKER_RESULT.md` and new diagnostic output folders
  in this packet. No production runtime, test, architecture, shared handoff or Git index/ref changes.
- Bounds retained: build 180 s, opening 30 s, probe 150 s, process 180 s; assignment 900 s / $9.
- Only shell command: `python3 Beta/implementation/p02/native-startup-profile/profile_native_startup.py [arguments]`.
