# Scheduled offers and explicit NPC gift decisions

Implement a coherent application binding from `d987bbdb6aff9cb1e43f01a5d79eaafd539eea7b`.
Exact `fable[1m]`, 1M context, Max effort; one run, 3,600 seconds / $36 client estimate.
Astra owns integration, review, Git and native acceptance. You are not alone in this checkout:
preserve others' work, especially the 19 unrelated staged predecessor files. No Git mutation,
recursive/relay agents, paid retry, model switch, package/toolchain changes or native windows.

## Outcome

Use the real `LocalWorldSession` scheduler for the saved-offer lifecycle and for explicit Host NPC
gift decisions. A configured helper can offer an actual owned dose, the player can independently
accept it on a later cutoff, and the player can spend that dose through ordinary self-use. Conversely,
an NPC can choose to accept or decline a player's offer through its own current control and policy.
No proposal-service call may advance time outside the session's selected cutoff. Do not return only
a DTO/controller scaffold, bypass authority or reconstruct a lived state to make the journey work.

Read AGENTS, current handoff, `../material-proposals/INTEGRATION_REVIEW.md`, the full custody integration
plan and the affected reference pages. Follow the world-composition discipline: separate permission,
declaration, decision, commitment and receipt. Read only relevant source/spec sections, not the archive.
Architecture snapshots in the private task directory are immutable inputs; original pages are editable
deliverables. Make a small meaningful owned notes edit first to verify actual edit access.

## Required behavior

1. Bind all four player proposal intents to `LocalWorldSession.SubmitAction` and its existing queue,
   serialized authentication, readiness, tick, preview, commit, pause, stop and exact retry semantics.
   Explicitly reject unsupported profiles and incompatible reading/encounter/aftermath/consequence
   contexts before constructing a fatal incompatible batch. A refusal must not fault the session or
   advance an extra clock. Stale admissible commands retain normal durable gameplay refusals.
   A pending request is not durable acceptance. Do not call `MaterialProposalService.Submit` from a
   frame, view or tick to run a second advancing transaction.
2. Add a bounded participant-only proposal projection to `LocalWorldView`, reusing the existing
   projection contract where appropriate. Capture exact terms/grant/presence tokens; never silently
   refresh a stale intent. Show open/terminal/due states without leaking other inventories, private
   controller preferences, credentials, world roots or command streams. Inspection causes no write.
   Keep historical replay out of Tick/ProjectView: an explicit serialized inspection API may retrieve
   a named archived result, without letting caller-supplied actor IDs choose another controller.
3. Implement a concrete, explicit, immutable Host NPC policy configuration for this local gift flow.
   Existing constructors/defaults keep existing behavior; opt-in policy identifies actual Host actors,
   intended partners and admitted desired restorative items/definitions. It is advice, not authority.
   Care permission, friendship, Host ownership or merely being nearby never implies a gift policy.
   Choose and document sensible finite-stock thresholds/preferences; no fabricated medicine, source,
   hands, reach, actor binding or patient injury. No generic ECS, universal policy framework or hidden
   persistent sidecar. A model's private memory cannot decide whether an offer was already made.
4. The configured NPC can offer one exact owned dose for a meaningful observed need and can accept a
   wanted incoming gift or decline an unwanted one. The NPC's own actual command and control are used,
   never forced acceptance or player impersonation. Share existing Core legality and complete-root
   validation; policy preference is distinct from legality. Offer creation and later settlement/use
   have separate cutoffs/receipts. Existing construction, wandering and ordinary aid remain coherent.
   Explicitly arbitrate selected actors and bilateral opportunities: a valid player acceptance must
   not be perpetually defeated by the sender's automatically chosen work/aid. An invalid/foreign/stale
   request must not grant the player control over unrelated NPC decisions. Prove contrasting cases.
5. Stable semantic NPC command identities prevent unchanged declined/expired/refused proposals from
   being regenerated every tick, after movement, unrelated inventory activity, archive cut or reopen.
   Use permanent occupied identities and actual saved lifecycle. An occupied preferred choice must
   not cause fresh-ID churn, another lot/partner fallback or an automatic retry. Define which meaningful
   changed need or item creates a new decision. Do not treat an open offer as a stock reservation.
6. Enable the ordinary restorative views/controller/player route needed for admitted profile30 so an
   actually accepted item can be used through the same session. Audit dependent profile guards,
   including supported excavation/lifecycle routes, without broad numeric-range acceptance. Add an
   exact profile-family case only where the Core already supports it and the tests prove the join.
   Do not change Core rules, codecs, schemas, tick duration, old bounds or existing assertions.
