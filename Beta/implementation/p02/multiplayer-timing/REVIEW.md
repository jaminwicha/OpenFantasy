# Source review and integration

The native independent reviewer first inspected host projection/authority paths and identified
duplicate lazy-root encoding, repeated terrain projection and full preflight/commit preparation
as hypotheses. Separate hash timing was added before choosing an optimization. The captured hash
spans were small relative to projection and observation work; none of those hypotheses is recorded
as the original timeout cause.

A bounded native worker authored EventSource/capture infrastructure and the owned-occupancy
optimization/tests. Astra integrated instrumentation, reviewed actual changes, repaired two new-test
xUnit analyzer issues, and ran every recorded build/test to terminal status. Native agents use Astra
capacity; they are not Claude-credit offloading. No provider retry or silent substitution occurred.

The lead rejected an arbitrary `ReadOnlyCollection<bool>` fast path after recognizing that a
read-only wrapper can have deceptive custom enumeration. The final path is limited to a sealed
defensively owned array. The existing generic path remains available and rejects a concrete custom
list whose advertised count disagrees with its null-bearing enumeration.

Final independent source review found no actionable correctness defect. It checked immutable array
ownership, unchanged declared property/wire types, collection-owner plus cell counting, maximum
child depth, empty collections at that boundary, and which failure wins when collection and aggregate
budgets conflict. The lead verified all 21 new occupancy cases and existing affected families.

The reviewer noted one diagnostic limit: `CommitSelectedBatch` spans do not wrap direct controller
event commits. The architecture reference now states that those commits are included only in their
enclosing cutoff/disconnect intervals. Diagnostics cannot be interpreted as a complete authority CPU
profile. The critic ran no checks; executable evidence is in `FINAL_CHECKS.json` and raw qualification
reports. Neither review nor a traced passing aggregate closes the unchanged normal aggregate failure.
