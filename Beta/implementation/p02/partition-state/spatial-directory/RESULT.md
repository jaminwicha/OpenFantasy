# Paged occupancy and selected spatial detail

This increment prepares actual movement geometry from bounded occupancy metadata and exact selected
archives without first constructing a complete local scene. It supplies a reusable read boundary for
PLAN-STATE; it does not install a partition owner or bypass ordinary world admission/publication.

`SpatialOccupancyPage` represents explicit complete or empty metadata on a signed 16-cell index grid.
References pin geometric scope, identity, conservative bounds and complete actor/structure detail.
`SpatialOccupancyReadPlan` checks queried-page copies, enforces page/detail budgets, retains exact
page pins and rejects missing information. Empty-page pins detect arrivals; unrelated page changes
can agree. The provider remains responsible for entities omitted from every supplied page, placement
admission, authority and stable reads. Metadata construction cannot establish those facts itself.

`SpatialActorArchiveV1` adds a bounded self-contained geometric archive using the existing actor
encoding, including body/form/condition. Expected-reference decoding checks bytes before detail and
requires exact canonical reconstruction. The existing actor/structure encoding method bodies are
unchanged; only their visibility becomes internal for reuse. World codecs, rule profiles and ordinary
launch behavior are unchanged. This archive is not a new world persistence format.

The Hosting reader loads only selected actors under cumulative byte limits and resolves pinned
structures. `SpatialMovementDependencies.FromDirectory` joins exact mover, scope and query bounds,
then uses the existing movement kernel. Conservative structure envelopes are filtered by actual boxes
after loading. Complete admission, control/cargo/time reads and ordinary action ordering still apply.
The joined world prerequisites continue to capture an admitted complete world.

## Observed behavior and limits

The [selected-I/O result](check-03/io-counts.json) comes from 200 separately archived actual geometric
presences, each admitted in its own local placement fixture. The returned catalog retains references
and 402 metadata pages, not all actor payloads. Preparation reads four pages, one selected actor archive
of 1,047 bytes and one terrain archive containing 768 cells. A separate initial mover-discovery read is
explicitly counted. The actual candidate moves west by 32 subunits and agrees with the intended pose.
No complete 200-actor LocalSpatialSnapshot or WorldSnapshot is constructed.

This measures selected I/O only. Fixture setup writes all geometric archives and retains the complete
metadata catalog. It does not measure total/peak memory, deep NPC state, global ownership, unloaded
simulation, city/endurance workloads or throughput. Structure bytes are not included in the actor-byte
budget. The page/detail/envelope limits are preparation constraints, not shipping population limits.

Core cases cover signed addressing, missing versus empty pages, arrivals/departures, cross-page
consistency, scope/key/identity mismatches, finite metadata enumeration, budgets, exact archives,
actual actor collision, structure bearing surfaces and hollow-envelope overfetch. Hosting cases add
file-backed selection and refusal for missing, stale, corrupt and over-budget detail without partial
results. The ordinary terrain and joined movement prerequisite regressions also pass.

## Verification and retained failures

- [Core build](build-01/process.json): clean, 3.860 seconds within 120 seconds.
- [Core spatial selection](check-02/core.json): 169 passed, zero failures/skips, 6.806 seconds within 180.
- [Hosting selection](check-03/hosting.json): 11 passed, zero failures/skips, 9.438 seconds within 180.

There are **180 distinct passing cases**, including 15 new cases. Final builds report no warnings or
errors. The first test compile failed because a new fixture called internal helpers; its page builder
was corrected to use the public contract. The next Hosting compile failed the existing xUnit2013
analyzer because a new assertion checked collection count instead of `Assert.Single`; only that
assertion was corrected. Both failures are retained in check-01/check-02; neither executed Hosting
tests. The passing Core selection was reused after the Hosting-only assertion change. No unchanged
retry, rule relaxation or deadline extension was used.

No ordinary movement algorithm, world interpreter, old test, root format or admission limit changes.
Focused spatial and joined Hosting regressions cover this additive boundary; older complete-suite and
native evidence retain their original source scope. No new full-suite, native, independent, platform,
human or phase acceptance is claimed. Independent architectural review remains open while orchestration
is paused. Direct lead inspection is not an independent critic.

## Roadmap and preservation

The October17 review's R1/R2/R7 remain the reason for PLAN-STATE. This selected-read increment does not
close them. The next integrated proof must supply authoritative directory completeness/publication,
actual owner transfer with cargo, absent-detail funded work, atomic commit/recovery and original history,
with named-hardware measurements. NPC-01 remains the next inhabited-world behavior after that decision.
All other review dispositions, full phases and frozen requirements remain unchanged.

[The pre-change checkpoint](CHECKPOINT.json) preserves c024b315 on
`codex/checkpoint-before-spatial-directory-20261017T230015Z`. The accepted marker is
`codex/milestone-paged-spatial-reads-20261017`; work remains on `codex/runtime-foundation`.
Unrelated predecessor staging, the October5 advisory, frozen baseline and prior raw packets are excluded.
[Verification](VERIFICATION.json) records source/binary pins, protected-input checks and documentation.

## Documentation review

- **Plan/spec:** CURRENT_ROADMAP, PLAN_STATE_COMPARISON and AGENT_HANDOFF record measured selected I/O and the remaining integrated proof. No new phase, shipping limit, manifest decision or acceptance shortcut is introduced; the recent review remains fully dispositioned, with its future deliverables open.
- **Reference:** World/geography documents page ownership/completeness obligations, canonical pins, explicit emptiness, conservative invalidation, archive/read budgets, structure overfetch and the complete-world admission boundary in timeless terms.
- **Accuracy/completeness:** Lead compared new types, existing actor/structure encodings, movement selection, tests and observed counters. Metadata is distinguished from detail and authority; separate mover discovery, all-metadata fixture residency and unchanged complete-world capture are explicit. No city/NPC or partial-simulation claim follows from the geometric population.
- **Checks:** VERIFICATION.json records terminal test outcomes, authored whitespace, documentation link/field checks and protected-input/source hashes. Source-based review supplements the mechanical checker; independent and full-scope gates remain open.
