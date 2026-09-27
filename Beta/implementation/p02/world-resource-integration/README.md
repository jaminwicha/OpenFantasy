# Complete-world resource composition

The in-memory care-enabled world joins actual attack damage/MP, funded quest XP/refills and finite
restorative treatment to one ordered actor-resource history. Each batch retains its real domain
cause, replays exact before/after actors, and participates in the same complete transaction as
currency, material custody, encounter randomness and readiness. The previous care-only last-attack
HP exception is removed; later resource changes no longer rewrite the original damage fact.

The [architecture reference](../../../Runtime/docs/architecture/world-resource-history.md) describes
ownership, causal checks, canonical bytes and continuation obligations. [Review disposition](REVIEW.md)
records two independent defects and their fixes. Verification results and source manifests are in
`qualification/`; development failures remain alongside passing results.

## Qualified scope

Actual in-memory `WorldStepProcessor` journeys cover raw and crafted doses, defeat, acknowledgment,
restoration and resumed combat; zero/no-level/level XP after combat; earned stats before later combat;
finite currency payment; stale-request and later complete-root rollback; actual sorted action position;
missing paired effects, forged causes/draws/anchors, conflicting episodes and malformed actor references.
Canonical restore is checked at meaningful boundaries. The previous profiles remain in full regression.

[Verification](qualification/verification.json) retains **2,039 distinct passing managed cases**:
1,355 Core and 680 earlier Hosting/client cases, followed by final Core and all 19 expanded affected
journeys. The final solution builds with zero warnings/errors. The native critic's two findings
are corrected, 215 architecture source/reference links resolve, and all 23 frozen specs, 53 earlier
milestones and the exact unrelated 19-file predecessor index are preserved. The implementation is
recorded by `codex/milestone-world-resource-integration` without switching the development checkout.

The full regression source and final review source are recorded separately. Only
`WorldResourceAdmission.cs` and `WorldResourceJourneyTests.cs` changed after the broad Hosting run;
the former returns immediately for worlds without this new owner. Final build, full Core regression
and the expanded focused journey suite qualify those changes. This composite evidence is not described
as a second full Hosting run against final bytes.

## Open gates and next dependency

`WorldGenesisAdmission` still refuses care-enabled roots. This is an in-memory integration milestone,
not durable/native care acceptance. Before enabling the profile, implement authenticated end-of-step
archive continuation, retain retrievable earlier causes, and reconcile the schema/profile, journal
commit kinds, preparation and replay checks in [host boundaries](HOST_BOUNDARIES.md). Then qualify
actual ordinary and aftermath treatment, duplicate/reopen/kill recovery and native presentation.
The 256-batch segment envelope is not an acceptable permanent gameplay cap. Roster/rule transitions,
broader anatomy/medical policies, multiplayer, performance and all full phase/production gates remain open.

## Preservation and delegation

Before direct edits, checkpoint `codex/checkpoint-before-direct-resource-integration-20260927T192548Z`
retained care WIP over `cf66dcd`; the frozen external-review snapshot is
`codex/checkpoint-resource-integration-review-20260927T193742Z`. Earlier snapshots remain untouched.

The original [Fable assignment](TASK.md) ended in denied writes with no implementation or tests.
The exact Opus 5.5/1M/Max review timed out without output or usage after the client update to 2.1.280.
Neither run demonstrates credit savings. Astra completed the integration directly and used one useful
native independent critic. No automatic paid retry or silent model substitution occurred.
