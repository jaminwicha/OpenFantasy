# OpenFantasy runtime

For phase status and the next integrated player journey, use the
[current roadmap](../implementation/CURRENT_ROADMAP.md). The ordinary local launcher opens the
construction farm; the richer hosted cargo/control routes below are explicit selections. Supported
fixtures and bounded native proofs do not imply that every feature is present in the default save.

See the [architecture reference](docs/architecture/README.md) for state ownership, runtime bindings,
data flow and enduring design contracts. [Documentation navigation](docs/README.md) separates that
reference from build instructions, preserved specifications and implementation evidence.

`OpenFantasy.Host --inspect-construction-descent <world.sqlite> <commit-revision> <site-id>` reads an
exact recorded world and assesses the proposed removal of one incomplete excavation site. Its
[event-descent contract](docs/architecture/event-descent.md) retains stopped material as obstacles for
later falls, reports real impact demand and validates complete original loads. This read-only operator
does not publish movement, earn mining rights or alter the existing live coupled-settlement profile.

Fresh structural worlds can explicitly select event-ordered settlement through
`WorldEventSettlementAdmission.CreateGenesis`. Profile18 saves retain that selection and resume pending
excavation through the ordinary Host/session authority; existing saves keep their original interpretation.
See the [live event contract](docs/architecture/event-descent.md#explicit-live-interpretation) for command,
material, time and journal bounds. This selection does not change the preserved farm's content or presentation.

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
do not change the preserved farm, allocate materials or create rivers. `tools/check_watershed_terrain.py`
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

This is the retained development runtime: a portable C# core, shared durable hosting library, headless executable, Godot C# client and executable tests. The native farm now connects player/NPC movement, Wren's original opening dialogue, a finite six-wood handover and save/reopen through one authoritative world. Terrain, bodies, language, custody, dialogue and time persist together. The gathering profile additionally admits real source tools, finite oak/berry reserves, earned effort, depletion and advancing-step regrowth. The production profile turns actual ingredients into rations and recoverable offcuts, retains consumed raw/derived rights, and upgrades an existing gathered save on a verified copy. The quest profile adds source-derived class/stats/XP, finite gil wallets and Wren’s funded six-wood agreement. Acceptance, reserved delivery, 420 XP and 200 gil commit once and survive a preserved upgrade/reopen. The construction profile connects the delivered wood to Wren’s routed movement and 200 authoritative work steps, creating one small physical seedling guard and three offcuts. Work and material geometry persist together; reading holds and pause stop work. Automated native journeys qualify both fresh games and progressed-save continuations. The preserved construction profile does not expose the later restorative-use, explicit human-building and hosted multiplayer routes described below. Those routes have bounded evidence; general building and the complete multiplayer experience remain open. The [increment record](../implementation/p01/native-loop/WORLD_CONTRACT.md) and [preparation packet](../implementation/p00/README.md) distinguish measured behavior from full phase exits.

## Dedicated player listener

`OpenFantasy.Host --create-cargo-roster <database> <seed> <name> [name2] [name3] [name4]` creates a fresh
one-to-four-human cargo world, preserving the existing NPC cast and finite supplies. It refuses an
existing save. The [roster reference](docs/architecture/player-rosters.md) describes character ownership,
starter tools and the cooperative encounter catalog.

`OpenFantasy.Host --serve-cargo-world <settings.json>` serves an existing admitted cargo world over
TLS/WebSocket with scoped player invitations. The [operator guide](docs/player-host.md) covers
certificate trust, private provisioning, runtime requirements and joined shutdown; the
[architecture reference](docs/architecture/player-http-host.md) defines ownership and protocol limits.
The reusable [player protocol client](docs/architecture/player-client.md) owns TLS connection,
validated first view, bounded input, exact receipt/query correlation and joined shutdown. The native
session obtains source-bound presentation and participant status from that host, then uses the ordinary
Godot controls and immutable action offers. Join with the .NET Godot executable:

```sh
godot --path client res://scenes/NativePlayer.tscn -- --join /absolute/path/player.invitation.json
```

Keep the invitation private and trust the listener's certificate through the platform trust store.
Closing this client closes its connection; the listener owns the saved world. Pause stops this player's
input, while other ready players may continue. Native 1–4-player, reconnect and production acceptance
remain distinct gates; a single native connection does not establish them.

An eligible saved adventure can explicitly enable collective withdrawal on a verified separate copy.
Use `--prepare-party-withdrawal <source> <new-request>` followed by
`--enable-party-withdrawal <source> <new-copy> <request>`; see the
[offline operator instructions](docs/player-host.md#enable-collective-withdrawal-on-a-copy).
The request pins the original saved head. Fresh admission remains unchanged, and ordinary opening
never upgrades a save. A migrated solo copy retains its exact original seed/name selection.

## Build and verify

Use Python 3, the exact .NET SDK in `global.json`, and the .NET edition of Godot pinned in `toolchain.json`. The SDK executable must have access to the required managed runtimes. A standard non-.NET Godot editor cannot run this project.

From this directory:

```sh
python3 tools/verify.py --dotnet /absolute/path/to/dotnet --godot /absolute/path/to/Godot
```

The verifier checks the actual tool versions, independently regenerates all retained independent numeric, command, package, custody, scheduling, history, exposure, anatomy, production, observation, language, authority, terrain, progression, quest and construction fixtures, restores locked dependencies, builds Debug, requires every discovered core and hosting test to execute and pass, checks the server suite with exactly the named foreign-platform case reported as unrun, exercises host contract inspection against valid/malformed inputs and retained source-packet hashes, imports the client, and runs a headless Godot smoke check. It compares host/client primitive results and records the managed runtime observed inside each process. It also kills the dedicated host before/after an actual SQLite commit, reopens and retries without duplication, refuses a competing process, and checks matching durable results and actual native SQLite settings inside Godot. The native farm probe also routes movement, talk, give and close through the real client and compares the paused public view after process restart. A zero process exit does not excuse missing tests or Godot error output. Results and raw process output appear in `artifacts/verification.json`, with a fresh TRX report for each run. Optional `--forbidden-token TEXT` checks text input contents and all input paths for a prohibited name; binary assets are hashed and reported separately; the report states whether that scan was requested. This remains local binding evidence, not a performance, rendering, networking or desktop-export qualification.

The portable test suite can run independently:

```sh
dotnet restore tests/OpenFantasy.Core.Tests/OpenFantasy.Core.Tests.csproj --locked-mode
dotnet test tests/OpenFantasy.Core.Tests/OpenFantasy.Core.Tests.csproj --no-restore
```

The retained-world proposal migration tests require the original qualification ledger. From this
directory, supply its directory explicitly when running Hosting tests:

```sh
OPENFANTASY_RETAINED_PROPOSAL_SOURCE="$PWD/../implementation/p02/care-definition-digests/qualification/durable-hosting-final" \
  dotnet test tests/OpenFantasy.Hosting.Tests/OpenFantasy.Hosting.Tests.csproj --no-restore
```

Those tests verify the pinned source bytes, copy them before opening SQLite and fail if the input
is absent; they never replace it with a fresh world or skip the retained-history assertions. The
[bounded verifier](../implementation/p02/retained-proposal-transition/qualification/run_checks.py)
supplies that same source and records preservation, source/build pins and nonempty test results.

On the qualified local macOS development host, double-click `Play OpenFantasy.command`, or run `python3 tools/play.py`. The launcher selects the pinned tools, performs a locked client build/import and opens the saved adventure; `--prepare-only` checks preparation without opening a gameplay save. On another machine, supply `--dotnet` and `--godot` (or `OPENFANTASY_DOTNET` / `OPENFANTASY_GODOT`) for the exact installed tools. It never resets an existing save. This development launch route is not a platform export.

The ordinary adventure keeps Wren as an autonomous NPC and connects the farm, finite crafting, two markets, road danger, retreat, care and shared-control policies. Its separate save is `user://worlds/openfantasy-adventure.sqlite`; older worlds are preserved. Use `python3 tools/play.py --farm` to continue the original construction farm. For a hosted world, create an explicit roster with `OpenFantasy.Host --create-adventure <new-world.sqlite> <seed> <name> [name2] [name3] [name4]`, serve it through the existing cargo-host settings, then use `python3 tools/play.py --join <invitation-file>`. No lived save is silently migrated. See [adventure entry](docs/architecture/adventure-entry.md) for ownership and selection contracts.

Alternatively, build Debug and open `client/project.godot` with the pinned .NET editor. Put the selected SDK directory on PATH and set DOTNET_ROOT to its runtime root before launching the editor; both matter for editor SDK discovery and native runtime selection. The current source-based visuals are a provisional original-asset baseline, not the approved production art target. The client derives semantic sound cues and music/ambience context from committed views on separate Music, Ambience, Effects and Voice buses, but ships no accepted audio assets, so play is currently silent. O, a controller's share button or the HUD's Settings control opens settings for the five mixer levels and captions for notable sounds; they are saved in `user://settings.cfg`, separate from every world save, and opening them never pauses the world. Move with WASD/arrows or the left stick; E/controller A talks or closes your conversation, G/controller X accepts or completes Wren’s addressed quest (outside a conversation it asks you to speak first; older profiles retain ordinary gifts), F/controller Y gathers from a nearby plant with its required owned tool, C/controller B packs two berries and one wood into two rations plus one wood offcut, and Esc/Start pauses. Closing saves through the host; reopening retains the world. For the preserved construction farm selected with `--farm`, the local save is `user://worlds/wrens-farm-construction.sqlite`. When absent, startup first continues an existing `user://worlds/wrens-farm-quests.sqlite` on a verified copy, admitting one finite owned joinery kit and an unstarted project without changing old work, stock or rewards. If no quest continuation exists, the same preserved chain works backward: When absent, startup upgrades a verified copy of an existing `user://worlds/wrens-farm-production.sqlite`; an existing gathered world at `user://worlds/wrens-farm-work.sqlite` first receives its preserved production continuation. Each earlier file and its complete history remain intact. Existing selected construction saves take precedence. A failed or unsupported upgrade, or orphaned source sidecar, stops opening; it never silently resets the world. A new game admits the construction profile directly. Delivering six eligible raw wood gives 420 XP and 200 gil once, reaching level 3 with 38 residual XP and HP78/MP14 from the admitted level-one Human Squire. The supplies remain reserved until Wren reaches the crop plot and completes the joinery. Close the conversation to let him work; keep the plot and his route clear. This is a small sapling guard, with no claimed orchard-wide wind protection. Its provisional atlas panel has physical collision, but a dedicated work animation and sound kit are still pending. The earlier exploration world remains at `user://worlds/wrens-farm.sqlite` and can still be opened with `--legacy-farm`.

`tools/check_native_farm.py` exercises those actual Godot input/view adapters and reopens the same durable world in a second process. Its default is headless; `--rendered` also retains native screenshots for a separate visual review. This proves the small walk/talk/give connection, not Wren's complete quest or the full first-prototype loop. `tools/check_native_gathering.py` exercises real gather input, finite yields, a paused resource, exact process recovery and subsequent regrowth; its rendered option retains source-asset screenshots. `tools/check_native_production.py` runs three actual native processes: gather berries under the retained rules, upgrade the saved world and craft, then reopen and gather/craft again. It checks that the old save bytes remain unchanged, outputs survive restart, and paused projection fields remain exact. `tools/check_native_quests.py` prepares a progressed production save, upgrades a copy, accepts and completes the actual quest, then reopens and verifies a repeated completion cannot duplicate its reward. Its `--rendered` option captures the native HUD and dialogue. `tools/check_native_construction.py` checks both a progressed quest-save copy and a fresh construction-profile game. The fresh family performs the same real gathering, ration crafting and addressed acceptance/delivery before the routed build, conversation interruption, partial/completed process recovery and body collision/walk-around checks. The integrated verifier includes these probes alongside the lifecycle checks; retained qualification reports establish which exact revision and journey passed.



Open the pack with **I**, controller **View/Select**, or the **Pack** button. Inspect actual owned tools, materials and crafted supplies with hover, arrow keys/D-pad, Tab, or item selection. Inspection never equips, consumes, crafts or transfers anything. Counts aggregate display groups while keeping source/crafted and protected stock distinct; the underlying lots are unchanged. Item names and details cover the admitted farm catalog, with an explicit unfamiliar-item fallback. Native item icons, equipment/use actions and richer management remain future work.

Open **History** with **H**, controller **left-stick click**, the HUD history icon, or the wheel's
History button. It discovers completed exchanges and gifts for the current character, including
archived records. Choose a row and **Inspect** to read its terms; **Next** reads older records from
the same snapshot, and **Latest** starts again with the newest records. Record references distinguish
similar entries, with full references in tooltips. Escape/controller B closes the screen. Browsing
does not repeat an action, move goods or pause other participants. Operated characters require the
separate PrivateHistory permission; Inventory and Trade are not required. Changing characters,
losing focus or returning control clears the view. An older session that lacks this API reports that
History is unavailable.

Open **Recipes** from the HUD to browse the character’s admitted crafting recipes. Inspect ingredients
and outputs, select a recipe, choose **Prepare**, then **Confirm craft** to spend the captured materials.
Browsing and preparation reserve nothing. Changed materials or character selection require fresh
preparation. The panel preserves an existing pause; crafting requires running local time. The quick
Craft wheel sector and **C** keep their ration shortcut. Production permission can expose Recipes
without exposing Pack or medicine-use controls. The current catalog is admitted content, not a
completed recipe-learning system.

For the explicit human-builder qualification scenario, create a separate new hosted world with
`OpenFantasy.Host --create-human-construction <new-world.sqlite> <seed> Amber Birch Cedar`.
The three travelers join Wren as a fourth human. Serve that database through the ordinary cargo-host
settings and invitations. Another traveler supplies Wren's original quest; select the assigned project
and inspect **Build** in the field controls or action wheel. Each confirmation performs one work step.
Funding, the owned matching tool and exact work stance remain required. This scenario preserves the
original 200-step guard requirement; it is not a polished sustained-work interface or production balance.
The command refuses an existing database and does not change the default cargo population.

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
- `src/OpenFantasy.Host`: the retained headless entry point. `--inspect-foundation` exercises the same core. `--inspect-command <command.bin>` validates a bounded canonical envelope, `--inspect-package <manifest.json> <payload-file>` validates metadata and payload bytes, and `--inspect-custody <snapshot.bin>` checks counted snapshot invariants. Those inspection diagnostics issue no gameplay receipts. Local operations use durable authority; cargo hosting supplies authenticated player admission, scoped invitations, gameplay commands and observations through the Server adapter. See [player hosting](docs/player-host.md) for the actual serving/joining route and its limits.
- `src/OpenFantasy.Server`: the engine-free TLS/WebSocket serving adapter over Hosting, with scoped admission, bounded transport and joined connection lifetimes.
- `client`: the independent Godot project and C# presentation adapter. Godot owns rendering, not authoritative outcomes.
- `tests/OpenFantasy.Core.Tests`: independently generated reference vectors and behavior/edge tests.
- `tests/OpenFantasy.Hosting.Tests`: actual temporary SQLite worlds, player/NPC transfers, concurrency, restart, corruption and independent critic regressions.
- `tests/OpenFantasy.Server.Tests`: authenticated transport, client-model lifetimes, real host processes and opt-in native client journeys; qualification must supply required fixtures/tools and observe terminal outcomes.
- `content`: the retained source dependency packet and versioned farm admission. The full source closure is embedded byte-for-byte for runtime use without a source checkout. Wren, the farm locality, finite wood and opening utterance now have admitted behavior; the remainder of the preserved packet still requires coupled conversion.
- `tools`: reproducible local verification. Build artifacts stay untracked.

Only the client references the engine. The solution's Debug configuration builds all development artifacts. Release maps the client to Godot's **ExportRelease** and the ordinary .NET projects to Release. Building either configuration does not create a distributable Godot export; actual templates, export presets and supported-device execution remain required. In particular, a macOS build does not establish Windows/Linux support.

## Candidate toolchain and portability

The current SDK is 10.0.401. Core, Hosting, Server, Core/Hosting tests and the client target net8.0; the headless entry point and Server tests target net10.0. These are distinct from the managed runtime Godot actually loads. The local candidate installation contains runtimes 8.0.31 and 10.0.12, and the smoke report records which was selected. Godot.NET.Sdk is pinned to 4.6.0 with locked NuGet dependencies. Desktop framework/export selection must be qualified in P01; the baseline framework choice is not a completed cross-platform export or production support promise.

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

`--inspect-liquid-reservoirs <reservoirs.bin>` reports the standalone physical reservoir wrapper,
including geometric capacities and separate actual/projected fill levels. Its authored watertight
vessels do not establish live terrain walls or flow. See the
[reservoir contract](docs/architecture/world-and-geography.md#domain-contract-finite-physical-reservoirs).

`--create-reservoir-world <new-world.sqlite> <construction-genesis.bin> <reservoirs.bin>` admits an
explicit unplayed construction basis with exactly matching inventory/Production and complete physical
reservoir ownership. All material actions then pass the physical/capacity gate before durable publication;
time alone does not move liquid or release promises. The preserved farm is unchanged. See the
[world binding](docs/architecture/world-and-geography.md#runtime-binding-exclusive-reservoir-ownership).
`OPENFANTASY_RESERVOIR_WORLD_EVIDENCE` retains the hosting fixture; the full verifier runs
`tools/check_watershed_network_world.py --profile reservoir` for independent outer-byte verification,
actual commit crashes, restart and exact history through the host.

Profile-eight host-selected batches also support funded runoff admission/cancellation through
`WorldActionCodecV6`. Requests pin the entire physical owner and its revision; cancellation releases
receiving headroom but preserves source holds. Older profiles reject these commands. This provides
no player-facing menu or live liquid movement. See the
[control contract](docs/architecture/world-and-geography.md#runtime-binding-funded-runoff-commitment-controls).
`OPENFANTASY_RUNOFF_COMMAND_EVIDENCE` retains the command/replay fixture used by the full verifier's
actual host crash/recovery checks.


Profile-nine reservoir transport adds explicit adjacent gravity gates and scheduled arrivals to a fresh
construction world. `--create-reservoir-transport-world <world.sqlite> <basis.bin> <transport.bin>` uses
the ordinary exclusive host; `--execute-world-step` schedules or advances it, and `--inspect-recorded-world`
reads exact history. The [transport architecture](docs/architecture/reservoir-transport.md) defines
indivisible funded batches, per-step throughput, conservative head checks and retained blocked outcomes.
This is contained reservoir transport with authored boundaries; it does not change preserved farm graphics
or provide a player-facing water interface. `OPENFANTASY_RESERVOIR_TRANSPORT_EVIDENCE` retains the
fixture used by `tools/check_watershed_network_world.py --profile transport` for actual transfer crashes,
paused arrivals, source conservation and history. Full verification invokes this same harness.


The host can assess reservoir floors/walls against an exact recorded world's terrain and completed
construction using `--inspect-reservoir-boundaries <world.sqlite> <commit-revision> <seal-policy.bin>`.
The canonical policy explicitly names sealing materials and minimum thickness; hardness and rounded
colliders do not count as seals. Negative coverage returns a successful inspection with
`IsContained = false`. This read-only report does not change admission, simulate breach/strength or
qualify the authored transport gates. See [boundary contracts](docs/architecture/reservoir-boundaries.md).
The verification fixture is retained with `OPENFANTASY_RESERVOIR_BOUNDARY_EVIDENCE`; the verifier invokes
`tools/check_reservoir_boundaries.py` against actual terrain and NPC-built tank saves.

Profile twenty adds exact source-backed liquid cells to a fresh terrain-contained profile nineteen
world. `--create-liquid-cell-world <world.sqlite> <profile19-genesis.bin> <liquid-cells.bin>` retains
the initial placement; ordinary world steps move its existing intervals downward and laterally under
starting-state quantity/capacity budgets. [Detailed liquid flow](docs/architecture/liquid-cell-flow.md)
defines holds, material reconciliation, cell/fragment bounds and persistence. Host inspection reports
actual cell placement instead of the equilibrium surface. This does not change the default rendered
scene or supply occupied wet-space, gates, pressure or reactions. Fragment overflow aborts the step and
faults a running local session; recoverable fluid continuation remains unqualified.
`OPENFANTASY_LIQUID_CELL_FIXTURE_OUTPUT` retains the twelve-step fixture for the standalone
`tools/check_liquid_cell_flow.py` Host, independent oracle and external commit-kill qualifier.

### Coordinated encounter pacing

`--create-paced-world <world.sqlite> <profile21-genesis.bin> <encounter-id|none>` creates a fresh
profile-twenty-two world through the real SQLite authority. It retains every terrain/liquid/farm owner
and adds a permanent exposure clock. An optional complete-resident encounter accepts authenticated
Wait choices; selected tactical ticks earn 50 ms, with gathering recovery and actual fluid flow only
at complete 1,800 ms process boundaries. No elapsed time comes from a decision hold or restart.

Read [world pacing](docs/architecture/world-pacing.md) for canonical bytes, schema five, queue receipts,
process carry and session selection. This operator creates the durable timing profile. Playable staging, release and native controls
are composed by the separate encounter journey described below. It neither
changes the normal farm launch nor migrates existing saves.
`OPENFANTASY_ENCOUNTER_PACING_EVIDENCE` exports the managed fixture consumed by
`tools/check_live_encounter_pacing.py` for independent full-state, SQLite and external-kill checks.

### Source-backed physical encounters

`--create-encounter-world <world.sqlite> <seed> <player-name>` admits a separate fresh profile-twenty-three
Squire-versus-Bramble-Wolf encounter, with Wren as a coupled quest-bearing bystander. Actual source
Strike/bite grants, anatomy, range, HP/MP, deterministic damage draws and positive readiness debt share
the complete farm/terrain/liquid authority. Queue receipts precede resolution; zero HP incapacitates
without deleting identity or possessions. Outcome holds the encounter with timing/process obligations
retained. The separate encounter journey binds native attack controls and entry/staging/release;
full death and rewards require broader game contracts.

Read [encounter actions](docs/architecture/encounter-actions.md) for exact rules, source transforms,
bounded NPC selection and canonical storage. This operator preserves ordinary farm launches and old saves.
`OPENFANTASY_ENCOUNTER_ACTION_EVIDENCE` exports the managed durable duel consumed by
`tools/check_encounter_actions.py` for independent full-root/RNG/SQLite and external commit-kill checks.

### Exploration and encounter lifecycle

`--create-encounter-journey <world.sqlite> <seed> <player-name>` creates a separate fresh
profile-twenty-four exploration world with the real Squire, Wren and two Bramble Wolves. Explicit
scenario entry, in-place confrontation/staging, physical combat and outcome release share one
schema-six authority. Entry/release earn no time; recovery debt and fractional process carry survive
release, restart and another encounter. Ordinary activity respects incapacity and recovery.

Read [encounter lifecycle](docs/architecture/encounter-lifecycle.md) for authority-derived episode
identities, complete dry-world safety, public entry/release observations and retained archive rules.
This operator does not change the normal native launch or migrate an existing save. The explicit
`--encounters` and `--care` launches bind native controls below. Production choreography, broader
effects, general hazard closure and campaign gates remain separate.
`OPENFANTASY_ENCOUNTER_LIFECYCLE_EVIDENCE=<directory>` exports the managed lifecycle fixture for
`tools/check_encounter_lifecycle.py` to reconstruct roots, journals and real entry/release crash recovery.

### Standalone timing and structural inspection

`--inspect-encounter-timing <timing.bin> <world-id> <branch-id> <encounter-id>` validates a bounded
canonical timing archive and reports independent next-tick/next-confirmation proposals, their exact
bytes and hashes. It writes no state and authenticates no world history, controller or action pin.
Read [the timing architecture](docs/architecture/encounter-timing.md) for scope, exposure carry and
the separate live-world admission requirements.

`--inspect-structural-resolution <checkpoint.bin> <trusted-graph.bin> <dependency-pin>` validates a
bounded canonical structural checkpoint against separately supplied expected dependencies, resumes its
immutable failure plan, and reports exact load accounting and result hashes. It reads files only; supplied
ratings are not authenticated world policy and no collapse is published. See the
[structural contract](docs/architecture/structural-loads.md) for mass ownership, rated contact, continuation
cost and the separate obligation to recheck physical footing. `OPENFANTASY_STRUCTURAL_LOAD_EVIDENCE`
retains the actual funded/occupied construction fixture used by `tools/check_structural_loads.py`.


### Physical construction ownership

Fresh dry profile thirteen retains physical member condition and placement separately from completed
construction work. `WorldConstructionPhysicalAdmission.CreateGenesis` explicitly selects it from an
unplayed profile-twelve genesis. Ordinary NPC construction creates member identities and material-backed
state in the same durable completion commit. Existing saves keep their interpretation. Historical Host
reports include `ConstructionPhysical` hashes and member/source information; they do not certify load
safety or live collapse. See [physical construction](docs/architecture/construction-physical.md).
`OPENFANTASY_CONSTRUCTION_PHYSICAL_EVIDENCE` retains the actual completion and upstairs fixture used by
`tools/check_construction_physical.py` for commit-kill/retry/history qualification.


Fresh dry profile sixteen adds [durable consequence continuation](docs/architecture/consequence-continuation.md)
to the intact-settlement interpretation. Eligible isolated excavation retains its command and reserved time
interval while actual geometric queries progress. Schema four journals explicit continuation rows; the
original receipt and physical/material changes publish once at completion. The session resumes saved work
before readiness, while read-only opening/history never progress it. `tools/check_consequence_continuation.py`
qualifies real trigger/intermediate/final process-kill boundaries using `OPENFANTASY_CONSEQUENCE_EVIDENCE`
fixtures from managed tests. The preserved playable farm remains on its existing profile.

## Separate worksite entry

`python3 tools/play.py --worksite` opens Wren's authored worksite in its own
`user://worlds/wrens-worksite.sqlite` save. The preserved farm and its earlier saves remain separate.
Speak with Wren and fund both tasks, gathering the additional wood with the normal tool action.
Keep the work routes clear while Wren builds. Walk onto the stairs using ordinary movement;
Q/right shoulder selects the worksite face, and V/right-stick click opens contextual Mine/Climb
actions. Direction selects a sector; Enter/controller A confirms. Costs and blocked reasons remain
inspectable.

The visible geometry is a functional demonstration with explicit material/load calibration, not the
accepted production art or final timber balance. See [local worksite architecture](docs/architecture/local-worksite.md)
and the [qualification record](../implementation/p02/playable-worksite/README.md) for actual evidence and limits.

## Separate watercourse entry

`python3 tools/play.py --watercourse` opens the finite stepped channel beside Wren's farm in
`user://worlds/wrens-watercourse.sqlite`. It keeps the normal farm and worksite saves separate.
Walk along the dry banks east of Wren; ordinary gathering, conversation, quest delivery and construction
remain available. Six thousand litres drain from the upper reach into the pool and settle. Reading and
pause hold the saved flow; reopening continues the same placement. There is no refill, swimming or water-use
action. The open-channel cutaway and native surface kit are provisional calibration, not production art
acceptance. See [the architecture](docs/architecture/local-watercourse.md) and
[the integration packet](../implementation/p02/native-watercourse/README.md).

## Separate terrain-breach entry

`python3 tools/play.py --breach` opens the authored divider and lower receiver in
`user://worlds/wrens-breach.sqlite`. Walk east along the north bank, use Q to select the divider,
then V, Right and Enter to inspect and confirm mining. Three deliberate work actions remove it;
partial labor survives closing the game. Water then crosses the opening from the same finite stock.
The contextual wheel exposes no unsupported climbing. Other farm/worksite/watercourse saves keep
separate selections and are never implicitly migrated.

This is a dry-bank excavation/flow connection, with provisional cutaway rendering and calibrated
starter-tool/material balance. Swimming, fluid loads, gates, production art and broader ecology retain
separate requirements. See [architecture](docs/architecture/terrain-liquid-breach.md) and
[qualification](../implementation/p02/native-terrain-breach/README.md).


## In-place encounters

Run `python3 tools/play.py --encounters` to open the separate source-backed encounter world at
`user://worlds/wrens-encounters.sqlite`. Existing farm saves remain selected by their original launch
routes. Q/right shoulder selects a wolf; E/controller A engages it. V/right-stick click opens the
icon wheel for inspection and explicit confirmation. During combat E/A queues Strike and G/X queues
Wait. Required decisions hold world time; pause remains Esc/Start. I/View opens the pack without
changing that time rule. At a victorious outcome E/A or the Continue button returns to exploration,
preserving wounds and recovery. An incapacitated initiating player currently cannot leave the outcome.

The source wolf idle/walk sprite and modest hit reactions are provisional. Damage feedback follows
actual HP changes; acceptance of an attack means queued, not already resolved. The
[native encounter architecture](docs/architecture/native-encounters.md) describes the public boundary.
`tools/check_native_encounter.py` runs isolated injected native journeys and exact process reopening;
`tools/check_native_encounter_defeat.py` exercises deliberate costed Wait, actual incapacity, inspection
and exact paused reopening. Both support `--rendered` with disposable unfocusable startup settings.
Those checks do not replace human art,
physical controller, accessibility or production-platform acceptance.

Run `python3 tools/play.py --care` for the explicit care-enabled encounter journey at
`user://worlds/wrens-care.sqlite`. Wren starts one cell north with two real Potions and six berries;
the original three-berry Potion recipe and 40 HP effect are source-pinned. Original combat statistics
remain unchanged. This is a fresh profile selection, not a migration of existing encounter saves.
After actual defeat, E/A or the care icon acknowledges the prior agreement. V/right-stick click
opens the wheel for inspection and confirmation. Acknowledgment restores no HP and spends no item;
Wren must separately administer a real owned dose when the world is ready. The same encounter then
resumes, retaining combat state and charging the helper's recovery. Pending or blocked care stays
inspectable. Other actions cannot bypass incapacity. Pause and focus loss retain their existing rules.
Observed HP gains show a brief mint tint and signed amount without additional sprite motion. Opening
a saved world establishes the current HP baseline without replaying historical damage or restoration.

`tools/check_native_care.py` drives disposable care journeys through the actual Godot input adapter,
checks retained transaction boundaries and reopens each saved world in another process. Its
`--controls all` selects wheel, keyboard, controller-event and pointer-event journeys; `--rendered`
uses isolated startup no-focus settings and captures images. Injected controls and background renders
do not establish physical-device, foreground-focus, human art or production-platform acceptance.

`--pending-save /absolute/path/pending.sqlite3` instead copies an existing closed backup containing
an actual pending acknowledgment. Three separate native processes prove paused recovery, explicit
unpause followed by helper treatment and a real attack, and exact post-treatment recovery. The source
backup stays unchanged; this path does not create or qualify a native acknowledgment. The managed
`CareRewardContinuationTests` can export a reproducible production-admitted pending backup when
`OPENFANTASY_CARE_CONTINUATION_EVIDENCE` names an empty evidence directory. Omit `--controls all` for
this mode. It supports the same `--rendered`, tool-path and output options as fresh journeys.

Run `python3 tools/play.py --restorative` for the separate ordinary-care journey at
`user://worlds/wrens-restorative.sqlite`. `RestorativeJourneyAdmission.CreateEmbedded` admits its
fresh profile26, partitioning the same two source Potions between the player and Wren;
`WorldRestorativeActions.Prepare` captures an exact self-use or permitted helper dose. Complete world
steps publish consumption, capped restoration and exploration time together. A controlled player can
submit these explicit commands through `LocalWorldSession`; reading holds and encounter boundaries
refuse them. [Ordinary-care architecture](docs/architecture/ordinary-care.md) documents permission,
revocation, persistence and the separate material-history limit. V/right-stick click opens the wheel;
Q/right shoulder changes to the Care page. Up selects personal medicine and Right selects permission
withdrawal; Enter/controller A or Confirm commits the inspected choice. The HUD bottle icon also uses
your own Potion. Full health, unavailable stock and storage-history limits explain why use is blocked.
Withdrawal permanently stops future ordinary aid under that agreement; it leaves the separate aftermath
agreement intact. Wren independently offers ordinary aid only with active permission, actual owned
medicine, physical eligibility and enough missing HP for the complete dose. Inspection does not pause a
running world, and no menu takes control of Wren's stock. The pack identifies your known Potion and its
strength. Combat-item binding remains separate; `--care` retains the earlier aftermath scenario.

`RestorativeNativeFixtureExportTests` earns injury and raw/crafted medicine through actual managed
gameplay, exporting backups when `OPENFANTASY_RESTORATIVE_NATIVE_EVIDENCE` names a directory.
`tools/check_native_restorative.py --dotnet <path> --godot <path> --fixtures <directory> --output <json>`
continues those saves with pointer/keyboard/controller input, independent helper aid, permission
withdrawal and a separate paused reopen process. `--rendered` uses disposable startup no-focus settings.
The produced manifest feeds `RestorativeNativeResultTests` through `OPENFANTASY_RESTORATIVE_NATIVE_RESULTS`
to authenticate exact journal command, dose, resource and time effects. Native controls do not imply
the preparatory injury/crafting happened natively. See the
[implementation packet](../implementation/p02/restorative-interface/README.md) for measured qualification
and remaining lifetime-history, combat, platform and acceptance obligations.


## Gifts

Run `python3 tools/play.py --gifts` for the dedicated proposal-capable world at
`user://worlds/wrens-gifts.sqlite`. It is a fresh profile selection, not an upgrade of earlier saves.
Open the pack with **I / View**, then choose **Gifts**. Select an exact inventory stack, recipient and
quantity, then prepare it. Choose an incoming/outgoing offer to inspect or answer it. Browsing and
preparing send nothing. **Review in Gifts wheel** opens the confirmation surface; **V** also opens
the wheel and **Q / right shoulder** cycles Actions, Care and Gifts. Gift sectors are Up: create,
Right: accept, Down: decline, Left: withdraw. **Enter / A / Confirm** commits the selected action.

Offers reserve no stock and expire as world time advances. Acceptance transfers the selected item;
it does not heal or authorize treatment. Use the Care page separately for your own medicine. Wren
has an explicit medicine-giving/answering preference; giving requires enough own stock to retain one
dose and a nearby player who can benefit from a full dose. Reopening supplies the same policy.
The pack's history control explicitly reads the selected offer's recorded outcome. Full pause allows
inspection only; a running world's ordinary clock continues while menus are open.

`NativeGiftFixtureTests` exports an earned injury, helper crafting and withdrawn ordinary-care
permission when `OPENFANTASY_NATIVE_GIFT_FIXTURES` names a fresh directory.
`tools/check_native_gifts.py --dotnet <path> --godot <path> --fixtures <directory> --output <json>
--rendered` continues separate copies with keyboard/controller acceptance, pointer decline and
creation/withdrawal, then reopens each in another paused process. Its manifest feeds
`NativeGiftResultTests` via `OPENFANTASY_NATIVE_GIFT_RESULTS` to authenticate journal effects.

An existing coordinated profile27 world can be explicitly copied into the proposal interpretation
through `SqliteWorldAuthority.UpgradeCoordinatedToProposals(source, unusedDestination, request)`.
Create the `WorldProposalTransition` request from the actual recovered source head and a fresh cause
identity. The operation holds the source lease, validates its complete history, appends the exact
transition to a verified copy and publishes that copy only after successful replay. It preserves
the original genesis and historical rows. Opening an existing save never performs this transition.
See the [transition contract](docs/architecture/coordinated-world-continuation.md#explicit-proposal-transition).
The native `--gifts` selector expects a fresh proposal genesis; it does not select these transitioned
worlds. Copying/transitioning and opening stay separate operations.

### Retained gift worlds

Run `python3 tools/play.py --retained-gifts SAVE GENESIS` to continue an existing, already
transitioned retained world. `GENESIS` is the exact original fresh profile27 genesis file of that
world. The route has no default save and never creates, migrates, resets or relabels one: opening
recovers the complete history, requires that original genesis, the same world, branch and inherited
content admission, and a profile30 head reached through exactly one authenticated transition. A
missing save, another genesis, a lived or malformed genesis file, an untransitioned profile27 save, a
fresh profile30 world and any other interpretation are refused and the save is released unchanged.
The world opens paused under normal focus rules. The companion named by the player's own care
permission receives the same explicit medicine preference as `--gifts`. Controls are those of the
Gifts section above; ordinary Care self-use spends a received dose.

`RetainedGiftFixtureTests` prepares native input from the pinned original through the accepted
transition. Its managed, labeled preparation uses only ordinary commands: the patient withdraws the
active care permission, the helper's own offers and withdrawals fill the proposal outcome window to
62, and empty steps start the helper's idle amble interval. It then proves the journey in a managed
scheduled session. With `OPENFANTASY_RETAINED_GIFT_FIXTURES` naming a fresh directory it exports
`save.sqlite`, `genesis.ofc`, `transition.ofc`, `before.ofc`, `metadata.json` and `manifest.json`.
`tools/check_retained_gifts.py --dotnet <path> --godot <path> --fixtures <directory> --output <json>
--rendered` copies that fixture, runs `--retained-gift-probe` once in keyboard mode and once as a
separate paused reopen within 30s opening/150s probe/180s process bounds, and never edits shared
project settings. In the journey the helper's policy offers a dose, the player's native pack/Gifts
offer is the step whose automatic cut carries that open gift, the helper declines the player's offer,
the player accepts the gift and then uses it through Care. Its manifest feeds `RetainedGiftResultTests`
via `OPENFANTASY_RETAINED_GIFT_RESULTS` (with `OPENFANTASY_RETAINED_PROPOSAL_SOURCE`), which binds
the saved database to the original row by row and authenticates the carried cut, custody and use.
Fixtures keep their source saves intact. Injected native controls and background images do not
establish physical-device, OS-focus, production art, complete gift UX or process-interruption acceptance.

## Markets

Run `python3 tools/play.py --markets` for the separate fresh market world at
`user://worlds/wrens-markets.sqlite`. It opens through its own entry scene
(`godot --path client res://scenes/NativeTrading.tscn -- --markets [SAVE]`); the preserved farm and every
other selection are unchanged, and an existing save opens only when it is this same world. Wren trades
her six admitted berries in person; your money comes only from her ordinary six-wood quest reward, and
her purchases are paid from her own proceeds. Her gift preference and Care work as in `--gifts`.

**V** opens the wheel and **Q / right shoulder** turns to **Market**: Up buys, Right sells, Down shows
offers and saved outcomes. Confirming a sector only opens the stall panel, also while paused. In the
panel choose Buy or Sell, units and (to sell) one exact own lot; **Show this lot** lets Wren offer to
buy it; **Stop showing** retracts that disclosure without transferring goods or closing an issued
quote. **Ask Wren** sends a nonbinding request, **Review offer** captures her exact terms and
**Confirm** accepts them. Changing a choice, **Back**, focus loss or a controller disconnect discards
the review; nothing is ever re-asked or confirmed for you. **Esc / B / V** close the panel.
Arrows / D-pad / Tab move focus along the panel; Enter / A open a list and choose its row, and Esc or
controller B close an open list without choosing (the stall stays open). The mouse clicks, hovers and
scrolls the same controls.

The panel suppresses walking but not world time: Wren answers shortly while the world runs. **Pause to
read** stops this world, including every offer window; she cannot answer and you cannot confirm until
**Resume time**. The card shows price, dues, total, your purse (and any shortfall) and the remaining
time in seconds or minutes of running time; hovering the status line shows the advancing-step basis.
Offers last 1,200 advancing steps (about a minute of running time), showings 2,400 and requests 600.
Wren keeps one standing offer for you; ignoring it lets it close on its own. **Check saved record**
reads the chosen outcome from the save; outcomes that exist only in archived history are not listed.
In this local single-player session the pause is not a shared multiplayer Wait.

For an authorized operated character, the same Market wheel and stall panel use that character's
Trade permission, purse and disclosed usable trade lots. The title names the shopper. Inventory
permission is independent; no home-character funds are substituted. Characters also links to this
shared stall screen. Changing character, returning control or changing the stall clears the review;
an old response cannot populate a later visit. Other ready players may keep the shared world running
while you read. Confirm always uses the exact reviewed quote, quantity and character revision.

Operated Trade shows open quotes. Saved terminal outcomes require separate PrivateHistory permission;
the screen remembers only offers observed during the current shopper/stall visit, not an archive
directory. The independent History screen supplies that directory. Cancelling a waiting inquiry does
not cancel a merchant's already-issued quote.

`NativeTradingFixtureTests` earns the reward and lets Wren finish her funded build through ordinary
cutoffs; with `OPENFANTASY_NATIVE_TRADING_FIXTURES` naming a fresh directory it exports `save.sqlite`,
`genesis.ofc`, `before.ofc` and `metadata.json`. `tools/check_native_trading.py --dotnet <path>
--godot <path> --fixtures <directory> --output <json> [--mode all|keyboard|controller|pointer] [--rendered]`
drives each route through the wheel and stall (buy, a genuine decline, show and sell, saved record),
then reopens each copy in a separate paused process. Keyboard and controller runs move focus only with
arrows or the D-pad from where the game puts it; every route reaches, cancels and chooses dropdown rows
itself. Its manifest feeds `NativeTradingResultTests` via `OPENFANTASY_NATIVE_TRADING_RESULTS` to
authenticate the journal commands and goods/money deltas.

`NativeTradingBoundaryFixtureTests` prepares three closed saves through ordinary cutoffs under
`OPENFANTASY_NATIVE_TRADING_BOUNDARY_FIXTURES` (`manifest.json` plus `expired/`, `unfunded/`, `moved/`):
an earned offer with 40 of its 1,200 steps left, an offer to a player with the source-zero purse, and an
offer reviewed just before Wren walks to her funded work. `tools/check_native_trading_boundaries.py
--dotnet <path> --godot <path> --fixtures <directory> --output <json> [--rendered]` runs each on the
keyboard route: review, the refused confirmation attempted anyway, then lawful recovery (ask again; earn
the quest reward and re-review; re-review after Wren returns). `NativeTradingBoundaryResultTests` reads
`OPENFANTASY_NATIVE_TRADING_BOUNDARY_RESULTS` and authenticates the refusal reason from the recorded
state at the attempt, the single later acceptance and its exact deltas.

## Cargo circuit

`CargoWorldAdmission.CreateEmbedded` composes a separate fresh cargo world: the same farm plus Marla in
Brasshollow (eight rations, no purse) and Kegga Toolwright in Gearford (no rations, an 80-gil purse) at their
retained tiles, joined by an authored fourteen-region corridor.

Play it natively through its own entry scene:
`godot --path client res://scenes/NativeCargo.tscn -- --cargo [SAVE]` (default save
`user://worlds/openfantasy-cargo.sqlite`). A new save is created fresh; an existing save opens only when it is
this same cargo world, and no other save is upgraded or relabeled. Earn Wren's quest reward (walk to her, **E**
to talk, **G** to accept, **G** to deliver, **E** to close), then walk down-screen (increasing Y) to Marla's stall
in Brasshollow, and from there follow the road east and then a long way up-screen (decreasing Y) to Kegga in
Gearford. The view draws only the regions near you, from their actual terrain: openings and channels by depth,
banks above your floor as raised blockers, committed water with the watercourse surface, and your own work
target outlined. The farm's breach divider can be mined (**Q** selects it, **V** opens its mining action) and the
released water is drawn as it settles. Marla and Kegga appear in their own original appearances.

**V** then **Q** reaches the Market page as in `--markets`. Opening **Offers** while no stall is within reach only
looks at a stall; choose one under the panel's **Stall** selector (it lists both stalls with place and reach) or
walk to a merchant, whose stall is chosen when you open it within reach. A chosen stall stays chosen while you
travel: at Gearford with Marla chosen, the wheel says Kegga's stall is here and to choose it under Offers → Stall.
A left click on a merchant also chooses that merchant's stall during ordinary unpaused exploration. Nothing is
asked, shown or confirmed for you. A spot that cannot be shown on one supported floor keeps the last view with a
notice; it never closes the game.

`NativeCargoFixtureTests` earns the quest reward in a fresh cargo world; with `OPENFANTASY_NATIVE_CARGO_FIXTURES`
naming a fresh directory it exports `save.sqlite`, `genesis.ofc`, `before.ofc` and `metadata.json`.
`tools/check_native_cargo.py --dotnet <path> --godot <path> --fixtures <directory> --output <json> [--rendered]`
walks the route with held arrow keys (farm → Brasshollow → Gearford → Brasshollow), opens Offers at the farm
without pinning a stall, buys, runs a complete saved-history replay while frames keep rendering, shows, sells and
reads saved records through the production wheel and panel, checks projection agreement plus settled actor
transforms, last-drawn terrain and framebuffer pixels, then reopens the save copy in a separate paused process. A
third process uses the production `--cargo` selection on a fresh disposable save (`--cargo-onboarding-probe SAVE
REPORT`): Wren's quest by keys, three wheel mining actions and the released water, then the walk to Marla. Its
manifest feeds `NativeCargoResultTests` through `OPENFANTASY_NATIVE_CARGO_RESULTS`. See
[cargo presentation](docs/architecture/cargo-presentation.md).

`CargoCircuitJourneyTests` drives the whole loop through session cutoffs
(an inherited wolf fight to release, quest reward, purchase, 457-cell walk with a save/reopen at the corner,
showing and sale, walk back) and, when `OPENFANTASY_CARGO_CIRCUIT_EVIDENCE` names a fresh absolute directory,
exports snapshots, saves and `results.json` (including the declared corridor overlays and journal checkpoint
and archive counts) there. Bounded refinement cases live under `tests/.../Content/Cargo/Refinement/`. See
[cargo circuit](docs/architecture/cargo-circuit.md).

### Retreat-capable cargo world

`CargoRetreatWorldAdmission.CreateEmbedded` composes the same cargo world and then admits the explicit retreat
interpretation (profile 34). Open it through its own entry scene:
`godot --path client res://scenes/NativeRetreat.tscn -- --cargo-retreat [SAVE]` (default save
`user://worlds/openfantasy-cargo-retreat.sqlite`). A new save is created fresh; an existing save opens only when it
is this same world. A `--cargo` save is never upgraded or read as this one; moving a lived save into retreat rules
would need its own explicit, replayed transition. `tools/play.py` does not select this route.

In a fight (Q selects the wolf, E engages it), **V** opens the wheel; **Up**, the D-pad up or pointing at the upper
sector selects **Retreat**. Its details name the destination cell and direction, the number of steps, the total
recovery and any reason it is unavailable. **Enter**, controller **A** or **Confirm** commits that route; **V**,
**B** or **Close** cancels without sending anything. Each step is walked at your own turns and costs readiness while
the wolf still acts; when the last step leaves its reach the encounter ends with your wounds and remaining recovery
kept. A blocked step stops the retreat without moving you. Wolves keep fighting unless a host retreat preference is
configured, which this launch does not do.

`NativeRetreatFixtureTests` earns Wren's reward, buys one ration from Marla and walks back beside the wolf; with
`OPENFANTASY_NATIVE_RETREAT_FIXTURES` naming a fresh directory it exports `save.sqlite`, `genesis.ofc`, `before.ofc`
and `metadata.json`. `tools/check_native_retreat.py --dotnet <path> --godot <path> --fixtures <directory> --output
<json> [--rendered]` runs keyboard, controller and pointer Retreat selection (one cancelled inspection, one commit,
observed steps, disengagement and held-key travel), then a separate paused reopen; its manifest feeds
`NativeRetreatResultTests` through `OPENFANTASY_NATIVE_RETREAT_RESULTS`. See
[encounter retreat](docs/architecture/encounter-retreat.md).

### Threatened cargo road (Bandit Chief)

`RoadThreatWorldAdmission.CreateEmbedded` composes the retreat-capable cargo world with the retained bandit camp's
Bandit Chief standing beside the lane crossing. Open it through its own entry scene:
`godot --path client res://scenes/NativeRoadThreat.tscn -- --cargo-threat [SAVE]` (default save
`user://worlds/openfantasy-cargo-threat.sqlite`). A new save is created fresh; an existing save opens only when it is
this same world, and neither the `--cargo` nor the `--cargo-retreat` save is ever upgraded or read as this one.

Walking the lane north from the farm road toward Gearford, the status area warns **Danger nearby · Bandit Chief guards
the road** once the chief is actually in view — within three cells on the same floor with a clear line of sight —
which is about 2.8 cells before it can reach you; hover it for its level, HP, reach and distance. Nothing about the
chief is shown while it is out of view. Within about one and a half cells of the chief, in its sight, it attacks: the
encounter starts by itself (an action you are still submitting does not hold it back; that action is refused) and the
world waits for your choice. **E** strikes (it barely marks an 800-HP chief and invites its counterattack); **V**, then
**Up** and **Enter** (or the controller/pointer equivalents) retreats. The chief does not chase or return home; after an
escape it may still be catching its breath, which is ordinary readiness, not safety. Without pursuit, stepping a cell
off the lane already passes outside its reach; the longer detour three cells west is one valid route. Carried rations
still sell at Kegga afterwards. Being incapacitated ends that world's journey: nothing releases or rescues you.

`NativeRoadThreatFixtureTests` exports, with `OPENFANTASY_NATIVE_ROAD_THREAT_FIXTURES` naming a fresh directory, a
fresh farm fixture in `farm/` (the unplayed save at commit zero) and a near-road fixture (Wren's reward earned, one
ration bought from Marla and walked to road cell (346,318)), each as `save.sqlite`, `genesis.ofc`, `before.ofc` and
`metadata.json`. `tools/check_native_road_threat.py --dotnet <path> --godot <path> --fixtures <directory> --output <json>
[--rendered]` runs the keyboard journey from the farm (Wren's dialogue reward, Marla's stall purchase, the outbound
lane, the observed warning, the chief's entry, a strike and its real hit, Retreat inspection/cancel/commit, no reentry,
detour, Kegga sale) and shorter controller and pointer Retreat routes from the road, then a separate paused reopen; its
manifest feeds `NativeRoadThreatResultTests` through `OPENFANTASY_NATIVE_ROAD_THREAT_RESULTS`. See
[road threat](docs/architecture/road-threat.md).

The separate outcome-completion world (profile 35) removes that dead end without changing any earlier profile:
`OpenFantasy.Host --create-cargo-outcomes <world.sqlite> <seed> <player-name>` creates it, and
`NativeEncounterOutcome.tscn` (`--cargo-outcomes [SAVE]`) opens only it. A resolved encounter is completed by the world
once every required player in the fight has read the result and no care obligation is pending; the winner reads **Continue** and keeps exploring with recovery
retained, a defeated player reads **Accept defeat** and then sees a terminal explanation with **Save and leave**.
Accepting asks the world to continue: the next ready boundary completes the encounter and closes aftermath care, so
admitted care (E / A while it is offered) must be chosen first. The separate HUD accept button has no E hint; the wheel
offers acceptance on the right and care on the left. V opens the wheel without committing. Accepting declines available care.
The outcome panel (`scenes/EncounterOutcomeOverlay.tscn`) shows only your own or currently observed condition and
states projected care reasons without treating a missing view as a missing agreement or a patient request as helper
commitment. Compact HUD text links to full card/tooltip explanations. Nothing is rewarded, revived or removed. See
[encounter outcome](docs/architecture/encounter-outcome.md).

`NativeEncounterOutcomeFixtureTests` exports declared fresh outcome fixtures when
`OPENFANTASY_NATIVE_ENCOUNTER_OUTCOME_FIXTURES` names an empty directory: `victory/` (chief declared at 1 HP) and
`defeat/` (production chief) carry an earned, bought ration to road cell (346,318); `care/` (player declared at 1 HP)
uses genuine managed approach and entry, stopping at the player's first decision boundary of the first wolf encounter
beside Wren. It does not prove native care-specific approach or entry. `tools/check_native_encounter_outcome.py
--dotnet <path> --godot <path> --fixtures <directory> --output <json> [--rendered]` then runs the native victory
(strike, read, Continue, detour, Kegga sale), defeat (Accept defeat, terminal Save and leave) and HUD/wheel care-decline routes
and a separate paused reopen of the defeat save, checking post-draw card, HUD and text bounds against the viewport and both
combatants; its manifest feeds `NativeEncounterOutcomeResultTests` through `OPENFANTASY_NATIVE_ENCOUNTER_OUTCOME_RESULTS`,
which re-derives each declared genesis and replays the complete journals. A bounded passive submission observer also
records each pre-input selection's full canonical command, actual submitted command and receipt. Authentication compares
them with journaled commands and rejects altered nested terms, receipts, identities or missing rows. These are the
verifier's contracts; execution results and unresolved routes remain in the implementation packet.


The uncalibrated accepted-care route uses `NativeAcceptedCareFixtureTests` with
`OPENFANTASY_NATIVE_ACCEPTED_CARE_FIXTURES=<fresh-directory>`. It exports the unplayed production genesis;
all movement away/back, first-wolf entry, paid Wait choices, patient care request and subsequent outcome acceptance
come from native controls. Run `tools/check_native_accepted_care.py --dotnet <dotnet> --godot <godot>
--fixtures <directory> --output <new-result.json> --rendered`. It launches the explicit disposable
`NativeEncounterOutcome.tscn -- --cargo-outcomes-probe <save> <report> <genesis> <metadata> care-accepted
--background-render`, then `care-reopen` in a separate process. The output's authentication manifest feeds
`NativeAcceptedCareResultTests` through `OPENFANTASY_NATIVE_ACCEPTED_CARE_RESULTS`. Rendered checks and full
journal authentication are separate required gates; neither alone qualifies treatment. The saved authority authenticates
Wren's own action and exact consumed dose independently of the patient's request. These disposable controls preserve
normal focus policy and do not establish physical-device, human art or multiplayer acceptance.

### Shared player host API

`LocalWorldSession.Player` exposes the original local player port. `ConnectPlayerAsync(actorId, delivery,
regions)` attaches another already admitted human to the same in-process host, initially unready, up to
four live ports. Each `LocalPlayerConnection` has its own view, readiness, movement, commands, inquiry
and participant-inspection methods. Dispose a port to leave without closing the host; stop the host to
close the authority. All ports share one clock and NPC scheduler. See the
[connection contract](docs/architecture/hosted-player-connections.md) for scheduling, privacy and recovery.
This API does not authenticate network transports or implement the planned recorded disconnect handoff.

### Durable controller allowance

Controller profiles 36–38 can explicitly use storage schema 10 to retain measured reconnect allowance
across host restarts. The operator requires exclusive access to each save:

```sh
OpenFantasy.Host --create-recoverable-world new-world.sqlite controller-genesis.ofc
OpenFantasy.Host --enable-controller-recovery existing-schema-nine.sqlite new-recoverable.sqlite
OpenFantasy.Host --inspect-controller-recovery new-recoverable.sqlite
```

Creation requires an admitted fresh controller genesis. Enabling recovery copies into a new destination
and retains original canonical history; it does not modify the source. Existing outstanding allowances
begin with explicit unknown-prior-duration provenance and a zero measured lower bound. Subsequent live
hosts retain durably observed time, capped at 30 seconds. Inspection and ordinary opening start no timer.
Offline duration and uncommitted crash time are uncharged; repeated crashes can extend effective grace.
Graceful shutdown checkpoints at the stop request. See the
[recovery contract](docs/architecture/controller-recovery.md) for transactions, fencing and limitations.
