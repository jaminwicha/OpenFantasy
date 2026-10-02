# Bounded observation allocation and timing checkpoint

The owned terrain occupancy validator avoids per-cell boxing while preserving its complete
validation contract. The same 131,072-cell validation workload allocated 3,145,984 bytes before
the change and zero bytes afterward; the unchanged generic boolean-array control still allocates
3,145,984 bytes. These numbers exclude construction and apply to this validator, not whole-game
memory or throughput. [Measurements](MEASUREMENTS.md) retain the wider diagnostic observations.

`LocalTerrainOccupancy` is a sealed internal collection that defensively owns its array. Only this
trusted type receives bulk primitive validation. The owning collection and all cells still count
toward the same traversal budget; child depth, collection size and competing-failure precedence
remain exact. Arbitrary read-only wrappers keep element traversal. Declared properties, packed
wire bytes, default diagnostic arrays, cross-field validation and canonical re-encoding are unchanged.

Opt-in `OpenFantasy-Host-Timing` events distinguish host work, movement sampling, observations,
replica application and cleanup. The bounded Server test listener records process context without
writing from timing callbacks. It is disabled by default. Trace IDs and wall times never enter
authority, commands or saves. Direct controller commits have only enclosing cutoff/disconnect
spans; absence of a batch-commit span is not proof of no authority work. The architecture references
describe the capability and limits.

## Verification and remaining failure

[Final checks](FINAL_CHECKS.json) pin the warning-free build, actual source and binaries:

- Occupancy validation: 21 pass, including aggregate/depth bounds, deceptive wrappers, immutability,
  canonical/default JSON parity and allocation comparison.
- Networking: 265 pass, including the occupancy cases and the retained transport/codec tests.
- Managed client: 179 pass; shared-session selection: 26 pass; controller selection: 73 pass.
- Normal managed Server aggregate: **76 pass, one failure, one expected Windows-only skip**.
- The same Server selection with tracing: 77 pass and one Windows-only skip.

There are 584 distinct cases with passing evidence, including 22 new cases. This is **not an
all-green aggregate**: the normal four-player TLS journey again missed its original five-second
movement observation at retained test line 104. The traced run did not reproduce it. No deadline,
input lease, workload, old test or parallelization policy was relaxed. The allocation improvement
does not establish or repair that timeout's cause. Earlier startup/disconnect/TLS failures remain
retained and unexplained; production/concurrent-load acceptance remains open.

The baseline occupancy run deliberately failed its allocation assertion while 18 correctness cases
passed. The later 21-case run also covers the two competing-budget contrasts. Two initial builds
failed new-test xUnit analyzer rules; the narrow assertion-style repairs and all logs remain retained.
No Core change, new native journey, whole-suite, platform, human or phase qualification is claimed.

All 688 original test/fixture files, 23 frozen specification files and the exact unrelated 19-file
predecessor index remain intact. All 1,273 checked architecture/design links resolve. Independent
native source critique found no actionable defect; [review](REVIEW.md) records its scope. No external
Claude request or claimed credit offload occurred while the account spending-limit failure holds.

## Preservation and next work

The initial checkpoint `codex/checkpoint-before-multiplayer-timing-20261002T174718Z` preserves
`d9e6ec577cb8abe70c14fdc1895840ffd1a619c7`. The before-optimization WIP checkpoint
`codex/checkpoint-before-observation-validation-20261002T180505Z` preserves
`68eac65e7f3cfcd971f87ac50bf9ed04f26aa519`, including diagnostic source and its traced aggregate.
The verified optimization scope is preserved by `codex/milestone-observation-validation-20261002`;
the working checkout remains `codex/runtime-foundation`.

Next investigation must capture the failed movement sample without assuming that tracing or a later
pass explains it. Compare actual receipt/selection and lease age with host/observation scheduling;
do not repeatedly rerun aggregates solely to seek a green result. Controller reassembly versus
durable cleanup still needs its own diagnostic coverage if reproduced.

The separate [pending-care disposition design review](../pending-care-disposition/DESIGN_REVIEW.md)
preserves the next gameplay dependency, including patient cancellation, helper decline, archive
closure witnesses and safe return of control. It is a proposal, not implemented or accepted rules;
profile 38/Care V5 are not reserved by that note. Full campaign requirements remain unchanged.