7. Update timeless architecture in the same change: session ownership and cutoff arbitration,
   projection privacy, explicit NPC preference/selection identity, stock/consent separation and
   actual restorative/profile bindings. Keep dates, results and pending gates in this packet.

## Executable evidence

Use existing real fresh-admission composition hooks and fixtures: `MaterialProposalTestSession`,
`MaterialProposalWorldFixture`, `ConsentedCompanyFixture`, `OrdinaryCareWorldFixture`,
`CareRewardJourney`, `ManualSessionTimeProvider` and `SessionFixture.Tick/Until`. They admit finite
stock and actual actors; inspect before declaring a fixture/ownership gap. Preserve their current
files; new helpers can reuse them. Build new tests named `ScheduledMaterialProposal*`.

- Real SQLite + actual controlled ticks: helper-owned offer -> no player transfer yet -> explicit
  player acceptance -> exact moved provenance -> player self-use -> health/resource/material/time
  evidence -> dispose/reopen and immutable receipts. Keep treatment and gift permissions separate.
- Reverse player gift, NPC wanted acceptance and unwanted decline, with actual finite owned items.
  No NPC command chosen by client; missing/invalid policy does not become implicit acceptance.
- Paused/unready, reading hold, encounter/aftermath, stale exact terms, competing stock, missing/foreign
  proposal, stop with pending input, duplicate pending and completed requests, wrong actor/current
  delivery: defined non-faulting behavior and no extra ticks. Assert complete affected owners and time.
- Valid acceptance beside sender work/aid/movement, and invalid acceptance beside an otherwise real
  NPC choice. One action/opportunity per actor; no hidden second transaction or identity retry.
- No repeated NPC command for unchanged refusal/decline/expiry through ordinary ticks and reopened
  history. If cut-level proof is needed, reuse the real owner pressure path, not a hand-sealed archive.
- Projection contains only the player's pair data, no implicit command on read, no historical replay
  in a frame path; actual archived lookup remains authenticated if exposed through the session.
- Existing session, care, encounter, construction and proposal suites stay green with no old test edit.

Preserve exact old save bytes and profile30. This work does not establish multiplayer scheduling,
control rollover, native UI/device input, world-profile migration or the original retained520-dose
journey. These remain required later; do not relabel a fresh journey as that saved-world proof.

## File ownership

Edit only:
- `Beta/Runtime/src/OpenFantasy.Hosting/Simulation/LocalWorldSession.cs`
- `Beta/Runtime/src/OpenFantasy.Hosting/Simulation/LocalWorldView.cs`
- `Beta/Runtime/src/OpenFantasy.Hosting/Simulation/LocalNpcController.cs`
- `Beta/Runtime/src/OpenFantasy.Hosting/Simulation/LocalRestorativeController.cs`
- `Beta/Runtime/src/OpenFantasy.Hosting/Simulation/LocalRestorativeView.cs`
- `Beta/Runtime/src/OpenFantasy.Hosting/Simulation/MaterialProposalService.cs`
- NEW `Beta/Runtime/src/OpenFantasy.Hosting/Simulation/LocalMaterial*.cs`
- NEW `Beta/Runtime/tests/OpenFantasy.Hosting.Tests/Simulation/ScheduledMaterialProposal*.cs`
- `Beta/Runtime/docs/architecture/material-proposals.md`, `ordinary-care.md`, `runtime-and-authority.md`
- This packet's `IMPLEMENTATION_NOTES.md` and `WORKER_RESULT.md`.

No Core, client/asset, shared schema, old test/fixture or qualification-script edits. Report a real
ownership/contract gap instead of bypassing it. Astra owns handoff, evidence scripts and native work.
Keep application code modular, readable and object-oriented with named responsibilities; avoid growing
the session into a policy manager. Native tool denials are terminal: report exact invocation/path and
stop, without a shell workaround or permission expansion.

## Checks and finite limits

Only these exact Bash shapes are allowed from repository root, sequentially, no pipes/redirections:

python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/scheduled-material-proposals/qualification/run_checks.py build
python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/scheduled-material-proposals/qualification/run_checks.py focused ABSOLUTE_SUCCESSFUL_BUILD_JSON
python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/scheduled-material-proposals/qualification/run_checks.py regression ABSOLUTE_SUCCESSFUL_BUILD_JSON

Finish code before build; finish one check before another. Retain build240s/test600s and every existing
test's internal deadline. Preserve failures and explain any corrections. The final result must name
executed evidence and incomplete cases, not just assertions of completion. Do not run a native window
or request another paid job. Shared-desktop focus can belong to other games and is irrelevant here.
