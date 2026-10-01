# Encounter outcome continuation: preparation for the next combat integration

Status: source-based design investigation, not implemented, assigned or accepted. The
[road-threat milestone](../road-threat-corrections/lead-integration/INTEGRATION_REVIEW.md) now proves
both terminal Outcomes and the real reading/care contrasts under the existing law. This investigation
changes none of those contracts or gates; choose and qualify a versioned rule before implementation.

## What the source establishes

`WorldEncounterLifecycleProcessor.ValidateControl` requires the requesting actor to have positive HP.
`EvaluateRelease` then requires that actor to be the scenario initiator, with the exact current episode
and final action hash. `LocalEncounterLifecycleView` mirrors that same initiator-only eligibility.
Therefore a player defeating an NPC initiator cannot obtain release: the winner is unauthorized and the
initiator cannot cooperate. This is a permission dead end, not a missing button.

`EncounterLifecycleSnapshot.Release` itself has no initiator dependency. It moves Outcome to Exploration
while retaining scenario/action archive, complete debt ledger and all counters. The world processor ends
pacing and reconstructs the complete root. Neither operation heals, teleports, pays rewards or removes
bodies. Those independent effects must remain independent.

HP zero currently establishes incapacity for voluntary action. It does not by itself establish the full
death/life-state transition required by the design. Ordinary restorative use refuses an incapacitated
recipient; the separately admitted aftermath-care contract can administer its specifically authorized
remedy after acknowledgment. Treating these as interchangeable would bypass their distinct permission,
stock, reach and boundary requirements. A rescue must be a real actor/resource process, never a free
release-time heal.

## Alternatives to qualify

| Option | Benefit | Obligation or risk |
| --- | --- | --- |
| Allow any living positive-side participant to release a completed encounter | Small explicit command change; retains current receipt and Continue interaction | Define whether surviving host opponents release automatically and how human readiness/aftermath obligations are retained; zero-survivor case still needs a rule |
| Let world authority finalize a resolved episode at a zero-duration boundary | Teardown does not depend on which actor initiated or survived; covers zero survivors | Separate saved outcome acknowledgment/reading and pending care from world-clock readiness; no automatic advance through a human decision, and no hidden gameplay effects |
| Grant a living nonparticipant a release right | Could accommodate an actual rescuer | Too broad as a default; rescue needs an independently admitted relationship/action and cannot be invented to unlock the encounter |

Preferred architectural direction: separate world-owned encounter completion from actor-owned aftermath
actions and client reading. Prototype an explicit surviving-participant release if it is the smallest
lawful step toward that separation; do not preserve initiator dependence as a permanent design. Decide
from a concrete single-player plus two-participant timing fixture, not from the convenience of the
current view model. The fresh rule/profile choice and exact acceptance contract remain unapproved design
work until the current evidence is reviewed and the implementation assignment is made concrete.

## Contract to preserve in any implementation

- An episode is resolved by actual combatant/body/resource facts. A client cannot declare victory,
  replay an old action hash, release Active combat or use another actor's control.
- Teardown retains every actor, HP/MP, readiness debt, custody/source interval, position, exposure,
  scenario/episode identity and last outcome. A dead or incapacitated actor cannot be silently erased.
- Reading/acknowledgment, world readiness, rescue/care permission and effect commitment remain distinct.
  Acknowledging a result is not a heal, transfer, reward or resumption of another actor's choice.
- All actual outcomes need a clear product continuation: victory returns to lawful exploration;
  incapacity offers only genuinely available help/rescue or an explicit terminal/game-over choice.
  The full design also retains later death, corpse, ghost, succession and nonduplicating return gates.
- Source XP/gil/drops remain unexecuted until their own once-per-episode authorized issuance, finite
  custody and source-provenance contracts are implemented. Returning to exploration does not qualify them.
- Preserve every accepted old profile's rule interpretation and bytes. A changed release law must be
  explicitly versioned; inspect profile dispatch, codecs, admission, storage recovery and native selection
  before choosing whether a new profile suffices. Do not rewrite old accepted recordings to the new law.

## Useful vertical proof

Use the original NPC-initiated scenario, actual original stats and ordinary earned capabilities for a
playable win where feasible; label any fresh stronger-character calibration separately from production
balance. Prove the opposite living winner, both incapacitated, side-zero bystander, forged control, stale
hash, conflicting retry, pending care and readiness contrasts. Save at Outcome and before/after teardown;
recover the complete journal and continue without rerolling identity, damage, time or possessions.
A schema/owner change additionally needs its real codec/malformed/continuation/archive obligations.

The native player must see an accurate result, inspect it without simulation leakage, choose a lawful
continuation and move/use the retained inventory afterward. Include a loss and an unavailable-rescue
message; a disabled Continue with no explanation is not a production continuation. Distinguish synthetic
input from physical controls and human acceptance. P02 still requires the much broader inhabited,
networked, artistic and causal loop; this closes only one necessary combat dependency.
