# Encounter aftermath and finite aid: implementation contract

Status: designed dependency; not executable recovery. The current profile24 behavior and its exact
replays remain preserved. This contract implements part of the existing defeat/healing obligations;
it does not replace flight, surrender, rescue, captivity, death, succession or the full injury design.

## Evidence and conflict

`WorldEncounterActions` excludes HP0 actors from participation. A single remaining living side ends
combat; `LocalWorldSession` holds at Outcome, and `WorldEncounterLifecycleProcessor` requires a living
initiator for release. This preserves resources and identity but leaves a defeated solo player without
a continuation. Simply enabling Continue does not restore action eligibility. Passive healing, an
unrecorded reset or an invented safe teleport would violate the existing ownership contracts.

Restoring HP while the opposing wolf lives creates two living sides again.
`WorldEncounterLifecycleAdmission` requires Outcome to agree with actual living sides. Treatment must
therefore resume Active atomically in the same episode, or follow a separately proven disengagement.
A revived player cannot remain protected indefinitely beside a frozen hostile.

There is a second admission constraint: `WorldEncounterActionAdmission` requires the retained last
attack's target HP to equal its recorded post-damage HP. The lethal record says HP0. Changing that
actor to HP40 while keeping the attack record unchanged currently fails complete root admission,
even if the phase is corrected. Profile25 needs an explicit ordered resource-mutation witness: aid
supersedes the affected resource value without deleting or rewriting the real lethal attack. Preserve
the original last-resolution record and random cursor; version the consistency interpretation so an
exact later treatment can explain the current HP. Do not remove the check globally. Full durable
replay still proves authority; an internally consistent archive is not proof of authorized history.

The retained Potion record in `content/legacy/aldermere-first-loop/records.json` restores 40 HP and
0 MP; payload SHA256 is `3b7a5fdd562795e58d68a11007763b88cd25d40841c739c4fef9495161c097aa`.
The retained recipe consumes three berries. Legacy item use is self-only and refuses fallen actors.
External aid of an incapacitated actor is a deliberate new adaptation, not a recovered legacy rule.
Do not grant a high-level healing ability to a starting character to sidestep this distinction.

## Chosen bounded interpretation

1. Introduce a fresh, explicitly selected profile25. Existing profile24 saves and native encounters
   retain their original meaning. Keep the entire terrain/liquid/farm composition in the new profile.
2. The authored participants enter with a recorded voluntary care agreement. No inferred emergency
   permission or simulated unconscious consent is needed for this first binding. The agreement names
   patient, helper and permitted restorative treatment; it cannot transfer control or possessions.
3. At real player defeat, a controller-level acknowledgment pins the exact outcome and agreement.
   It is not a physical action by the incapacitated actor. Authentication and exact-retry handling are
   mandatory. Acknowledgment alone changes no HP, position, time, supply or debt.
4. A helper must exist, be willing under that agreement, be alive, have functional manipulation,
   own an available dose and be in actual same-plane reach. Use `QueryInteraction` within 256 subunits
   for the first calibrated treatment. These are new explicit adaptation values. A helper that is
   absent, incapacitated, blocked, out of reach, recovering or out of stock provides no offer.
5. The first authored fixture places the helper nearby through fresh spatial admission. Existing
   Wren is several cells away; no remote service, teleport or encounter-time walking is inferred.
   The helper is an explicit neutral side0 bystander in the complete resident/timing/debt catalogs,
   with a voluntary care role separate from combat membership. A living ally on the patient's combat
   side would keep that side alive and prevent the required defeated Outcome. Reject that incompatible
   authored composition; combatant aid/joining requires its own broader contract.
   Actual helper approach remains a subsequent behavior using ordinary movement/path legality.
6. Resolve one actual dose through the helper's own authenticated command and captured revisions.
   It restores `min(40, MaxHp - CurrentHp)` HP and no MP. Its positive readiness cost is one standard
   action, using the existing exact speed-normalized arithmetic. It requires zero starting helper
   debt and preserves patient debt. Acknowledging twice cannot schedule two treatments.
7. Publish dose retirement, HP, helper debt, treatment result and phase together. If the hostile still
   lives, resume Active in the same encounter ID with the same RNG state, elapsed tactical units,
   process carry and all other debts. Pre-defeat choices remain cancelled; no old input replays.
   Like current attack resolution, this logical boundary itself earns no exposure. Its action cost
   is paid through subsequent lawful advancing ticks, not time spent reading the offer.
