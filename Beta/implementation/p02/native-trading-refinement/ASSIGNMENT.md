# Independent native trading review and refinement

You are a fresh reviewer of the native trading foundation, not its original implementer. Review input
commit `35b0797fa78ac73557ad4ed704af72f12c8bfd49` (foundation `ff120fb`) before repairing it. The owner
explicitly requests Opus 5.5: exact `claude-opus-5-5[1m]`, one-million context, Max effort. One bounded
request: **3,600 seconds / $25 client-estimated usage**. Astra integrates. No retry, fallback or recursion.

You are not alone in the checkout. Read the shared AGENTS, current handoff, prior native-trading
INTEGRATION_REVIEW/RESULT/FOUNDATION_CHECKS, the living UI supplement and affected architecture.
Use the supplied UI/input/verification skills and local Godot 4.6 C# API XML for targeted questions.
Do not reread unrelated research, scan the parent temp directory or delegate another worker.

First create RESULT.md here with an in-progress marker to prove edit access. Before editing runtime,
write INPUT_CRITIQUE.md: concrete findings/risks, exact source references, attempted counterexamples and
priority. Challenge the initial delivery independently, including actual focus/navigation, exact intent,
quote/commodity matching, timing, history privacy and the distinction between injected and physical use.
Do not manufacture findings or call passing counts proof of unexercised behavior.

## Required outcome

1. **Real native row selection/navigation.** Complete the existing trading native journey with the named
   keyboard/controller/pointer route for lot and receipt dropdown selection as well as its buttons/wheel.
   Remove the keyboard substitution in controller/pointer runs. Use actual InputEvent/Control paths,
   not Select(index), SetFocusedItem, EmitSignal(Pressed) or direct semantic handlers to manufacture input
   success. Starting fixture focus is allowed; qualify actual navigation between relevant panel controls
   instead of programmatically grabbing every destination. Assert which rows/actions were reached and
   that cancel, focus changes and repeats do not submit unintended commands. Retain precise route traces.
2. **Native rejection contrasts.** Add actual expired/stale-offer, insufficient-funds and issuer-moved-
   after-review cases. Prove the intended reason with all other permissions/readiness/stock conditions
   satisfied, attempted UI confirmation doing nothing, actual preserved goods/funds, and explicit recovery
   (re-review or a new request when lawful). Never silently requote/retarget. Keep original 1,200-step offer
   terms and all deadlines. A closed, honestly labeled real-authority fixture prepared near expiry is
   acceptable; a synthetic view, shorter production window, state teleport, fake receipt or skipped replay
   is not. Separate preparation from actions actually exercised natively. Independently authenticate the
   saved journals, expected command set/refusals and relevant identity-keyed state, not just report JSON.
   Three focused boundary journeys may share one named injected device route; the normal journey still
   qualifies all three full selection routes. This does not claim physical devices or multiplayer Wait.
3. **Readable player experience.** Replace ordinary UI references to save segments, revisions, step counts
   and implementation/multiplayer caveats with relevant player language. Keep price, fees, total, remaining
   time, affordability and pause consequences visible/inspectable. Technical timing belongs in docs and
   optional diagnostics, not the primary trade card. Fix singular goods wording. Preserve icons, tooltip
   and focus explanations, deliberate confirmation and a small immediate surface. Inspect screenshots;
   do not claim human aesthetic acceptance. No actor/terrain art or movement redesign.
4. **Input findings and architecture.** Repair substantiated UI/input defects within scope and add useful
   contrasting tests. For future multi-stall support, don't imply that matching merchant alone proves a
   quote belongs to the chosen commodity/stall. Fix a safe local guard where applicable and explicitly
   record remaining generalization limits. Keep architecture timeless and evidence/progress here.
5. **Evidence economy.** Existing native reports serialize full terrain views at each observation.
   Where no verifier needs that duplication, prefer compact hashes plus relevant actor/quote/lot/input
   facts and retained canonical save/journal artifacts. Preserve information needed to diagnose timing
   failures and prove identities; don't erase earlier reports or replace actual evidence with assertions.

## Owned deliverables

