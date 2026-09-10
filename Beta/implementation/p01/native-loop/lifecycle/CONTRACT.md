# Native startup lifecycle qualification

This harness exercises the actual Godot C# game node, its asynchronous local session creation, and the shared SQLite authority. It provides bounded regression checks for closing during startup and removing the game node during startup. It is a headless native lifecycle check, not a rendered-art or user-acceptance test.

## Exercised behavior

`FarmLifecycleProbe.Start(parent, savePath, reportPath, mode)` accepts three explicit modes. The coordinator-owned `Bootstrap` dispatches `--farm-lifecycle <save> <report> <mode>` to it.

| Mode | Native action | Required evidence |
| --- | --- | --- |
| `remove-opening` | Add a real paused `FarmGame`, then remove it and queue it for deletion immediately after synchronous `_Ready`, before a frame or await. | No public view existed at removal. Actual shared-service reopening eventually acquires the exclusive database lease and recovers the exact admitted genesis. |
| `close-opening` | Add a real paused `FarmGame`, then call `RequestClose` before a frame or await. | Initiation report precedes close. The actual game shutdown exits zero without Godot/script errors; a separate native `inspect` process supplies final save evidence. The initiation report alone cannot pass the check. |
| `inspect` | Open an existing save through `SqliteWorldAuthority.OpenExisting`. | A fresh process acquires the exclusive lease and recovers the exact expected genesis. The mode never creates or resets a missing save. |

Successful reopening requires world revision zero, zero advancing steps, eight units in the player's admitted inventory, no reading hold, and equality with the entire canonical admitted genesis hash for the fixed farm seed and player name. The authority also reports WAL journaling, FULL synchronous mode, enabled foreign keys, exclusive ownership and the loaded native SQLite version. State assertion failures are final; only temporary opening failures during asynchronous creation/ownership are retried in the removal mode.

The async opening task may finish before or after the lifecycle action, but its result cannot be adopted by a frame before the action. This checks ownership during opening/adoption rather than claiming control over every operating-system scheduling interleaving. The session starts unready and the game starts paused, so no host step is eligible before shutdown. The probe waits for successful recovery of an existing complete save; absence of a file or an initial failed open can never count as success.

## Runner and retained evidence

`tools/check_farm_lifecycle.py` runs the already built native client. It validates the exact configured SDK/editor pins, sets both `DOTNET_ROOT` and the leading `PATH` entry to the supplied SDK root, and launches every lifecycle/inspection stage in a separate native process. Two repetitions per lifecycle action are the default; the bounded CLI permits one through four. Every action uses a new save path, and every successful action is followed by another fresh-process inspection. All observed genesis hashes must agree.

Each native inspection has a 25-second outer timeout; waiting for startup ownership is bounded to 15 seconds plus an in-flight SQLite open. Each external native process is limited to 40 seconds. Nonzero exit codes, native/script error logs, missing or malformed reports, state mismatches and timeout all fail the run. JSON reports, process logs and save hashes remain in the output artifact directory; source and tested-binary hashes belong in the associated validation manifest after execution.

## Scope limits

The harness does not manufacture a host-failure trace inside Godot to test focus/readiness callbacks. The guarded readiness helper is covered by the separate static lifecycle review, while managed session tests cover actual host-failure propagation. It also does not qualify rendered layout, native focus transitions on all operating systems, platform export packages, arbitrary editor reloads, power loss, or production-scale save lifetimes. Passing these probes closes the exercised startup-shutdown regressions only.

The critic authored this harness after independently identifying the three client lifecycle findings. The client and session implementation remained owned by other agents. This is independent verification of those implementations, with implementer verification of the harness itself; no previously failing native run is claimed unless retained execution evidence records one.
