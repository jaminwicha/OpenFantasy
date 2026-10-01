# Road-threat corrections: design

Worker: Opus 5.5 / 1M / requested Max (as dispatched). Base: delivery `48fb19e` plus the lead's two regressions
(prepared `f36d6c0`). Authoritative findings: [CRITIC_DISPOSITION](../road-threat/CRITIC_DISPOSITION.md) D1–D3, R1–R6.
No Core rule, codec, schema, root field, profile or release law changes. Outcome/evidence: [RESULT.md](RESULT.md).

## 1. Hostile entry owns its cutoff (D2)

`LocalWorldSession.TickAsync` keeps every genuine hold in its existing order: pause, retained consequence, care,
player lifecycle choice (entry/release), active encounter. Only after those, an eligible hostile entry is selected
**whether or not an ordinary player request is pending**. When the chief's entry commits, the pending ordinary request
(selected for an exploration step that no longer exists) is completed at that same cutoff with the ordinary boundary
refusal (`AuthorityDispatchException`), so its Task never hangs, nothing of it executes against the new boundary and
no movement joins the entry batch. A proposal the authority would refuse still commits nothing and the ordinary step
(with the pending request) proceeds unchanged. Reading hold, pause and care keep selecting nothing hostile.

## 2. Threat projection is observation-bounded (D3)

`LocalRoadThreatView.Project` publishes a row only while the observer has a supported observation of the initiator:
`Space.QueryInteraction(observer, initiator)` is `Reachable` under the existing 768-subunit (three-cell) interaction
bound — same floor, centre range and clear centre sightline. No new range, no use of the 24-cell terrain observer.
`Watching` = observed beyond entry reach; `InReach`/`Recovering` = observed within reach (ready / paying debt);
`Engaged`, `Incapacitated` as before but also only when observed. Worlds without a hostile scenario keep `null`
(omitted, exact old bytes); the threatened world publishes the observed subset (possibly empty). Warning copy becomes
"Danger nearby"; the tooltip says it attacks *you* (the scenario's only target).

Contrasts: real lived range states (farm, Marla, road start 7 cells, lane 809 subunits unobserved, 572 observed,
reach), a whole-walk binding check (row present iff the Core query is `Reachable`), and a declared fresh calibration
of the spatial owner only (`WithStructures` wall → occluded; `WithActors` floor change → different floor).

## 3. Boundary holds, reopening and pending input (item 3, R1, R2, R4, R5, R6)

New tests under `Simulation/RoadThreat` and `Client/RoadThreat`: pending refused/accepted-shape input at the reach
boundary vs. outside it; chief readiness after an actual disengagement (both debts measured, preserved through a
complete-journal reopen, entry exactly when eligible); reopen mid-confrontation and mid-retreat with continuation;
incapacity Outcome reopen and refusal; `OpenCargoThreatAsync` create/reopen/refuse. Victory (chief at 0 HP) is not
reachable without inventing state; it stays a documented terminal-release blocker under the existing Core law.

## 4. Native keyboard join from a fresh farm fixture (item 4, D1)

`NativeRoadThreatFixtureTests` additionally exports `farm/` — the same fresh threatened genesis, closed, revision 0,
paused. The keyboard route talks to Wren through the dialogue keys, walks to Marla, buys through the stall panel, walks
the outbound lane, sees the observed warning before contact, meets the chief, strikes, takes the hit, inspects/cancels
and commits Retreat, detours, sells at Kegga and is reopened. Controller/pointer keep the declared near-road fixture.
`NativeRoadThreatResultTests` replays every journal and authenticates exact player commands (inspection/cancel sends
nothing), active-retreat `EncounterResolution` samples with due player and intervening `TacticalAdvance`, last sample =
record sequence, final feet = report = authored destination, and keyboard reward/purchase/sale lots, intervals,
parties and funds. Checker limits stay 30/720/750/300/330/90/900.

## 5. Copy and references

Remove the planning codename from `RoadThreatSources` and `road-threat.md`; update road-threat, encounter-lifecycle
and README warning/priority text; keep pursuit, aggro/leash, rewards, respawn, wages, party, multiplayer, migration,
frontier, art/physical/human and P02 gates explicit.