Existing editable client scripts: FarmGame.cs, FarmGame.Market.cs, FarmClientSession.cs,
FarmInputRouter.cs, FarmInputMap.cs, FarmQuickWheel.cs, FarmQuickWheelModel.cs, FarmWheelDisc.cs,
FarmActionOffers.cs, FarmActionOffer.cs, FarmMarketPanel.cs, FarmMarketDraft.cs, FarmMarketTerms.cs,
MarketActionOffers.cs, MarketTradingPlayProbe.cs, NativeTradingEntry.cs. New trading-specific scripts/UIDs
and relevant market scenes may be added. Only the four market-*.svg UI glyphs may change; other art is
protected. Application C# stays modular, human-readable and object-oriented.

Tools: existing Runtime/tools/check_native_trading.py and new check_native_trading_boundaries.py only.
Tests: NEW files under Runtime/tests/OpenFantasy.Hosting.Tests/Client/Trading/Refinement/ and
Content/Trading/Refinement/. Existing tests/helpers stay byte-exact. Test csproj allows only additive
new engine-free source links in its existing format; no package/property/old-link changes.
Docs: Runtime/README.md; architecture native-trading.md, presentation-and-interaction.md,
runtime-and-authority.md, README.md. Immutable input snapshots are in the private inputs directory.
This packet: INPUT_CRITIQUE.md and RESULT.md only.

Core, all Host application source/content policies, existing tests/probes unrelated to trading,
project.godot, client csproj, launch script, historical packets/specs/source assets/SQLite, Git/index,
context/pins/wrappers/handoff and other projects are outside edit scope. No installs or foreign-app focus.
If a required repair needs another owner, report the exact dependency rather than weaken a gate.

## Fixed verification commands and interfaces

Only these exact Bash calls are allowed. Read/Glob/Grep do discovery; **no `ls` Bash calls**, shell
pipelines/redirects or alternate commands. Observe each terminal result (Bash timeout up to 600,000 ms)
and do not overlap heavy jobs. The shared lock coordinates checks; every failure remains in a new report.

`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/native-trading-refinement/run_checks.py build`
`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/native-trading-refinement/run_checks.py unit`
`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/native-trading-refinement/run_checks.py regression`
`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/native-trading-refinement/run_checks.py fixtures`
`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/native-trading-refinement/run_checks.py native`
`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/native-trading-refinement/run_checks.py native-auth`
`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/native-trading-refinement/run_checks.py legacy-native`
`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/native-trading-refinement/run_checks.py boundary-native`
`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/native-trading-refinement/run_checks.py boundary-auth`

Build pins all managed/native binaries, then tests require identical source/binary maps. Run focused
checks while developing and one relevant final regression after code settles. Legacy-native now runs
all three modes, satisfying its unchanged aggregate verifier. No empty/skipped required selection passes.

New NativeTradingBoundaryFixtureTests exports into OPENFANTASY_NATIVE_TRADING_BOUNDARY_FIXTURES, with
manifest.json and closed canonical saves/genesis/before/metadata for its scenarios. This is a separate
output root from the existing trading/gift fixtures: never write into their nonempty directories.
The fixture wrapper invokes all three exports sequentially. New boundary-native tool accepts
--dotnet PATH --godot PATH --fixtures DIRECTORY --output NEW_JSON --rendered. Report status and
managedAuthenticationManifest, execution/fixture pins, actual paths and scenario coverage. Existing
native process/probe/opening deadlines remain 180/150/30 seconds. Use unfocusable background clones,
never request focus or change source project.godot; observe actual native focus across the run.

New NativeTradingBoundaryResultTests reads OPENFANTASY_NATIVE_TRADING_BOUNDARY_RESULTS and authenticates
actual effects/refusals independently. The normal NativeTradingResultTests remains unchanged, so its
five-command buy/show/sell proof must still pass. Boundary result tests explicitly skip without their
opt-in input and must substantively assert with it. New unit tests must not rely on native evidence.

Return a compact critique/disposition, changed responsibilities, final exact report/image paths,
failures/friction and honest remaining limitations. If time expires, preserve useful partial work and
report what remains; don't claim a route or requirement that the evidence doesn't exercise.
