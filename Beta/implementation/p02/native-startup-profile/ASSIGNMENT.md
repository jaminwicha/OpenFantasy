# Native startup measurement assignment

Diagnose the actual failed coordinated native reopen at commit1581. Fable owns only this packet's
`profile_native_startup.py`, `WORKER_RESULT.md` and new diagnostic output folders, plus disposable
runtime copies outside Git. Astra owns integration, production source, architecture and shared status.
No other worker may edit these files. Other projects run on the desktop; never take focus or close them.

Use exact `fable[1m]`, Max effort, one 900-second/$9 bounded call with no automatic paid retry.
Measure full recovery and client attachment/presentation separately on a copy of the retained failed
save. Record CPU/wall time, runtime identity, GC/allocation where meaningful and original byte pins.
Instrument only disposable source copies. Preserve journal/archive admission and 30s opening/150s
probe/180s process bounds. A diagnostic timeout is evidence; it is not permission to relax acceptance.
Avoid broad rewrites or production optimization until the measurements identify the cost.
