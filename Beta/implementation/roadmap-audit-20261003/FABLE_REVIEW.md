> Independent read-only opinion at ffa1f971 plus declared overlay. Some findings are incorrect or overstate evidence; read [lead disposition](FABLE_DISPOSITION.md) before using recommendations. Not a phase-acceptance record.

# Independent roadmap review, commit ffa1f971 plus declared recovery overlay

Read-only, no execution. Every "pass" below is as recorded in report files, not observed by me.

## Where the project really stands

**Bottom line.** The runtime has a genuinely strong P01-grade authority: deterministic C# core, SQLite single writer, exact receipts, journal replay, real process-kill recovery, four TLS peers. That part is real and over-proven. But no phase has closed, including P00 and P01, and P02 is far from its frozen exit. The last two days of work (roughly 40 commits, 2026-10-02 to 10-03) went almost entirely into a co-player "operated character" subsystem that the frozen P02 text does not require in that form, while most P02-mandated systems have zero source. The current playable default is still the profile-five farm; everything newer lives in separate saves, scenes and launch flags.

**Phase status, honestly stated.**

| Phase | Reality |
|---|---|
| P00 | Contracts and a first-loop source closure exist. Hardware rows "deliberately unfilled" (`Beta/implementation/p00/BENCHMARK_FIXTURES.md:7`); closure covers 31 roots of 532 records (`p00/migration-closure/README.md:3`). Not formally exited. |
| P01 | Authority, persistence, recovery: real. Missing exit items: Windows/Linux exports (`Beta/Runtime/README.md:242`), the 10,000-actor and city workloads, LANG-01–04 ("all cases planned/unrun", `BENCHMARK_FIXTURES.md:3`), prehistory/epochs, growth/diet/dispersal, reaction/phase definitions (plan `IMPLEMENTATION_PLAN.md:126-134`). |
| P02 | A thin vertical of the loop exists across fragmented worlds. See blockers below. |
| P03–P14 | Not started as phases. Standalone geology/liquid/structural components (watersheds, reservoirs, liquid cells, loads) are P01/P03 GEOR/SUB groundwork, not phase work. |

**What the uncommitted recovery packet actually shows.** Report files under `p02/delegated-control-recovery/qualification/` now record: first build exit code 1, later builds exit 0, archive-managed three Passed, process-crash six Passed, recovery-storage all Passed, control-hosting three Passed, and the second native-recovery run exit 0 with one Passed. The first native-recovery run's status did not appear in my bounded query. These remain unaccepted. Their own design limits apply: the matrix starts with an already-expired allowance and empty allowance rows, a controlled clock and one returning owner (`DESIGN.md:57-64`). This is a bounded save/restart proof for one profile, not the P02 "save/restart and network reconnect" exit, and it adds a 40th world profile rather than converging on one.

## Ranked findings

**1. The P02 exit is blocked by whole systems with no source, not by remaining UI breadth.** The plan's P02 text (`IMPLEMENTATION_PLAN.md:138-148`) requires items I could not find at all:

