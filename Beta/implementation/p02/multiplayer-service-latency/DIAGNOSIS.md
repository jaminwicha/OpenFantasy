# Movement expiry and serialized projection cost

The preserved baseline aggregate reproduces the original failure with tracing enabled:
[Server report](qualification/server-managed-20261002T192025015869Z/report.json),
[test output](qualification/server-managed-20261002T192025015869Z/command.log), and
[actual test-process trace](qualification/server-managed-20261002T192025015869Z/host-timing-11211-c69db3248a7c45acbffac5b1aa752c58.json).
It has 76 passes, one failure and the expected Windows-only skip. No source changed during the run.

## Captured failure

The roster host is `743bec412622409fb181f86f7cf9d546`. Its third moving player's port is
`b65b8a151f6a4f20843e9b2800ba2703`, bound to actor `79dddd9ea31bf745606a5473ea76ed14`.
The server accepts its sole `(0,-32)` sample at 17.338887 seconds relative to capture start.
The first sampling age is **163.3869 ms**, beyond the unchanged 150 ms lease. There is no
`movement.selected` event for this port during its wait. Earlier players were selected with ages
15.1621 ms and 121.595 ms. The failure is the original test's line-104 five-second observation wait.

Relative to the failed sample's receipt:

| Work | Boundary in milliseconds |
| --- | ---: |
| Revision-four third-player projection began | -38.565 |
| That projection ended | 76.457 |
| Fourth-player projection ended | 107.286 |
| Previous cutoff ended | 107.314 |
| Next cutoff began | 163.362 |
| First movement sampling age | 163.3869 |

The input arrives while the serialized host is publishing four views from its previous commit.
After that work, the scheduler discards missed deadlines and schedules its next cutoff at least
50 ms later. The sample expires before its first selection. This is evidence of the captured
failure mechanism, not proof that every earlier timeout had this cause. A stale sample must remain
stale: no input lease, scheduler, movement, test assertion, parallelization or deadline is changed.

An unrelated test application was observed using CPU during this run. That observation does not
establish causality or justify changing its process. Stage times include scheduling and GC delay;
they do not measure exclusive CPU time. No other application's work was stopped.

## Isolated component workload

The unchanged `ProjectionCostTests` builds the same four-player production genesis, warms each
projection, then measures 32 calls (eight rounds of four actor views). Its before/after reports
retain actual source and binary pins:

- [Before reuse](qualification/projection-20261002T192519719062Z/report.json)
- [After reuse](qualification/projection-20261002T192907713525Z/report.json)

| Projection, 32 calls | Before allocated bytes | After allocated bytes | Before wall ms | After wall ms |
| --- | ---: | ---: | ---: | ---: |
| Worksite | 7,510,976 | 117,952 | 85.182 | 0.142 |
| Liquid | 7,473,408 | 80,384 | 123.173 | 0.181 |
| Complete four-player views | 16,003,392 | 1,217,344 | 215.407 | 3.385 |

The fixed complete projection workload eliminates **14,786,048 allocated bytes**. Timing numbers
are observations on the shared desktop, not a whole-game frame-rate or production latency claim;
the after selection also includes the new correctness tests. The measured method itself is unchanged.
Initial cache misses and simultaneous first access still pay construction, potentially more than
once under a racing conditional-weak-table factory. The improvement applies to reused immutable
regions, which movement preserves; terrain successors still require their own first projection.

The full-view encoding assertion checks repeatability. Separate independent terrain projections
compare every occupancy value and default JSON against the original copying construction; complete
network regression checks retain canonical encodings and validation. No correctness assertion is
replaced by the timing measurements.

## Change and boundaries

`LocalTerrainOccupancy.Project` shares one owned immutable occupancy by exact
`TerrainRegionSnapshot` reference. Both worksite and liquid projections use it. Weak keys keep
no old region or world root alive, including when an old public view is retained. Equal region
bounds/revisions on different branches never constitute a match. Wire rehydration continues to
copy caller arrays and does not populate the cache. No simulation authority, resource or permission
is cached or skipped.

Seven new correctness tests cover reference reuse and independent old-construction parity,
divergent same-revision excavations, decoded snapshot separation, old-view immutability,
wire-copy ownership, concurrent observers, source collection, warm allocation and separate
four-player inventories/wire scope. The eighth new case is the fixed component probe.

Archive-query timeouts remain a separate open issue. Current saved quote retrieval can perform
two complete verified replays (outcome lookup and its publishing root); this is a next measurement
candidate, not an established cause or authorization to bypass replay/authentication. Native/platform,
full survival and production latency/scale gates remain separate.
