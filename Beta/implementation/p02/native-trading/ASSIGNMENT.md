# Native trading implementation assignment

Implement a substantial playable local trading slice on `214da29bebfbc30e6dbc4ad92d394186d16aa2c6`.
The owner explicitly requests Opus 5.5: exact `claude-opus-5-5[1m]`, Max effort, 1M context.
Finite assignment: 5,400 seconds and $40 client-estimated usage. No model substitution, paid retry,
recursive delegation, installs, Git/index operations, foreign app control or broader tool access.
Astra integrates; you are not alone in this checkout. Preserve all unrelated changes.

Read root/implementation AGENTS, current handoff, the scheduled-trading NATIVE_FOLLOWUP and integration
review, and the living PLAYER_INTERFACE_AND_NAVIGATION supplement. Read affected architecture pages
and the supplied Godot UI/input/technical-design skills. Read task-relevant source, not the research archive.
The full cargo campaign, legacy migration and later phases remain obligations; do not reduce their scope.

First create `RESULT.md` in this packet with a brief in-progress scope to verify the actual write grant.
If an operation is denied, stop and report the exact operation/path; do not bypass it with another tool.

## Deliverable

1. A production fresh market-world admission and explicit `--markets` launcher/save selection. Reuse
   ordinary-care admission -> fresh paid exchange -> Wren berry market. Bind opt-in merchant session
   preferences and the existing gift capability. Existing saves/modes continue; no silent migration/reset.
   Use Wren's six admitted berries and ordinary quest reward money. No test fixture in production,
   injected balances, second inventory, free replenishment or altered source facts. A small production
   factory/configuration is preferable to embedding a test scenario in the client.
2. Icon-led contextual market actions and a native scene-owned panel. Support buying available stock,
   selecting/showing an owned lot and quantity for sale, requesting/reviewing a merchant reply, precise
   quote inspection, explicit confirmation and cancellation. Tooltip/focus detail includes price,
   quantity, fee, total, age/expiry and relevant constraints. Do not expose secret stock bases, issuer
   control credentials or unrelated actors' private quotes/balances. Narrow local interaction DTOs may
   extend the existing safe projection; no second authoritative model.
3. Separate inquiry, world readiness, movement suppression, reading and consent. The merchant answers
   at an advancing cutoff: a paused reading panel must not deadlock that answer. Author a deliberate
   finite human-readable window in NEW content/configuration, preserving existing issued terms,
   defaults and expiry tests. Selection/quantity/target changes, stale terms or focus loss cannot confirm,
   retarget or auto-requote. Re-review after an explicit user action is acceptable. Explain what reading
   pauses locally; do not claim multiplayer Wait is solved. Native flow preserves normal movement,
   input ownership, wheel/pack focus/cancellation and gifts/care.
4. Actual buy and sell through the normal session boundary, current outcome/history inspection and
   paused separate-process reopen. Keep archive-only discovery as an explicit residual if not surfaced
   by the native UI; do not advertise it just because the Host has an archive API.
5. Meaningful managed contrasts for admission/real stock/funding, draft invalidation/privacy/timing,
   and relevant input models. Native fixture export is a real earned world/closed DB. New rendered
   probe drives actual InputEvent/native Controls through normal UI code, authenticates settled views
   and exact identities, and produces inspectable screenshots. Do not call the final action handler or
   emit Pressed directly as a substitute for a claimed input route.
6. Timeless architecture references and concise runtime controls/save documentation updated in the
   same change. Keep progress, limitations, measurements and failures in RESULT.md, not architecture.
   Source identifiers/comments/runtime strings remain OpenFantasy, never the planning folder codename.

Application C# is modular, human-readable, object-oriented: cohesive models/controllers/panels and short
engine callbacks. Reuse the accepted Host protocols instead of duplicating authorization/arithmetic.
Re-use existing UI glyph conventions; new small UI SVGs are permitted. Pixel art/actors/movement style
are outside this assignment and remain separate artist/human acceptance work.

## Ownership

You may edit/create only these deliverables, with protected baseline hashes additionally enforced:

- `Runtime/src/OpenFantasy.Hosting/Content/MarketCircuitPolicies.cs` if needed for explicit new window
  configuration; new `Content/NativeTrading*.cs` production composition files.
- `Runtime/src/OpenFantasy.Hosting/Simulation/` only necessary safe market projection or client binding;
  prefer existing APIs. No change to scheduling/authority semantics merely to make native tests pass.
