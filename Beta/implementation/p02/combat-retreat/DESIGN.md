# Lawful encounter retreat — design decisions

Written before canonical contracts change. Timeless rules go to the architecture reference
(`encounter-retreat.md`); progress and acceptance limits stay in this packet.

## Interpretation and selection

- **Profile 34** (`WorldEncounterRetreatAdmission`) is a new opt-in fresh admission over an unplayed
  profile-33 local-market genesis. It adds one root owner, `EncounterRetreatState`, pinned by the
  versioned rule text `encounter.retreat.v1` and admission frame `world.retreat-admission.v1`.
  Profiles ≤33, their roots, steps, commands and stored histories keep their bytes and meaning;
  a retreat command in any older profile is refused at batch construction and decode. No golden
  bytes change. A lived profile-33 save is never upgraded; lived migration is a separate obligation.
- Host selection: `CargoRetreatWorldAdmission` composes the accepted cargo world, then profile 34.
  The native client opens it through a new entry scene (`NativeRetreat.tscn`, `--cargo-retreat`),
  the same `FarmClientSession`/`LocalWorldSession`/SQLite authority (schema eight family).

## Rule (one deterministic law for players and NPCs)

- `QueueEncounterRetreatIntent(encounter, expectedTimingRevision, destination, route)` — action
  codec **v19**, tag `world.action.queue-retreat`. It is a new typed intent; Wait and Release are
  untouched. The route is 1–32 cardinal samples of exactly 32 subunits (the ordinary movement
  sample); the captured destination must equal origin + route. The actor revision pins the origin.
- Queue admission (EncounterDecision boundary): authenticated control, actor revision, living
  positive-side participant, due, no queued choice, no retreat already committed in the episode,
  ground locomotion (`Condition.CanLocomote(Ground)`), sole living member of its side (supported
  party interpretation), every sample `Prepared` by `LocalSpatialSnapshot.PrepareMove` in sequence,
  and the destination **clear of contact**: no living opponent `QueryInteraction`-reaches the actor
  there within `min(768, opponent's longest granted range + 256)`. The 256-subunit margin is one
  full cell, the authority's maximum single displacement. Accepted: an Action-kind timing decision
  pinned to the command hash, plus the committed continuation retained in the retreat owner.
- Resolution: a committed retreat is the actor's standing decision at each of its due boundaries
  (Full Wait still holds every *undecided* required human). Each resolution publishes exactly one
  sample through `PrepareMove` and complete-world admission (liquid envelopes, reservoirs, seals,
  custody, construction), and charges readiness `ceil(10000·100 / (800·speed))` — one standard
  action per full cell. Opponents act between samples at their real boundaries; no catch-up.
- Completion: after the last sample, if the actor is still clear of contact and remains the last
  living participant of its side, the episode ends **Disengaged** in that same transaction: the
  lifecycle returns to Exploration, debts (including the last sample's) copy to the permanent ledger,
  pacing ends the binding with exposure/carry intact, other queued choices are cancelled with their
  identities retained. No HP, MP, reward, outcome release or position is fabricated.
- Interruption: a sample that is no longer `Prepared`/admitted (occupancy, terrain, liquid,
  controller, locomotion change) publishes no movement, still charges that sample's readiness,
  clears the continuation and records `Interrupted` with its reason; combat continues and the actor
  must decide again. Incapacity or an outcome cancels the continuation (`Cancelled`).
- Retained evidence: `EncounterRetreatState.Last` keeps the latest terminal record (actor, command,
  episode, origin, final feet, samples, readiness charged, status, reason, world sequence).

## Advice, NPC policy and presentation

- `LocalRetreatAdvisor` (host) offers the fewest-sample route to a clear destination by bounded
  breadth-first search over 32-subunit samples using the authority's own ground-segment clearance;
  ties prefer greater minimum opponent distance, then search order. Advice is never authority.
- NPC retreat is opt-in host advice (`LocalRetreatConfiguration`, HP permille threshold), resupplied
  on every opening, never saved; it submits the same intent through the same admission and costs.
- Native: the encounter wheel's Retreat sector (existing `traverse.svg` art) shows destination, sample
  count, readiness cost and blockers on hover/focus; confirm submits the captured offer once.

## Supported scope (explicit refusal boundaries)

Two positive sides in one coordinated complete-resident episode; the retreating actor must be its
side's only living participant (allies → refused `Unavailable`). Bystanders keep side zero and
identities. Opponents have no pursuit movement in this interpretation. Not claimed: party/joint
retreat, pursuit, independent pockets, four-client, multiplayer, bandit-camp threat, universal retreat.
