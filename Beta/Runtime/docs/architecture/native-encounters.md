# Native encounter interaction

## Owners and projections

[LocalEncounterActionView](../../src/OpenFantasy.Hosting/Simulation/LocalEncounterActionView.cs) projects
only the observer's granted abilities, visible targets, costs and current choice eligibility within the
admitted sight-complete local world. The authority's ordinary ability evaluator supplies resource,
anatomy, side and spatial legality. Readiness, pending choice, phase and outcome conditions further
restrict a presented choice. These are read-only offers, not authorization tokens. Other actors' grants,
private random state and world roots remain outside the client. Persistent visible incapacity belongs
to the actor projection, independently of whether an encounter is active.

[EncounterActionOffers](../../client/scripts/EncounterActionOffers.cs) captures the exact public scope,
actor revision, scenario or ability, target, episode and timing revision in the same
[FarmActionOffer](../../client/scripts/FarmActionOffer.cs) contract used by other interactions.
[FarmClientSession](../../client/scripts/FarmClientSession.cs) registers offers for its lifetime and
submits each offered command once through the durable session. A stale offer receives an ordinary
refusal; inspection cannot silently choose a different target or episode.

## Contextual controls

[FarmInputRouter](../../client/scripts/FarmInputRouter.cs) owns a distinct encounter input context.
Ordinary movement and farming do not reach the host while that context is active. The same Interact
intention selects entry, strike, outcome release or care acknowledgment as appropriate; Alternate
action selects Wait only in its applicable combat context.
Target cycling identifies actual observed opponents. Clearing a selection allows the sole opposing
actor to be selected contextually, without reinterpreting an explicitly selected different target.

[FarmQuickWheelModel](../../client/scripts/FarmQuickWheelModel.cs) pins its encounter choice to the
captured intent and actor revision. Readiness/timing changes invalidate that draft rather than replacing
it. Closing a wheel commits nothing. [FarmGame](../../client/scripts/FarmGame.cs) shares one frame's
offers between HUD and hotkeys, while the host independently validates submitted commands.

The encounter hold is a world rule, not a UI pause. Opening pack or wheel leaves readiness unchanged.
Explicit pause and window focus use the existing local host readiness policy. Held navigation directions
must be released after a blocking context before they can become world movement again.

## Care interaction

The public [care projection](aftermath-care.md#local-session-and-public-observations) takes precedence
over ordinary outcome actions. `EncounterActionOffers` captures the exact agreement, episode, lifecycle
revision and action hash. Its explanation distinguishes acknowledgment from later treatment and dose
consumption. Available, unavailable, pending and attention-required care remain visible for inspection;
a blocked offer grants no command. Strike, Wait, exploration release and ordinary farm actions are
excluded while this care context owns input.

An explicitly selected person must match the helper. With no person selected, a sole applicable offer
can name its helper even if the old opponent remains selected. Multiple agreements are not silently
ranked or substituted; ambiguous care remains blocked. Target cycling includes the publicly named
helper without exposing private inventory, credentials or material provenance.

The four-slot encounter wheel assigns its fourth slot to care when constructed from care offers.
That draft retains its action, helper and exact acknowledgment tokens. A later refresh can block it,
but cannot turn it into Continue or Strike. Cancellation and focus loss discard the draft without
acknowledgment. The care icon and hover/focus text share the same presented offer as contextual
Interact. A successful acknowledgment receipt says only that acknowledgment committed and care is
pending. Health restoration and completion of care appear after the separate helper transaction.
An explicitly selected helper retains its observed position after care completes; losing an applicable
action does not make that person disappear or silently select an opponent.

## Presentation and persistence

[FarmEncounterStatus](../../client/scripts/FarmEncounterStatus.cs) explains whether the world awaits a
choice, is resolving/recovering, has reached outcome or is explicitly paused. A successful queue receipt
means a choice was accepted, not that damage happened. [FarmActorView](../../client/scripts/Presentation/FarmActorView.cs)
reacts to a decrease in observed HP using sprite-local tint/motion and a damage label. It never changes
world position, anatomy or timing. A newly created view establishes its current HP baseline without
replaying historic damage. Incapacity dims the retained body; it does not create a corpse or remove
custody. Source appearance is explicitly bound to the admitted actor role, not inferred from its name.

[FarmHealthFeedback](../../client/scripts/Presentation/FarmHealthFeedback.cs) compares consecutive
permitted HP observations. A decrease retains the damage cue; an increase uses a brief mint tint and a
stationary signed amount without additional sprite motion. The cue describes observed HP restoration,
including a level refill, rather than asserting a medical cause or an anatomical repair. Repeated equal
observations do not restart it. Losing HP visibility discards the baseline and transient cue; the next
observation and a newly opened view establish a baseline without replaying an unobserved change. The
latest observed change replaces the earlier cue; this display is not a complete event ledger.

The explicit encounter launch opens a separate selected world. It checks the save against that
selection and preserves existing files. The durable
[lifecycle](encounter-lifecycle.md), [physical actions](encounter-actions.md) and
[pacing](world-pacing.md) owners retain all game state. UI target selection, wheel drafts and visual
feedback are transient; none are replayed as commands after reconnect or reopening.