- Existing client scripts: FarmGame.cs, FarmGame.Gifts.cs, FarmClientSession.cs, FarmInputRouter.cs,
  FarmInputMap.cs, FarmQuickWheel.cs, FarmQuickWheelModel.cs, FarmWheelDisc.cs, FarmActionOffers.cs,
  FarmActionOffer.cs, FarmHud.cs, FarmInventoryScreen.cs, FarmInventoryModel.cs, FarmTargetSelection.cs.
  New trading-related scripts/UIDs in `Runtime/client/scripts/`; relevant scene integration and new scene
  in `Runtime/client/scenes/`; market glyphs in `Runtime/client/assets/ui/`.
- `Runtime/tools/play.py` and new `Runtime/tools/check_native_trading.py` only.
- New tests under `Runtime/tests/OpenFantasy.Hosting.Tests/Client/Trading/` and `Content/Trading/`.
  Existing tests remain byte-exact. The test csproj permits ONLY additive lines linking NEW engine-free
  client scripts: four spaces then `<Compile Include="../../client/scripts/NAME.cs" Link="Client/NAME.cs" />`.
  No property/package/existing link edits; baseline bytes are saved in this packet.
- `Runtime/README.md`; architecture `presentation-and-interaction.md`, `runtime-and-authority.md`,
  `world-markets.md`, `scheduled-trading.md`, `README.md`, new `native-trading.md` if useful.
  Immutable input snapshots of editable pages are supplied in the task directory.
- This packet's `RESULT.md` only. The lead owns assignment, pins, wrappers, dispatch, handoff and checks.

Core, Worlds, existing probes/verifiers, other client scripts, source assets, project.godot, client csproj,
source SQLite, frozen specs and unrelated predecessor staging are protected. If needed work exceeds
ownership, report the precise dependency; do not change it or silently claim completion.

## Required evidence

Lead-owned `run_checks.py` holds the shared heavy-check lock, preserves old tests/core/source state,
pins sources/binaries and retains failures. Run only these exact Bash commands with a timeout sufficient
to observe terminal completion (up to 600,000 ms). No shell listings, pipes, redirects, background jobs,
other wrappers or manual commands. Read/Glob/Grep provide discovery inside repository/private task paths.

`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/native-trading/run_checks.py build`
`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/native-trading/run_checks.py unit`
`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/native-trading/run_checks.py regression`
`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/native-trading/run_checks.py fixtures`
`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/native-trading/run_checks.py native`
`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/native-trading/run_checks.py native-auth`
`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/native-trading/run_checks.py legacy-native`

Use focused checks during development; one final regression on integrated source. Any source edit
requires a new accepted build before new checks. Do not leave commands running on return. If the tool
cannot observe completion, report its handle/output path as unfinished, not a pass. No threshold changes.

Fixture contract: new `NativeTradingFixtureTests` exports under OPENFANTASY_NATIVE_TRADING_FIXTURES with
closed `save.sqlite`, `genesis.ofc`, `before.ofc`, `metadata.json`. The fixtures check also exports the
unchanged native-gift fixture. No original DB opens: copy original bytes if ever needed.
Native verifier accepts the same arguments as check_native_gifts.py, including `--mode all` and
`--rendered`, and returns JSON `status`, `managedAuthenticationManifest`, useful pins/limits/runs/paths.
Use a per-run unfocusable clone (never edit source project.godot), no focus requests. Preserve existing
180-second process / 150-second probe / 30-second opening bounds, settled views and complete identities.

All three injected routes must exercise selection, exact confirmation and cancel without duplicate
commands; buy and sell, stale/refused offers, changed selection/focus, saved outcome and paused reopen
must be proved with truthful route coverage. Reuse existing controller/keyboard/pointer helpers where
available; distinguish wheel, form and physical-device coverage precisely. Native screenshots should
show readable buy/sell inspection and outcomes. Managed `NativeTradingResultTests` reads the manifest
from OPENFANTASY_NATIVE_TRADING_RESULTS and independently authenticates actual journal records and
item/currency deltas, not just self-reported JSON. The legacy-native mode runs existing controller gift
active/reopen and its unchanged journal authentication to detect shared input regressions.

Do not weaken/assert-away old failures or claim manual art/physical-device/multiplayer/production gates.
Report authentic blocker/partial delivery within the budget rather than scaffolding false success.

## Compact result

Record changed responsibilities, exercised and unexercised behavior, every final report path, observed
failures and disposition, screenshots to inspect, architecture updates, tool friction and residual work.
Do not paste transcripts or huge logs. Astra reviews exact edits and authoritative evidence before commit.
