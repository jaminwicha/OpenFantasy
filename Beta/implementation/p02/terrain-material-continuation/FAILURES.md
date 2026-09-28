# Retained validation failures

## Legacy terrain refusal diagnostic

The first full Core run in [core-01.log](qualification/core-01.log) completed with 1,534 passes,
one failure and no skips. `TerrainMaterialRegistryTests.Terrain_admission_refuses_a_standalone_material_cut_until_archived_coverage_is_integrated`
correctly rejected the generic continued owner, but its diagnostic no longer began with the complete
legacy sentence asserted by the existing test.

The initial correction restored only `complete inline`, leaving the rest of that sentence different.
[Core02](qualification/core-02.log) again reported 1,534 passes and that same single diagnostic failure.
The final correction preserves the exact sentence `Terrain material admission requires complete inline
production history.` and appends the qualified-registry alternative. The test and admission guard are
unchanged. Build03 and the focused registry regression qualify this final literal-only change; Core02
qualifies the substantive replay change. The original reports and source/execution pins remain retained.

## Bounded workload cost

The unchanged 512-refine/demote case passed in both full Core runs, but took 694.17 seconds in Core01.
Repeated prefix snapshot/history reconstruction was removed after a separate WIP checkpoint and
independent delta review. The same case passed in 145.61 seconds in Core02; the full run fell from
719.11 to the duration recorded in [its command report](qualification/core-02-command.json).
These are local executions on a shared machine, not controlled performance benchmarks or closure
of a production latency gate. No test limit, operation count or rejection assertion was reduced.

## Tool and delegation friction

The optional native runtime-worker performance follow-up hit the account usage limit. Astra implemented
the bounded change directly and the independent critic reviewed it. The earlier read-only process sample
was not executed because automatic approval review hit a usage limit; no sample evidence is claimed.
The original full Core process completed and its report was recovered after the interruption. No test
process was killed or restarted to turn an incomplete run into a pass. External Claude routes were not
automatically retried and no credit savings are inferred.