- NPC perception, belief, desire, commitment, memory loop with offscreen job reconciliation. Core has 25 `*Planner` classes and zero types named Belief, Desire, Perception, Memory or Intention. NPC autonomy today is policy tables (Wren's "medicine preference", scheduled trades, routed construction).
- Alpha zone chat and system notices with two-client delivery: zero matches for chat in `Beta/Runtime/src`.
- Interaction audio and one adaptive transition, plus the RQ-169/170 native audio fixture: zero `AudioStream` references in the client.
- Seasonal plant, herbivore, predator with real food; lava, cooling reaction, ice microfixture: zero matches for Lava, Herbivore, Predator, Season, Ice, Reaction in Core. Berry and oak regrowth exist; seasons and a food web do not.
- Artist kits (person, creature, animal, interactive object): the README calls current visuals "a provisional original-asset baseline" (`README.md:114`). No art lane evidence appears in any handoff.
- Missing-cargo investigation, field-action puzzle, traveling merchants with routes, remapping, scalable text, novice/journeyman/expert views: not claimed anywhere.
- Mine a tunnel beneath an occupied floor and remove a support in the playable loop: exists only as standalone descent/settlement fixtures and a dry breach divider.

The handoff's "Next dependencies" (`AGENT_HANDOFF.md:350-366`) list none of these. They list native paging, paid-quote details, catalog names, mouse dropdown breadth and aftermath-care screen proofs for the operated character.

**2. Domain fixtures are being treated as the game.** The README names 16 distinct save files, from `wrens-farm.sqlite` to `openfantasy-cargo-threat.sqlite`. The client has 7 `Native*.tscn` entry scenes. `tools/play.py` exposes 9 routes, and the cargo, retreat, threat, outcome and roster worlds are not among them (`README.md:795`). The host has 41 operator flags (`HostApplication.cs:45-105`). There are 56 `*Admission` classes and 38 supported snapshot codec versions (`metrics.json:91-130`). "The normal playable farm still selects profile five" (`README.md:285`) and "No production world or lived save upgrades automatically" (`AGENT_HANDOFF.md:369-370`). A new player double-clicking `Play OpenFantasy.command` sees none of the encounter, care, trade, cargo or multiplayer work. The composition pattern already exists in code: `CargoWorldAdmission` nests the farm plus Marla and Kegga, and `RoadThreatWorldAdmission` nests retreat plus cargo. What is missing is the decision to make one composed world the default.

**3. Prioritization drifted from D-13 into an unbounded loop.** D-13 requires disconnect, 30-second allowance, then authorized controller or NPC handoff (`DECISIONS.md:19`). The implemented result is a full consent, scoped-permission, selected-character, scoped-query, private-history-directory and shared-screen subsystem spanning profiles 36 through 40, with each packet generating new "remaining shared domain surfaces" for the operated case. The plan assigns retry, reconnect and membership boundaries to P03 (`IMPLEMENTATION_PLAN.md:138`). The handoff itself notes "Combat + traversal + recovery remains a provisional simple preset, not an owner-approved preference" (`AGENT_HANDOFF.md:375`). This lane should be declared sufficient for P02 at its current bounded evidence and parked.

**4. The one measured performance fact is a failure, and the real gate is unrun.** The only latency data is a four-player movement sample at 151.96 ms against a 150 ms lease with a 424.7 ms scheduling gap (`AGENT_HANDOFF.md:280-283`), two 600 s storage timeouts, and an unexplained raw-gift opening failure. LANG-01 through LANG-04 remain planned and unrun. The P03 exit requires closing them before broad P04 (`IMPLEMENTATION_PLAN.md:164`). Allocation reductions of 10 to 24 percent in replay are not evidence against this; the handoff correctly says so.

**5. Toolchain decision D-04 is unexecuted and now time-critical.** D-04 selects Godot 4.6 .NET plus .NET 10 LTS. Core, Hosting, tests and client still target net8.0; only Host targets net10 (`README.md:246`), and the README itself flags net8's approaching support end. No Windows or Linux export has ever been produced. This is a P01 exit item (Q-01) and a cheap, visible de-risking step.

**6. Content migration has barely begun and its denominator is moving.** P11 denominators are 123 NPCs, 45 quests, 63 items, 35 monsters, 25 regions (`IMPLEMENTATION_PLAN.md:244`). Admitted behavior covers Wren, Marla, Kegga, a Bandit Chief, Bramble Wolves and one quest. Meanwhile the predecessor is still being developed in the same checkout: the staged index adds `docs/MILESTONE_8.md` ("The Wide Water", procedural frontier, settlements, dated 2026-08-17) and new `src/Sim/SettlementGen.cs`. The plan requires reconciling newer Alpha deltas (`:244`). Without a dated predecessor snapshot for migration, P11 cannot have a fixed denominator.

**7. Verification ceremony is measurable and contradicts the project's own rules.** Evidence from the limited sample:

| Observation | Source |
|---|---|
| Full Core suite rerun 6 times on 2026-10-03 with an empty Core source/test/build diff | `metrics.json:131-171`, `core_source_test_build_contract_diff_empty: true` |
| 17 milestone branches dated 20261003 for 20 commits that day | `GIT_MILESTONES.md:25-41`, `GIT_ACTIVITY.txt` |
| Qualification artifacts committed to git | 51,201 files, 5.84 GB logical (`metrics.json:36-39`) |
| 132 p02 packet directories, 67 bespoke verify/finalize/check scripts | `metrics.json:46-50` |
| Handoff 3,998 words, milestone doc 11,775 words, README 10,466 words | `metrics.json:51-67` |

`Beta/AGENTS.md:129-131` says changes confined to Host fixtures do not invalidate identical Core evidence and forbids repeating an unrelated full suite for a "final build" label. The doctrine reserves milestones for "a reviewed vertical slice, major integration or other meaningful accepted checkpoint" (`GIT_MILESTONES.md:8-9`). Neither is being followed. The "2,360 distinct passes" style headline is mostly the same 1,885 Core cases; new coverage per packet is 10 to 33 cases (`AGENT_HANDOFF.md:75-76, 93-94, 116-117`). This does not inflate claims of completion, which the handoff is careful about, but it inflates reading time and repository size and produces no new information.

**8. Unsafe inference risk is low in wording, high in structure.** The handoff never claims phase closure, and that discipline is good. The structural risk is that a reader equates 274 commits, 40 profiles and thousands of passes with progress toward P02. Against the P02 text, I estimate roughly a third of the mandated behaviors have any bounded evidence, and none has human acceptance. I cannot give a defensible percentage and do not offer one.

**Scope faithfulness and which steering binds.**

- Pure C#: compliant. Zero `DllImport`, `unsafe` or native source files in `Beta/Runtime`. The 2026-09-20 owner decision (`Beta/AGENTS.md:47-52`) supersedes the plan's Rust-replacement branch at `IMPLEMENTATION_PLAN.md:9` and `:164`, but not the LANG measurement gate itself. The plan should be annotated, not edited.
- Stable OpenFantasy naming: compliant. Zero "Beta" tokens in runtime C#; assemblies are `OpenFantasy.*`. The 8 September override supersedes D-54 in source only.
- Time travel: `--inspect-recorded-world` is linear replay to an exact commit. The README lists accelerated queries, interior instants, prehistory, branch ancestry and playable travel as required work (`README.md:325-329`). No Fork, ChildBranch, Prehistory or Epoch types exist. RQ time-travel "cornerstone" and D-21 are untouched beyond this reader.
- NPC parity: real at the command-authority layer (Wren performs funded construction, trades, offers medicine through the same authority). Absent at cognition, kinship and relationships. D-14 and the kinship requirements are not started.
- Fluids, ecology, culture, crafting, transport, magic, death, planes: liquid cells and reservoirs are real standalone components; recipe crafting is real and small; comprehension profiles exist as schema. Ecology, culture, transport, magic, death with estate, planes: none. These are P03 through P10 and are correctly not claimed.
- UI steering of 2026-09-10 (icons, tooltips, capability-dependent screens) and the upkeep supplement of 2026-10-02 are later binding refinements that do not reorder phases. The wheel has provisional glyphs and History has tooltips; the supplement's own boundary section (`PLAYER_INTERFACE_AND_NAVIGATION.md:175`) is stale and should be refreshed, since inventory, recipe, market, care and history screens now exist.
- Frozen but obsolete: D-04's "qualify .NET 10" remains frozen and binding, yet unexecuted. The plan's P02 text predates co-player control and should be read with D-13 as the only requirement there.

## Recommended sequence and limits

The goal is owner-visible coherence fastest without weakening any gate.

1. **Freeze the operated-character lane now.** Record it as D-13 sufficiency for P02. Move paging, paid-quote detail, catalog names and dropdown breadth to P03 reconnect/membership work. Accept the recovery packet only within its stated limits.
2. **Compose one default world and make it the launcher default.** Nest farm, worksite, watercourse and breach, encounters with outcomes and care, gifts, markets and the cargo road into one genesis the way profile seven nests the construction basis. Fresh default save; no lived-save upgrade needed for development. Retire the per-feature `play.py` flags to a "fixtures" subcommand. This is the single largest visible-progress lever and requires no new gameplay law.
3. **Fill P02 holes in dependency order on that one world.** Four native clients joining and reconnecting during the loop (roster and TLS exist). Zone chat and notices. Labeled temporary interaction audio plus one adaptive transition, keeping the RQ-169/170 quality gate open. A seasonal plant, herbivore and predator microfixture using the existing regrowth and resource law. Lava and ice on the existing liquid-cell flow. Then the NPC cognition loop for Wren, Marla and Kegga with offscreen reconciliation, which is the largest missing core piece. Then novice/expert views, remapping, scalable text, the investigation and the puzzle.
4. **Execute Q-01 in parallel.** Decide net8 versus net10 per D-04 and produce one real Windows and one Linux client export before November 2026. Cheap, visible, and it de-risks everything after.
5. **Start the art lane as its own owner.** The plan requires it from P02 and nothing exists. Even a graybox person and creature kit with native motion would change what the owner sees.
6. **Freeze the predecessor snapshot for migration.** Date-stamp the Alpha source used for P11 denominators; treat Milestone 8 and later as a separate, later reconciliation.
7. **Cut ceremony using the project's own rules.** One milestone per player-visible slice, not per commit. Skip Core reruns when the contract diff is empty; cite the prior run. Move qualification binaries to an artifact store with hashes in git. Replace the narrative handoff with a one-page ledger: requirement or P02 line, status in four states, exact evidence path, next witness. Keep packets as they are.

**Forecast.** Not defensible. The plan says to estimate schedules only after P02 and the first P04 to P06 authoring batches supply measured throughput (`IMPLEMENTATION_PLAN.md:298`). No content throughput exists: roughly five NPCs and one quest migrated in 26 days. The only honest forecast is conditional: if items 1 and 2 are done, the owner can see one coherent inhabited valley with trade, combat, care and four players within the next coherent slice, and P02's remaining list becomes finite and visible.

**Uncertainties.** I did not run anything. I did not read `GIT_MILESTONES.md` beyond line 132, the architecture reference, or the two concurrent critic reports. My absence checks used specific identifiers in `Beta/Runtime/src` and the client; a system implemented under different names could have escaped them, though the README and handoff do not claim any such system. The first native-recovery run's result is unknown to me. The git sample covers five days and cannot speak to person-hours, billing or completion.
