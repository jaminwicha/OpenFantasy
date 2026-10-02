# Multiplayer timing investigation

The unchanged managed Server aggregate has failed both the ten-second first-full-view startup
and five-second four-player movement observation. The same four-player test passed separately.
The Controller aggregate also previously timed out waiting for durable teardown following a
fragment reassembly timeout. Retain all original evidence in `../general-recovery/qualification/`.
No passing repeat establishes contention as the cause or qualifies production latency.

The diagnostic change adds disabled-by-default Host EventSource stages plus an opt-in bounded
Server test capture. Existing tests, parallel execution policy, assertion limits and runtime
deadlines remain unchanged. The first experiment runs the exact retained Server-managed
selection with tracing, using the existing heavy-check lock and source/binary preservation.
Stage durations will determine the next experiment; do not repeat aggregates without a question.

Authority is unchanged. Trace identifiers and Stopwatch readings never become world state.
The key distinction is receipt of a movement sample versus its selection within the existing
150 ms lease, and commit time versus view/observation construction and delivery. Tracing itself
has overhead, so final qualification must also run with the listener disabled.

Pre-change ref: `codex/checkpoint-before-multiplayer-timing-20261002T174718Z`, resolving to
`d9e6ec577cb8abe70c14fdc1895840ffd1a619c7`. `PRESERVATION.json` pins the 688 old test/fixture files,
23 frozen specification files and exact unrelated staged predecessor diff. This checkpoint records
bounded recovery work with the timing gates open; it is not an all-green acceptance claim.
