# Source-backed cargo world and authoritative circuit

Implement a cohesive application increment toward the actual two-market cargo/escort loop. Source
revision: `bf0e5a3967b3c270be37b989c98a093ca73c8165`. Exact model `claude-opus-5-5[1m]`, one-million
context, Max effort requested. One request, **4,200 seconds / $40 client-estimated usage**. Astra owns
integration and commits. You are not alone in this checkout; do not revert, stage, commit or edit others'
work. No delegation, model fallback, installation, paid retry or scope expansion.

Read AGENTS.md, Beta/AGENTS.md, the current handoff, the spot-exchange integration plan and
CARGO_DEPENDENCIES/LEGACY_MARKET_INPUTS, world-composition/content/markets/locomotion architecture and
the relevant P02/P03 plan. The frozen whole campaign remains required. Create RESULT.md here first as
an in-progress marker. Then inspect actual APIs and write a compact DESIGN.md with canonical owners,
source transforms, admission/route decisions and concrete risks before substantive implementation.
Use the supplied technical-design and verification skills. Do not reload unrelated research.

## Outcome

1. **Admit real source merchants and goods.** Add a separate reusable fresh cargo-world admission
   using the actual retained Marla at (96,350), Kegga Toolwright at (347,140), their place identities,
   original payloads/shop lists/appearance/dialogue/quests and the shared rations definition. Preserve
   source hashes/IDs and make every transform and newly authored fact explicit. Preserve all original
   sources; copying their names onto Wren or relocating them to the farm is not import. Use the
   existing content pipeline and ordinary actor/custody/progression/market owners. Do not weaken the
   two-actor farm's admission; compose a distinct exact fresh content binding. Preserve the inherited
   farm/Wren quest/crafting/care/encounter meanings wherever the chosen genesis includes them.
2. **Finite supply and funding.** Admit declared finite merchant stock and accounts as versioned
   authoring; the original 200-gil quest reserve is not a market treasury. Keep player purchasing funds
   earned through actual actions when using the farm player. Ration source/production provenance must
   be real and typed; do not relabel berries or attach manufactured rations to an unrelated source.
   Keep legacy base prices untouched. Distinct local stock and authored rules may produce useful
   trade, but the proof must report actual prices/costs, not guarantee profit by changing source facts.
   Include source-input rejection for changed/missing identities or mismatched goods/policy.
3. **Legal continuous travel.** Connect the actual locations with admitted supported terrain and
   bounded real route legs. Retain coordinates, bodies, place identities, occupancy and ordinary
   movement costs. Crossing a region is real movement, never a teleport, direct state replacement,
   test-only position override, free travel, bigger arbitrary search budget or global path stub.
   Supply a reusable Host itinerary/leg coordinator only where needed; stop and revalidate when the
   next leg is obstructed or state changes. Missing route coverage is not proof of no route. The
   existing 16-region and local search bounds are fixed. Declare authored corridor geometry honestly;
   this is finite local qualification, not procedural geography, streaming or complete town design.
4. **Complete managed journey and recovery.** Through actual LocalWorldSession cutoffs and durable
   authority, earn the player's purchase funds, request/buy actual stock from Marla, carry the owned
   lot across the entire route, disclose/request/sell at Kegga, and return to the first place with
   changed stock, funds and learned prices. Independently check the exact source-interval/production
   lineage and all participants' money at relevant commits, the distance/region transitions, and
   ordinary archive crossings if they occur. Save/reopen at a meaningful mid-trip boundary and finish
   with the same cargo and world history; compare the preserved prefix and complete final state.
   Do not shorten the route to make tests pass. Exercise in-person/distant knowledge and blocked-leg
   behavior, actor parity/own control and stale trade refusal without hidden renewal. Reuse paid
   settlement, inquiry, quote, disclosure, receipt and replay mechanisms rather than another ledger.
   If a real threat can be exercised through an already admitted encounter along the route, retain
   its actual costs/aftermath and show it. Otherwise name the exact required threat/escort dependency;
   no simulated narration may stand in for combat, guard wages, traveling NPCs or multiplayer.