8. This is restoration of an existing incapacitated living body, not resurrection. Anatomy, missing
   limbs, scars, equipment, custody, currency, experience, quests and identities remain unchanged.
   The finite supply can run out. A valid aid action offers renewed play, not a guaranteed victory
   or universal defeat escape. Broader continuation must still be implemented before release.

## Ownership and conservation

Add a bounded care owner for the pinned policy/agreement, acknowledged outcome, pending helper
command and completed administered-dose claims. Keep definitions, instances and permissions distinct.
Claims retain the exact raw source or produced output interval, original lot/revision, dose definition,
recipient, helper, cause and result identity. Retain the administered material disposition explicitly;
it is not a completed metabolism simulation or capacity-free transferable storage.

The existing production owner has no terminal consumption sink: recipes require actual output, and
every produced interval must remain live or retired into another recipe. Therefore do not silently
delete a Potion, fabricate an empty bottle with the Potion's full mass, or hide the dose in a fake
craft. Extend the complete raw/produced partition admission to include administered claims, including
source prefixes, lot history and overlap with fabrication, constructions and other sinks. Reject the
same dose being consumed, delivered, crafted or installed twice. Raw and crafted remedies must share
this contract; neither may become an unaccounted special case.

This is a production-owner change, not only a root-level afterthought. Its constructor currently
rejects missing produced intervals before world admission can run. Version the production
construction/decoding boundary to accept and validate the exact external administered claims, and
bind those claims back to the same care owner in complete admission. Combine fabrication and care
claims for overlap and surviving-lot revision checks. Do not create two independently editable ledgers
that can disagree about which dose remains spendable.

The world root owns all of these components. A planner can construct a candidate; only the complete
world transaction may publish it. Profile25 encoding and applicable component versions retain the
new state canonically. The identity index, archive byte bounds and fresh-genesis admission include
the new owner. Exhausted bounds refuse safely with a diagnostic, without losing supply or health.

## Implementation order and required proof

| Increment | Owners and exit evidence |
|---|---|
| Accountable consumption | Dose definitions/claims and canonical care state; exact raw/produced partition coverage, collision/capacity/adversarial restore tests, unchanged old bytes. No live recovery claim yet. |
| Complete authority | Profile25 root/admission, acknowledgment and aid protocols, world dispatcher and atomic phase/resource publication. Prove permission, actual body/range/stock/revision, capped HP, positive cost, unchanged RNG/debts/carry and all failed-action conservation. |
| Durable hosting/content | Source-pinned finite remedy/recipe, admitted nearby helper/agreement, exclusive storage, identity/retry/restart and pending-action recovery. Independent actual-process reconstruction plus kills immediately before/after acknowledgment and treatment. |
| Native continuation | Truthful available-aid preview and costs, explicit acknowledgment, visible helper action and observed HP. Real source defeat → aid → fresh choice in same encounter → eventual legal outcome/release; exact reopen at each boundary. |

Use a consumed crafted Potion as well as an admitted raw Potion in the proof, rather than qualifying
only a synthetic health calculation. Negative cases include no agreement, wrong principal, helper
HP0, lost manipulation, out-of-range/support changes, wrong patient, stale HP/lot/actor revision,
reserved stock, duplicate claim, wrong episode, exhausted supply and no eligible helper. No rejection
may spend a dose or grant healing. Exact retries return the original receipt without repeating aid.
Prove that a helper on a positive combat side is rejected by this bounded aftermath admission, while
the neutral helper retains its actual identity, authority, inventory and readiness across resumption.
Include a root with tampered restored HP or a forged/misordered superseding care witness; both must
fail admission/replay as applicable. The unchanged profile24 lethal-record/HP consistency must still
reject the same unsupported restoration. Follow a real aid with another actual attack to prove that
resource-lineage ordering remains valid after combat resumes.

Independently inspect conservation and the resumption boundary; full managed regression runs at the
coherent integration checkpoint. Rendered injected input and actual OS/device/human acceptance remain
distinct. Flee, broader remedies, interruption/approach, multiple helpers, hostile aid, co-op controller
handoff and full consequences remain on the campaign plan rather than being implied by this fixture.
