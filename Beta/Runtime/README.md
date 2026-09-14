# OpenFantasy runtime

See the [architecture reference](docs/architecture/README.md) for state ownership, runtime bindings,
data flow and enduring design contracts. [Documentation navigation](docs/README.md) separates that
reference from build instructions, preserved specifications and implementation evidence.

`OpenFantasy.Host --inspect-watershed-network <prepared-network.bin>` validates and reports a finite
macro network archive. It checks exact member derivation, adjacent downstream links, acyclic routes,
composed contribution counts, bounded terminals and shared-face identities. This read-only operator
does not change a gameplay save or create terrain/liquid quantities. The
[network contract](docs/architecture/world-and-geography.md#domain-contract-finite-watershed-networks)
describes its ownership and bounds.

`OpenFantasy.Host --inspect-watershed-terrain <prepared-terrain.bin>` validates a bounded dry geometry
candidate derived from an exact network, integer cell scales and elevation datum. Natural heights,
macro ancestry and local window coordinates survive exact archival reconstruction. The
[mapper contract](docs/architecture/world-and-geography.md#domain-contract-dry-macro-to-local-geometry)
explains negative coordinates, window partitions and its separation from lived terrain. These candidates
do not change the default farm, allocate materials or create rivers. `tools/check_watershed_terrain.py`
checks independent archives, malformed inputs and the maximum source/window through the actual host;
its maximum fixture comes from `OPENFANTASY_WATERSHED_TERRAIN_EVIDENCE`. The full verifier wires this
alongside the existing network checks.

`OpenFantasy.Host --inspect-terrain-materials <terrain-materials.bin>` validates a standalone funded
material archive. A finite slab binds exact source intervals to coarse or detailed custody; loading
detail cannot mint material, and demotion preserves stock already moved elsewhere. The
[material allocation contract](docs/architecture/world-and-geography.md#domain-contract-funded-terrain-material)
defines physical calibration, exclusive ownership, identity and capacity bounds. This is a core
representation dependency, separate from live terrain admission or player mining. The full verifier
uses `OPENFANTASY_TERRAIN_MATERIAL_EVIDENCE` and `tools/check_terrain_materials.py` for independent
interval checks, retained cap fixtures and malformed archives through the real read-only host.

`OpenFantasy.Host --inspect-material-registry <material-registry.bin>` validates 1–64 disjoint funded
allocations over one shared inventory. It rejects overlapping ground, reused source units, conflicting
calibration and registry-wide identity collisions. The normalized archive stores terrain and custody
once. The [registry contract](docs/architecture/world-and-geography.md#domain-contract-shared-material-allocation-registry)
describes shared budgets and atomic validation. `tools/check_material_registry.py` consumes
`OPENFANTASY_MATERIAL_REGISTRY_EVIDENCE` fixtures through the full verifier. This domain composition is
separate from playable mining, production retirement and durable world admission.

This is the retained development runtime: a portable C# core, shared durable hosting library, headless executable, Godot C# client and executable tests. The native farm now connects player/NPC movement, Wren's original opening dialogue, a finite six-wood handover and save/reopen through one authoritative world. Terrain, bodies, language, custody, dialogue and time persist together. The gathering profile additionally admits real source tools, finite oak/berry reserves, earned effort, depletion and advancing-step regrowth. The production profile turns actual ingredients into rations and recoverable offcuts, retains consumed raw/derived rights, and upgrades an existing gathered save on a verified copy. The quest profile adds source-derived class/stats/XP, finite gil wallets and Wren’s funded six-wood agreement. Acceptance, reserved delivery, 420 XP and 200 gil commit once and survive a preserved upgrade/reopen. The construction profile connects the delivered wood to Wren’s routed movement and 200 authoritative work steps, creating one small physical seedling guard and three offcuts. Work and material geometry persist together; reading holds and pause stop work. Automated native journeys qualify both fresh games and progressed-save continuations. Food use, general building, Wait combat and multiplayer remain pending. The [increment record](../implementation/p01/native-loop/WORLD_CONTRACT.md) and [preparation packet](../implementation/p00/README.md) distinguish measured behavior from full phase exits.

## Build and verify

Use Python 3, the exact .NET SDK in `global.json`, and the .NET edition of Godot pinned in `toolchain.json`. The SDK executable must have access to the required managed runtimes. A standard non-.NET Godot editor cannot run this project.

From this directory:

```sh
python3 tools/verify.py --dotnet /absolute/path/to/dotnet --godot /absolute/path/to/Godot
```

The verifier checks the actual tool versions, independently regenerates all retained independent numeric, command, package, custody, scheduling, history, exposure, anatomy, production, observation, language, authority, terrain, progression, quest and construction fixtures, restores locked dependencies, builds Debug, requires every discovered core and hosting test to execute and pass, exercises host contract inspection against valid/malformed inputs and retained source-packet hashes, imports the client, and runs a headless Godot smoke check. It compares host/client primitive results and records the managed runtime observed inside each process. It also kills the dedicated host before/after an actual SQLite commit, reopens and retries without duplication, refuses a competing process, and checks matching durable results and actual native SQLite settings inside Godot. The native farm probe also routes movement, talk, give and close through the real client and compares the paused public view after process restart. A zero process exit does not excuse missing tests or Godot error output. Results and raw process output appear in `artifacts/verification.json`, with a fresh TRX report for each run. Optional `--forbidden-token TEXT` checks text input contents and all input paths for a prohibited name; binary assets are hashed and reported separately; the report states whether that scan was requested. This remains local binding evidence, not a performance, rendering, networking or desktop-export qualification.

The portable test suite can run independently:

```sh
dotnet restore tests/OpenFantasy.Core.Tests/OpenFantasy.Core.Tests.csproj --locked-mode
dotnet test tests/OpenFantasy.Core.Tests/OpenFantasy.Core.Tests.csproj --no-restore
```

On the qualified local macOS development host, double-click `Play OpenFantasy.command`, or run `python3 tools/play.py`. The launcher selects the pinned tools, performs a locked client build/import and opens the retained normal world; `--prepare-only` checks preparation without opening a gameplay save. On another machine, supply `--dotnet` and `--godot` (or `OPENFANTASY_DOTNET` / `OPENFANTASY_GODOT`) for the exact installed tools. It never resets an existing save. This development launch route is not a platform export.

Alternatively, build Debug and open `client/project.godot` with the pinned .NET editor. Put the selected SDK directory on PATH and set DOTNET_ROOT to its runtime root before launching the editor; both matter for editor SDK discovery and native runtime selection. The current source-based visuals are a provisional original-asset baseline, not the approved production art target. Move with WASD/arrows or the left stick; E/controller A talks or closes your conversation, G/controller X accepts or completes Wren’s addressed quest (outside a conversation it asks you to speak first; older profiles retain ordinary gifts), F/controller Y gathers from a nearby plant with its required owned tool, C/controller B packs two berries and one wood into two rations plus one wood offcut, and Esc/Start pauses. Closing saves through the host; reopening retains the world. The default local save is `user://worlds/wrens-farm-construction.sqlite`. When absent, startup first continues an existing `user://worlds/wrens-farm-quests.sqlite` on a verified copy, admitting one finite owned joinery kit and an unstarted project without changing old work, stock or rewards. If no quest continuation exists, the same preserved chain works backward: When absent, startup upgrades a verified copy of an existing `user://worlds/wrens-farm-production.sqlite`; an existing gathered world at `user://worlds/wrens-farm-work.sqlite` first receives its preserved production continuation. Each earlier file and its complete history remain intact. Existing selected construction saves take precedence. A failed or unsupported upgrade, or orphaned source sidecar, stops opening; it never silently resets the world. A new game admits the construction profile directly. Delivering six eligible raw wood gives 420 XP and 200 gil once, reaching level 3 with 38 residual XP and HP78/MP14 from the admitted level-one Human Squire. The supplies remain reserved until Wren reaches the crop plot and completes the joinery. Close the conversation to let him work; keep the plot and his route clear. This is a small sapling guard, with no claimed orchard-wide wind protection. Its provisional atlas panel has physical collision, but a dedicated work animation and sound kit are still pending. The earlier exploration world remains at `user://worlds/wrens-farm.sqlite` and can still be opened with `--legacy-farm`.

`tools/check_native_farm.py` exercises those actual Godot input/view adapters and reopens the same durable world in a second process. Its default is headless; `--rendered` also retains native screenshots for a separate visual review. This proves the small walk/talk/give connection, not Wren's complete quest or the full first-prototype loop. `tools/check_native_gathering.py` exercises real gather input, finite yields, a paused resource, exact process recovery and subsequent regrowth; its rendered option retains source-asset screenshots. `tools/check_native_production.py` runs three actual native processes: gather berries under the retained rules, upgrade the saved world and craft, then reopen and gather/craft again. It checks that the old save bytes remain unchanged, outputs survive restart, and paused projection fields remain exact. `tools/check_native_quests.py` prepares a progressed production save, upgrades a copy, accepts and completes the actual quest, then reopens and verifies a repeated completion cannot duplicate its reward. Its `--rendered` option captures the native HUD and dialogue. `tools/check_native_construction.py` checks both a progressed quest-save copy and a fresh construction-profile game. The fresh family performs the same real gathering, ration crafting and addressed acceptance/delivery before the routed build, conversation interruption, partial/completed process recovery and body collision/walk-around checks. The integrated verifier includes these probes alongside the lifecycle checks; retained qualification reports establish which exact revision and journey passed.



Open the pack with **I**, controller **View/Select**, or the **Pack** button. Inspect actual owned tools, materials and crafted supplies with hover, arrow keys/D-pad, Tab, or item selection. Inspection never equips, consumes, crafts or transfers anything. Counts aggregate display groups while keeping source/crafted and protected stock distinct; the underlying lots are unchanged. Item names and details cover the admitted farm catalog, with an explicit unfamiliar-item fallback. Native item icons, equipment/use actions and richer management remain future work.

The pack displays the local host’s running, waiting or paused status. Opening it stops player movement/input but leaves the existing time policy in effect; a running world continues. Opening from pause preserves that pause. Escape, I, View/Select, B or Close unwinds only the pack. Focus remains inside the screen, returns to the prior valid HUD control, and held navigation requires release before walking resumes. Selection is local to the current client and owned actor; no UI click is replayed after restart. These controls have injected native regression coverage, not physical-controller or full multiplayer acceptance.

Choose an explicit field target with **Q** or the controller's **right shoulder**, or use the
HUD's **Next** button. **R**, **left shoulder** or **Clear** restores automatic contextual actions.
The target name and field marker identify the selection. It remains selected when another plant
becomes nearer; exhausted or absent targets explain why their action is unavailable. Talk/Give
on a selected plant never redirect to a person. Target selection is transient client state;
reopening a save starts with automatic targeting. These controls use the same pause, conversation
and focus gates as other farm inputs. Physical gamepad and general pointer targeting qualification
remain separate from the injected native-event checks.

The farm HUD presents captured action offers: target, cost and any known blocker come from the
current public view, and buttons and hotkeys submit that same intent without silently choosing
another target, recipe or ingredient lot. Focus/hover inspection is read-only. The host still
adjudicates legality and stale revisions. `tools/check_native_offers.py` checks unavailable-action
inspection plus actual gather, craft and conversation input against a disposable world;
`--rendered` retains screenshots and checks that visible controls remain inside the viewport.
The integrated verifier includes this journey. This is the current small farm interface, not
the complete wheel, rebinding, management-screen or physical-controller acceptance suite.

Open **Quick actions** with **V**, **right-stick click**, or the **Actions** button. The small
farm wheel presents Talk, Gather, Craft and Give with original provisional glyphs. Point to a
sector, use arrows/WASD, D-pad or a deliberate left-stick tilt to inspect its exact target, cost
and blocker. No action is selected initially. **Enter**, controller **A**, or the **Confirm**
button commits the inspected offer; selecting a sector or clicking its center never acts.
**V**, **Escape**, **Start**, **right-stick click**, **B** or **Close** cancels the wheel only.
Focus loss or controller disconnect also cancels without submission.

Opening the wheel stops player movement and owns input while the existing world clock policy
continues. An existing pause remains paused; close the wheel before resuming. Pinned targets
and four cardinal slots stay stable while permitted previews refresh. Unavailable choices
explain their blockers. A changed or absent target cannot silently become another target, and
the host still rejects stale commands. Directions held during inspection must release before
walking resumes. This is a bounded farm wheel, not a saved favorites library, nested categories,
general capability discovery, remapping UI or full device/scaling acceptance.

`tools/check_native_wheel.py` checks injected pointer, keyboard, analog and controller input,
blocked inspection, time and movement isolation, real gathering, focus/disconnect cancellation
and exact process reopening on a disposable save. `--rendered` captures four native states;
the rendered fixture requests focus once after native startup settles and never reacquires it
during its journey. The integrated verifier runs the headless check. Physical-device experience,
production art and usability acceptance remain separate.

On a shared desktop, other game/editor tasks can take foreground focus and interrupt ordinary
rendered probes. Headless checks avoid that dependency but still share CPU/memory. For controlled
UI/image evidence, the wheel and action-offer runners accept **`--rendered --background-render`**.
Only these explicit disposable probe invocations can select that mode. They request a non-focusable
window and detach OS focus notifications from the fixture's readiness; deliberate fixture focus
changes still exercise the same cancellation/readiness handlers. Reports record both logical and
native window focus plus the binding mode. Normal launches and headless probes retain native focus
bindings. Initial window activation is platform-dependent; the fixture never runs a focus-grab loop.

This mode proves rendered behavior under controlled injected input, not actual OS focus switching
or physical devices. Those require a coordinated foreground interval. Do not close another task's
applications, loosen deadlines or call an interrupted journey a pass. Older gathering, crafting,
quest and construction fixtures also never reclaim focus; use a coordinated foreground interval
for their rendered runs, or their default headless mode. Captures wait for the actual
completed rendering frame so a state label cannot silently describe a stale viewport image.

## Outdoor daylight

The farm now samples an editable outdoor light palette from the same permitted calendar used
to present the world. Dawn warms the scene, daylight preserves its colors, dusk cools gradually
and moonlight keeps terrain and people readable. The HUD and pack keep their own brightness;
the selected-target marker is an unshaded interface cue. Presentation never advances the host,
changes resource rules or consults a second clock. Pause and dialogue holds retain their existing
time policy, and reopening a saved instant restores the same light.

The existing 20 Hz exploration cadence advances 1.8 world seconds per step: a nominal full day
takes 40 minutes of running exploration. Pauses and waiting extend that wall time; this increment
does not change pacing or implement sleep/camp. Edit `client/assets/lighting/outdoor_daylight.tres`
in Godot to tune the native Gradient; its opaque midnight endpoints must match. The small
`FarmDaylight` scene/component owns sampling, separate from world authority and UI. This is a
provisional outdoor palette, not approved production art, geographic/seasonal sunlight, weather,
interior illumination or physical visibility. Those remain separate planned work.

`tools/check_native_daylight.py` starts disposable source-farm fixtures at declared dawn, noon,
dusk and night times, advances the ordinary host through dusk/midnight, and reopens the retained
save with exact world/light and unchanged file bytes. It never adjusts a normal save or accelerates
gameplay. `--rendered` captures native frames for visual review; the integrated verifier runs the
headless checks. The 40-minute value is a pacing calculation, not a measured full-day playtest.

## Project boundaries

- `src/OpenFantasy.Core`: portable deterministic primitives, immutable command/receipt contracts, pinned package metadata, finite counted custody candidates, calendar queues and exact historical request boundaries, anatomy/condition definitions, material recipe validation, exact input retirement and transformed-output entitlements, experienced-time arithmetic, scoped resource-knowledge projection and actor-owned comprehension profiles, finite source/control admission and seeded stacked terrain with retained excavation. It has no engine or third-party package dependency. See its [byte and arithmetic contract](src/OpenFantasy.Core/CONTRACTS.md).
- `src/OpenFantasy.Hosting`: shared net8.0 SQLite adapter, durable counted and composed-world streams, identity history, strict journal recovery, embedded source-pinned farm admission and the serialized local session. Its 20 Hz clock has no input-driven acceleration or catch-up debt. It has no Godot dependency or IPC layer.
- `src/OpenFantasy.Host`: the retained headless entry point. `--inspect-foundation` exercises the same core. `--inspect-command <command.bin>` validates a bounded canonical envelope, `--inspect-package <manifest.json> <payload-file>` validates metadata and payload bytes, and `--inspect-custody <snapshot.bin>` checks counted snapshot invariants. Those inspection diagnostics issue no gameplay receipts. Local world operations below run the real durable counted authority; network authentication, session grants and gameplay projections remain pending.
- `client`: the independent Godot project and C# presentation adapter. Godot owns rendering, not authoritative outcomes.
- `tests/OpenFantasy.Core.Tests`: independently generated reference vectors and behavior/edge tests.
- `tests/OpenFantasy.Hosting.Tests`: actual temporary SQLite worlds, player/NPC transfers, concurrency, restart, corruption and independent critic regressions.
- `content`: the retained source dependency packet and versioned farm admission. The full source closure is embedded byte-for-byte for runtime use without a source checkout. Wren, the farm locality, finite wood and opening utterance now have admitted behavior; the remainder of the preserved packet still requires coupled conversion.
- `tools`: reproducible local verification. Build artifacts stay untracked.

Only the client references the engine. The solution's Debug configuration builds all development artifacts. Release maps the client to Godot's **ExportRelease** and the ordinary .NET projects to Release. Building either configuration does not create a distributable Godot export; actual templates, export presets and supported-device execution remain required. In particular, a macOS build does not establish Windows/Linux support.

## Candidate toolchain and portability

The current SDK is 10.0.401. Core, Hosting, tests and client target net8.0; the headless entry point targets net10.0. These are distinct from the managed runtime Godot actually loads. The local candidate installation contains runtimes 8.0.31 and 10.0.12, and the smoke report records which was selected. Godot.NET.Sdk is pinned to 4.6.0 with locked NuGet dependencies. Desktop framework/export selection must be qualified in P01; net8.0's approaching support end makes that an early decision, not a production support promise.

`toolchain.json` retains verified official download metadata for the initial macOS ARM64 machine and candidate export templates. Install tools side by side. When adding an older runtime to a newer SDK directory, retain the SDK's root launcher and copy only the runtime's versioned `shared/Microsoft.NETCore.App` and `host/fxr` directories. Download checksums prove archive identity; actual builds/runs prove only their recorded exercised paths. Other OS/architecture packages and mobile workloads require their own qualification.

Names, persisted tags and namespaces are stable product/domain identifiers. Temporary planning names do not belong in code or project metadata. Keep future domain models in a few clear assemblies; do not generate an assembly or empty interface for each design concept.

## Admitted watershed world

The sixth composed-world profile retains one immutable prepared watershed alongside the full
construction basis. `WorldWatershedAdmission.CreateGenesis` requires an unplayed construction root,
matching world/branch/realm scope and collision-free macro identities. Admission pins the exact
prepared bytes. Ordinary steps, dialogue holds, construction, restart and recorded-history inspection
preserve that geography; older profile encodings and saves remain supported without implicit migration.
Profile seven similarly retains a complete finite network through `WorldWatershedNetworkAdmission.CreateGenesis`.
It nests the full construction basis directly; it does not wrap profile six or own the same basin twice.
Both macro profiles use the existing schema-three authority and 16 MiB world snapshot bound.
Macro identities grant neither local terrain extraction rights nor physical water.

The offline host can create an explicit new save from existing canonical inputs:

```sh
OpenFantasy.Host --create-watershed-world new-world.sqlite construction-genesis.bin prepared-basin.bin
OpenFantasy.Host --create-watershed-network-world new-network-world.sqlite construction-genesis.bin prepared-network.bin
OpenFantasy.Host --execute-world-step new-world.sqlite host-selected-step.bin
OpenFantasy.Host --inspect-recorded-world new-world.sqlite 0
```

These are privileged operations under the existing exclusive composed-world authority, not network
client endpoints. A batch is host-selected input; a stale/reused batch fails. The existing
`--create-world` command still addresses the older standalone counted-authority format.
`tools/check_watershed_world.py` exercises the composed host, real before/after-commit process kills,
exact history and unchanged input files. Its fixture is emitted by the hosting recovery test when
`OPENFANTASY_WATERSHED_EVIDENCE` names an explicit disposable directory; the integrated verifier wires
this automatically. The sibling `tools/check_watershed_network_world.py` consumes the network recovery
fixture from `OPENFANTASY_WATERSHED_NETWORK_WORLD_EVIDENCE` and runs the same real crash/history checks
for profile seven. Both are wired into the verifier. Python independently checks each outer archive
and admission-pin composition. Composed and recorded-state operator reports include the admitted
network hash, province and counts when present.

The normal playable farm still selects profile five and its existing terrain. Profiles six/seven
preserve macro constraints only: mapping into local terrain, river water, mutable geological events
and migration of played saves require their own contracts and evidence. The finite admission bounds
are preparation envelopes, not the final world's extent.

## Local durable authority

The portable `Geography` module prepares an explicit finite macro watershed before dependent
local detail. `WatershedPlanner.Prepare` accepts a pinned scope/seed, a grid of up to 64×64 columns,
relief rules and a boundary outlet. It returns immutable parcel/formation identities, natural and
spill elevations, acyclic drainage ranks, contributing-column counts and shared internal faces.
`ReadWindow` reads that same preparation without regenerating it. Cancellation publishes no partial
result. `WatershedCodecV1` archives and verifies the complete preparation against its pinned algorithm.

```sh
dotnet run --project src/OpenFantasy.Host -- --inspect-watershed /absolute/prepared-basin.bin
```

This read-only command accepts the bounded canonical archive, including the independent watershed
fixtures. A candidate is not an admitted world: basin selection/composition, downstream exchange
budgets, local terrain realization, physical liquids and geological history remain required.
Spill elevations are potential drainage constraints, not raised ground or minted water. Macro
grid addresses are not local voxel coordinates. Existing farm saves and terrain keep their prior
generation contract; loading a window must never overwrite lived edits with a fresh candidate.

The composed farm world now supports privileged reconstruction at an **exact recorded commit**:

```sh
dotnet run --project src/OpenFantasy.Host -- --inspect-recorded-world /absolute/farm.sqlite 125
```

Commit zero returns the original genesis; each later revision returns its actual admitted profile,
calendar, state hash, actor positions and stock summary. Different commits at the same calendar
instant remain distinct, including dialogue holds and profile upgrades. The returned source-head
revision/hash pins the preparation; later commits do not change an already returned state.
`SqliteWorldAuthority.ReadRecordedStateForHost` supplies the complete immutable snapshot to trusted
host code. This is privileged operator access, not a player visibility feed or a travel command.

Close the game using that database first: inspection respects exclusive writer ownership. Queries
verify the complete bounded journal, checkpoints, receipts and identity registry through the head;
they neither advance simulation nor rewrite the save. This synchronous correctness baseline is
linear in retained commits and must stay outside frame/simulation callbacks. Accelerated checkpoint
queries, arbitrary interior milliseconds and partial exposure, generated prehistory, branch ancestry
and playable travel remain required work. This interface does not round an unsupported time to a
nearby commit or satisfy the full time-travel gate. The native farm check also exercises this operator
against its own disposable save and verifies unchanged database bytes.

These commands require local operator access to a database and canonical accepted input files. Principal/session arguments describe that operator's trusted local delivery context; they do not authenticate remote clients.

```sh
dotnet run --project src/OpenFantasy.Host -- --create-world /absolute/world.sqlite /absolute/genesis.bin
dotnet run --project src/OpenFantasy.Host -- --execute-local-command /absolute/world.sqlite /absolute/command.bin PRINCIPAL_ID SESSION_ID
dotnet run --project src/OpenFantasy.Host -- --inspect-world /absolute/world.sqlite
```

Creation refuses every existing file. Open rejects unknown/modified schema, corrupt snapshots, discontinuous or unreproducible command journals and incomplete historical identity reservations. Each fresh valid delivery atomically records its command, receipt, resulting state and reserved IDs before returning a receipt. Rejected gameplay decisions consume a commit sequence while leaving custody revision unchanged. Exact retries return the original result; changed content under the same key refuses. IO or uncertain commit failures require closing/reopening and retrying the exact command.

The pinned SQLite adapter uses Microsoft.Data.Sqlite.Core10.0.12, SQLitePCLRaw.bundle_e_sqlite3 3.0.5 and native SQLite3.53.4. Verify actual `sqlite_version()`/`sqlite_source_id()` on every supported exported target. The above CLI commands retain counted schema version1. The exploration/gathering farm retains composed schema version2. Production, quest and construction profiles use composed schema version3 with explicitly tagged migration rows; earlier genesis, journal rows, receipt keys and identities remain byte-identical in the copied continuation. It journals compact step/outcome records, maintains one current head and stores checkpoints every 6,000 advancing steps or 10,000 commits. Full replay validates checkpoints, receipts and identities before readiness. Development recovery limits are 250,000 commits, 65,536 action outcomes and 128 MiB accepted journal bytes; reaching a limit stops the session visibly and retains the save. The bounded profile2-to-profile3, profile3-to-profile4 and profile4-to-profile5 copy-and-publish migrations are implemented. The quest admission adds definitions, empty project storage, explicit initial progression and finite funding; it never resets existing gathering/crafting stock or progress. The current farm has two residents and one funded claimant; a four-player quest economy still needs its own content and concurrency qualification. Long-lived checkpoint-tail recovery, broader schema evolution, scheduled work, outbox, control rebinding and network sessions remain production obligations. The current production ledger admits 32 recipes, 256 retained operations and 4,096 combined live lots; these are development bounds, not a long-lived economy capacity promise. Storage remains count-limited; calibrated mass allocation does not establish a physical volume-capacity system. Profiles3/4/5 use a global production revision token, so independent simultaneous crafts can conflict; fair multi-actor crafting needs explicit later-profile qualification before the 1–4-player claim. The local session performs synchronous database work on its serialized worker, never a Godot frame callback.

### Standalone terrain production inspection

`--inspect-material-production <material-production.bin>` reads the explicit registry v2 archive,
including retained Production consumption and live transformed goods. The report separates original
source quantities into coarse, detailed, other live raw and retired raw units. Refinement and demotion
preserve consumption; recrafting an output does not restore or count its raw inputs again.
`--inspect-material-registry` and `--inspect-terrain-materials` retain their raw-only v1 formats.
See the [composition contract](docs/architecture/world-and-geography.md#domain-contract-terrain-and-production-composition).
This read-only standalone operator does not install terrain in a played world or enable mining.

### Standalone runoff budget inspection

`--inspect-runoff-budget <runoff-budget.bin>` reads an exact funded effective-runoff interval over a
prepared watershed network. It reports downstream obligations and shared receiver headroom from
actual stock and physical calibration. Dry basins can carry upstream contributions. The candidate
moves no water and reserves no receiver capacity. See the
[runoff contract](docs/architecture/world-and-geography.md#domain-contract-funded-runoff-budgets) for
source ownership, canonical bytes and the boundary to future hydraulic stages.

The privileged `--inspect-runoff-capacity <runoff-capacity.bin>` host operator inspects a normalized
standalone runoff reservation owner: pending/closed commitments, logical and custody revisions, and
shared physical/counted receiver headroom. It does not apply flow or load the archive into a played world.
See the [capacity contract](docs/architecture/world-and-geography.md#domain-contract-shared-runoff-capacity-ownership).