5. **Timeless architecture and continuation.** Add a source-linked cargo-circuit reference and update
   affected content/market/runtime/index pages and Runtime README in the same change. Explain fresh
   admission versus migration, current source mapping, itinerary ownership/restart, privacy, finite
   bounds and how native region presentation will consume this world. Preserve all required native,
   threat, escort, NPC-route, migration, multiplayer, infinite-frontier and full phase gates. Report
   concrete dependency or performance failures; do not silently reduce the circuit into a shorter
   demonstration. The fixed farm renderer is deliberately not used for this world; the next native
   binding must address real observed regions, multi-stall identity/choice and camera-relative drawing.

## Ownership

Create new runtime C# only under `Runtime/src/OpenFantasy.Hosting/Content/Cargo/` and, if needed,
`Runtime/src/OpenFantasy.Hosting/Simulation/Cargo/`. Use matching Content.Cargo/Simulation.Cargo
namespaces. New authored content may live under `Runtime/content/legacy/two-market-circuit/`; cite
the retained source-support records and distinguish authored admission from copied source records.
The Host already embeds content recursively; no project/package changes are needed.

New tests only under `Runtime/tests/OpenFantasy.Hosting.Tests/Content/Cargo/`, namespace
`OpenFantasy.Hosting.Tests.Content.Cargo`. Name the complete durable journey test class
`CargoCircuitJourneyTests`; other bounded source/route/admission tests use other classes there.
Do not alter existing tests/helpers or source packets to get a pass. Optional new reference pages
must remain under `Runtime/docs/architecture/`. Editable existing docs: content-and-composition.md,
world-markets.md, runtime-and-authority.md, README.md there, plus Runtime/README.md.
This packet: DESIGN.md and RESULT.md only. Astra owns pins/wrapper/handoff and integration evidence.

All existing Core/Host/client/source-content/tests/tools/project files are byte-pinned. No schema,
codec, historical save, profile relabelling, client art/input/movement redesign, authority shortcut,
new framework/assembly, C++/Rust/IPC or predecessor edits. If an existing API genuinely prevents correct
composition, return the precise dependency and useful partial code; do not duplicate an authority or
forge its history to evade the scope. Runtime C# must be modular, readable and object-oriented.

## Fixed checks

Only these exact Bash commands are allowed; use Read/Glob/Grep for discovery, not shell ls/find,
pipelines or redirects. Use Bash timeout covering the wrapper bound and observe terminal results
before returning. The shared lock serializes heavy work. No unfinished background test counts as pass.

`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/cargo-world/run_checks.py build`
`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/cargo-world/run_checks.py unit`
`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/cargo-world/run_checks.py journey`
`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/cargo-world/run_checks.py regression`

Build is the solution (240s). Each test selection has 600s. Unit selects new Content.Cargo cases
except CargoCircuitJourneyTests. Journey selects that class and supplies a fresh absolute
`OPENFANTASY_CARGO_CIRCUIT_EVIDENCE` directory. Export closed genesis/before/final snapshots and saves,
source/admission/route facts, actual command/recovery evidence and compact results there. Use no
inherited native-fixture environment. Regression covers affected existing Content/Simulation/Client/
exchange/proposal paths, excluding opt-in native authenticators and the separately qualified journey.
Every required selection must execute substantive tests with zero skips/failures. All checks pin the
same final source/content/binaries and preserve original tests, runtime, frozen specs, source DB and
predecessor index. Failed attempts remain immutable. No new native/device/human acceptance is implied.

Prefer focused development checks, then one final coherent regression. Return a compact result with
changed responsibilities, exact evidence paths, actual source/route/cargo/funding facts, failures,
unfinished gates and the next concrete integration. Leave useful partial work if limits prevent
completion; do not claim a completed circuit from a successful constructor or helper-only test.
