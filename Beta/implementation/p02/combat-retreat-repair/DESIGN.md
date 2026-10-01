# Combat-retreat correction — design decisions

Inputs: `../combat-retreat/{ASSIGNMENT,DESIGN,RESULT,CRITIC_REVIEW,CRITIC_DISPOSITION}.md` (immutable). Profile 34 has
no accepted lineage, so its still-unaccepted law changes here: the rule text becomes `encounter.retreat.v2` and its pin
changes. Every profile-34 artifact of the original packet stays where it is as historical (pre-correction) evidence;
older profiles, codecs, histories and public bytes are untouched.

## Law (one rule each; players and NPCs)

- **Sample admission (P1).** `WorldEncounterRetreats.AdmitRoute` walks every route prefix through
  `LocalSpatialSnapshot.PrepareMove` and then the complete root admission (`WorldSnapshot.WithState`: liquid dry
  envelope, reservoir seals, custody, construction, identity) with all other owners unchanged. Queue admission,
  the advisor's verification and the resolver all use it; the resolver publishes the same root with its consistent
  charged timing and continued/finished retreat owner. The advisor expands only swept-body ground segments (cheap,
  bounded) and verifies candidates with `AdmitRoute`, memoising each placement's admission within one search, so it
  never encodes a root per expansion. Only the `WithState` call is caught; record/disengage invariant failures throw.
- **Contact (P2).** Clear means: for every living opponent, the XY distance between body centres (actual body
  dimensions, the attack geometry's centres) is strictly greater than that opponent's longest granted range plus one
  full cell (256). No cap, no sightline, no floor shortcut: occlusion, a missing cell or a different floor never
  grants escape. Advisor ranking and authority use the same `ContactExcess` function.
- **Scope (P3).** Exactly two positive sides in the episode, the actor is the only combatant of its side (living or
  downed; a downed ally is never abandoned), and the opposing side has a living member. Otherwise `Unavailable`
  (queue) / `AllyRemains` (sample). Party withdrawal/rescue remains a separate full-plan join.
- **Cost wording (P6).** A sample charges `ceil(10000·100/(800·speed))` readiness; the encounter clock advances in
  5-unit ticks, so a sample's interval is that charge rounded up to whole ticks. Offers state the walked steps, the
  accumulated readiness charge and the tick-quantised time of the walk; the last sample's charge stays as debt after
  disengagement (exit recovery). "One standard action per cell" is replaced by these exact terms.

## Holding, privacy and presentation (P4, P5, P7)

- A committed retreat holds its actor everywhere: `LocalEncounterActionView` blocks Wait/abilities with a new
  `Retreating` reason, `LocalWorldView.Encounter.PlayerDecisionQueued` is true while held, and the HUD shows
  "Retreating · step x of y". A new terminal record for the observer produces one HUD message (interrupted/cancelled
  reason or disengaged).
- `LocalRetreatView` projects progress and the terminal record only for the observer; other actors' retreats appear
  as an anonymous `OpponentRetreating` blocker with no route, destination or command id.
- `LocalWorldView.Retreat` is omitted from JSON when null, so pre-retreat public hashes are byte-identical.
- Wheel opening captures the offers of the view read at opening (`FarmClientSession.CaptureWheelOffers`), never the
  previous frame's, fixing the first-open stale Retreat/Strike/Wait capture.

## Evidence plan

- Durability: `RetreatDurabilityFixtureTests` exports closed saves just before three selected retreat publications
  (queue, a mid-route sample, the disengaging sample) with their host-selected step files and the unbroken result;
  `check_retreat_durability.py` runs the existing `--probe-world-step-crash` seam before/after each, SIGKILLs only its
  own child, recovers through `--inspect-recorded-world`, retries uncommitted steps and refuses duplicates;
  `RetreatDurabilityResultTests` authenticates journals/roots/receipts/identities against the unbroken originals.
- Checkpoint: a managed journey places the 6,000-step checkpoint inside an active retreat by ordinary idling and Wait
  boundaries, completes the retreat, then proves checkpoint bytes, prior retained bytes, full replay and exact retry.
- Counterfactual contrasts are marked as such and run only the authority's processor on otherwise admitted roots.
- Native: one deliberate first-open inspection; keyboard walks to Marla after disengaging and buys through the stall
  panel; reopen checks the carried lots and funds; result tests check every sample and the absence of other commands.
