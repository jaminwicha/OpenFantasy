# Retained encounter timing

The full inhabited-place loop still lacks a real Wait encounter. This increment implements its portable
canonical timing machinery before admitting a new live world interpretation. It does not substitute a
separate duel game, reuse dialogue as combat, grant HP effects through a UI, or close P02/P03 combat gates.
The core/application remain C#; the frozen TDD sections 5–6 remain authoritative.

## Required timing behavior

- Tactical time is distinct from commit order, branch calendar and cosmetic time. A selected advancing
  tactical tick is five units; each unit represents 10ms of applicable exposure. Required choices,
  validation failures and action-boundary bookkeeping advance no tactical time.
- A standard action incurs `ceil(10000 / effectiveSpeed)` readiness units for a positive speed normalized
  to 100. The immutable timing record preserves that debt. A restore, controller handoff or future
  encounter rebind cannot grant immediate readiness by resetting it.
- Ready player actors hold until their explicit choices are retained. Automatic actors use the same
  readiness/resource rules; an automated controller is not permission to attack while a required human
  choice is outstanding. Simultaneously due choices resolve in an explicit deterministic order.
- A timing choice is a pinned intent, not an executed ability. HP/MP, inventory, body condition, effects,
  rights, target reach and action costs remain their existing/future sole domain owners. The eventual
  world boundary must revalidate those owners and publish effects plus timing as one transaction.
- Complete timing archives restore exact participants, delay, pending choice identity, counters and
  clock scope. Invalid, duplicate, reordered, unknown or oversized encodings refuse. Component hash
  equality is not proof of complete world or save recovery.

## The clock mismatch that must be resolved before live binding

The existing `WorldStepProcessor` charges every advancing step 1800ms, advances all resident exposure and
calls gathering/flow with the exploration ordinal. `LiquidCellPlanner` accepts only the next 1800ms
boundary, and `WorldLiquidCellBinding` requires that boundary equal the world calendar and its revision
equal `AdvancingSteps`. A 50ms tactical step cannot be routed through those calls unchanged. Held time is
never a debt to replay on release.

Reuse `ExposureProgress` for exact duration/rate/fractional carry; do not create another generic rate
engine. The eventual world profile needs explicit process interval ownership and a persisted remainder
or equivalent schedule for work, regrowth, liquids and effects. It must select each interval once, refuse
stale proposals, and never perform 36 unearned flow/work steps merely because a combat hold ended.
An independently held wet pocket additionally requires source/capacity admission and physical storage.

## Live integration sequence and safeguards

1. Qualify this component, its canonical archive and read-only Host boundary against independent integer
   and byte calculations, including zero-duration decisions, pending choices and nonzero saved debt.
2. Join the timing state to the existing inhabited world with versioned calendar/exposure semantics,
   world identity registration, sole authenticated command receipts and journal recovery. Existing
   profiles and all saved histories retain their original meaning; no implicit migration occurs.
3. Admit a real source monster/ability and complete applicable actor state. Progression already owns
   current HP/MP, but has no damage transition. A damage owner and a consistent incapable/defeated action
   gate must precede hostile effects; zero HP cannot coexist with ordinary work/walking by accident.
   Death, remains/estate, succession and broader restoration remain their own required lifecycle proof.
4. Add legal on-field staging, contextual combat actions, conservative NPC decisions, persistent costs,
   outcome and validated release, then native camera/animation/audio. A decorative transition cannot
   substitute for legal positions or costed actions. Player/NPC parity and save/retry tests are mandatory.
5. Prove multiplayer/pocket membership, complete dependency closure, joins/merges/exits, hazards and the
   persisted 64-evaluation shared-pause continuation. The 128 tactical/512 detailed qualification limits
   are not permission to drop participants. A bounded timing component cannot claim that fallback.

## Environmental dependencies retained

Profile 21 excludes bodies/members from the entire potential wet envelope and requires each void's
vertical vent. A flooded passage still needs connected cavity/atmosphere admission, body displacement
and capacity conservation, immersed movement/footing, breath/exposure and shared condition consequences.
A material-backed sluice alone cannot establish those. A meaningful occupied-water fixture must include
water reaching an already present actor and a consequential immersion/capability threshold, not only
walking after drainage or refusing every wet move. Reactions/ice/ecology and coarse/history coupling
remain required. These owners do not prevent a first dry encounter; they must be admitted before they
can influence held participants.

## Evidence limits

This increment's archive/Host checks are component recovery, not live SQLite world admission, combat
playability, branch-wide clock composition, independent co-op, native animation or human acceptance.
No existing gate is relaxed or converted to a pass. The exact APIs and measured outcomes belong in the
packet README and timeless architecture reference after implementation has been inspected.
