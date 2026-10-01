# Scheduled trading through participant boundaries

Implement the next application binding over accepted fresh profile 33 at `257f460`.
Use exactly `claude-opus-5-5[1m] --effort max`; finite limit 3,600 seconds and $35 client estimate.
No nested agents, paid retry, model fallback, installs, staging or commits. Astra integrates and
reviews. You are not alone in the checkout: preserve other writers and the predecessor index.
First write RESULT.md with an in-progress note to verify the granted edit route.

Read root/Beta AGENTS, current handoff, the local-market integration's next dependency, and affected
architecture. The maintained technical-design skill includes the latest privacy and economy lessons.
This task moves actual application behavior forward; no native art/UI is assigned. Keep readable,
modular C# with cohesive owners. Core rules/codecs and all 532 existing test files are immutable.

## Required outcome

1. Add participant-safe paid quote inspection: exact fixed terms and lifecycle only for a currently
   authenticated issuer or counterparty. Unknown and foreign quotes are indistinguishable. Do not
   expose another actor's principal/session credentials, complete root, unrelated inventory, market
   stock basis or current distant presence revision in the public view. A buyer can prepare its own
   exact acceptance from inspected terms with an actually observable issuer presence revision.
   Keep confirmation distinct from quote issuance and from settlement; never silently requote.
   Include terminal outcomes after a real archive cut and reopening, using authenticated replay
   like saved-proposal inspection. Keep expensive historical reads off the client frame and latency-
   sensitive callback; handle shutdown/serialization honestly. Do not introduce a second archive owner.
2. Add a bounded authenticated shopper inquiry and scheduled merchant response through the existing
   `LocalWorldSession` cutoff. The player's inquiry belongs to that session's actor. A shopper names
   the market/commodity, direction, quantity and its own selected disclosed lot when selling, not a
   merchant delivery or a private merchant lot ID. The configured host merchant chooses an eligible
   own lot for a sale and uses only live showings addressed to it for a purchase. It alone prepares
   and issues through its actual host control. Check availability/locality before disclosing data.
   A merchant cannot learn an inquiry from the shopper's private price memory or scan private bags.
   An inquiry is nonbinding application input: make its bounds, cancellation, repeated polling,
   pause/hold/unavailable behavior and restart meaning explicit. Do not invent a durable promise or
   new world profile for it. Issued quotes retain the already durable world lifecycle. Accepting or
   ignoring an offer is a separate shopper choice. Busy/refused inquiry processing must not fault the
   session or flood the quote book with new identities on every tick.
3. Bind opt-in NPC trading preferences to normal scheduled action selection, composing with care,
   gifts, movement and ordinary work arbitration. At minimum prove a host merchant answering an actual
   player inquiry in each direction, and a configured NPC shopper completing an ordinary trade with a
   host merchant using its own admitted control, funds and disclosed goods. A simple bounded authored
   desire/stock target is enough; do not claim full cognition, demand simulation, route/wage contracts
   or offscreen commerce. Players and NPCs use the same quote/acceptance rules; no forced sale, spawned
   funding, secretly renewed stock, direct snapshot mutation or test-only shortcut into the controller.
   Default behavior and older profiles must remain unchanged when trading is not configured.
4. Prove the binding through real `LocalWorldSession` cutoffs and SQLite authority, not only manually
   calling a selector. Reuse the accepted market fixture's genuine reward/gift routes and the manual
   session time provider. Cover buy and sell, finite funding, no automatic player acceptance, player
   cancellation/pause, unauthorized/foreign inquiry and query, remote/unshown/stale refusal with lawful
   contrasts, exact receipt/reopening, and ordinary work exclusion for both parties during acceptance.
   Confirm no extra movement or competing actor work enters a valid accepted fill; a conflicting NPC
   choice must not reject an otherwise valid player action. Demonstrate bounded repeated selection and
   return to useful work after refusal/expiry. Explain the precise scheduling/fairness policy rather
   than claiming canonical ID ordering proves fair service. Preserve all existing assertion thresholds.
5. Update the timeless architecture with state ownership, APIs, visibility, transient inquiry versus
   durable quote, cutoff arbitration, historical lookup and extension boundaries. Record progress,
   exact checks and genuine remaining gaps only in RESULT.md. This does not close the full market,
   cargo/escort, native, multiplayer or P02/P00–P14 campaign gates.

Inspect `LocalWorldSession.SelectBatch`, the saved-proposal controller/query/inspection paths and
the market application service before choosing types. Extract a small shared helper only where it
avoids real duplicated semantics. Do not generalize all action scheduling or change accepted rules.
The allowed source scope permits a coherent host solution; report any indispensable core change
instead of silently editing it. A public `SpotQuote` embeds issuer delivery and is not itself a
safe participant DTO. Avoid also leaking delivery through nested outcome/acceptance values.

## Owned deliverables

- New trading-related types/partials in `Runtime/src/OpenFantasy.Hosting/Simulation/` and
  `Runtime/src/OpenFantasy.Hosting/Worlds/`; changes to existing hosting files only where needed for
  the above session, query, control or recovery integration. Preserve other behavior and file ownership.
- New tests under `Runtime/tests/OpenFantasy.Hosting.Tests/WorldExchange/Scheduled/`. The entire
  existing test suite, including accepted market fixtures, stays unchanged; new helpers can compose it.
- `Runtime/docs/architecture/scheduled-trading.md` and necessary updates to `world-markets.md`,
  `world-paid-exchange.md`, `runtime-and-authority.md`, `presentation-and-interaction.md` and `README.md`.
- This packet's `RESULT.md` only. Instructions, handoff, wrappers, pins and previous evidence are
  read-only. The granted wrapper writes its own qualification evidence.

No Core, client, source content, frozen specs, predecessor, ecosystem or other project edits.
No native game launch or foreground focus changes. Source/persistence compatibility is required.
The immutable input snapshots in the private task directory document editable architecture inputs.

## Verification

Run these exact commands serially (Bash timeout 600000); observe each terminal result before returning:

python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/scheduled-trading/run_integration_checks.py build
python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/scheduled-trading/run_integration_checks.py new-host
python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/scheduled-trading/run_integration_checks.py session-host

The check wrapper has a shared lock, exact source/assembly pins, immutable Core/old tests/spec/source
DB/staging checks and finite limits. A task handle is not terminal evidence. Start verification early
enough to fix errors within this same request. Read/Glob/Grep for discovery; these are the only Bash
grants. Never open the original retained SQLite file directly, even read-only; use copies where needed.
If a tool is denied, stop that path and report it without a bypass or paid retry. Return a compact map
of implementation, exact evidence paths/counts, failures/dispositions and remaining work. Honest partial
delivery is preferable to calling an unexecuted or bypassed integration complete.
