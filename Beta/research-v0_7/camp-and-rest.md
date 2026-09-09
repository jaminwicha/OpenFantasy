# Camps, rest and daily pacing

## Reference lessons

D&D's 2024 rules distinguish extended rest from brief recovery, include sleep and light activity such as watch, and define interruption and limits on repeating a full rest. The transferable lesson is that recovery needs duration, eligibility and interruption rules. Beta need not inherit the exact hours, recharge values or action economy.[^dnd]

Sabotage's pre-release developer update explicitly connected campsite access to cooking and supplies used between encounters. It described world-map camping and designated dungeon camping spots. This is historical design evidence, not an audit of every shipping version. It supports treating preparation and recovery locations as part of expedition pacing rather than isolated menu functions.[^sos]

Larian's Patch 6 notes include new camp behaviors, interactions between companion animals, camp-ambush placement fixes and a summoned companion's return-to-camp correction. These illustrate the need for authored life, encounters and durable companion lifecycle at camp; the notes do not validate Beta's clock model or establish an ideal day length.[^bg3]

## Beta pacing decision

Use approximately 40 minutes for a full day/night cycle as the initial tuning hypothesis. This means authoritative exploration time, not wall time spent with a paused solo game. The technical profile converts that progression to the world calendar. Time spent choosing during a local Wait encounter follows the explicit encounter suspension policy; independent surroundings can continue under the disclosed co-op abstraction.

Forty minutes is not yet a proven rhythm. Day/night ratio, fatigue onset, normal exertion, species needs, meal preparation, short respite and full-rest recovery must be tuned together. A character should have legible warnings and options, rather than abruptly falling asleep because a countdown expired. Measure uninterrupted outing length and camp frequency directly. A mathematically long day can still feel short if chores repeatedly consume it.

Camps are places in the world: roadside clearings, sheltered ruins, caravan rings, inns, stable yards, ship berths and more specialized sites. Safety, shelter, ownership, fuel, water, animal space, terrain and local relationships affect their use. A camp cannot erase pursuit, weather, a siege or a known hazard simply by opening a rest interface.

Assign actual activities: sleeping, watch, cooking, feeding/caring for companions, tending injuries, repair, low-intensity research, conversation and ritual where supported. A person cannot perform a full watch, manufacture goods and receive full sleep credit for the same interval. Care and production reserve resources and labor; interruption preserves accounted progress and valid partial recovery.

Solo or an agreeing affected group can accelerate through scheduled rest. The host processes consequential events in order and stops acceleration at a meaningful interruption. One player's decision cannot silently advance another's workshop deadline, journey or battle by a night. Without agreement, normal-time rest remains possible while the player controls an authorized consenting companion, or performs a different camp activity that does not count as sleep. Short respite offers bounded recovery without refreshing full-rest entitlements repeatedly.

Camp events use the same contextual encounter/adventurelet system. Night watches, weather, visitors, animal behavior, rumors, relationship scenes, theft or an ambush must satisfy real eligibility. Not every rest needs an encounter or dramatic conversation. Preserve quiet and respite; a constant ambush tax would undermine the purpose of camping.

## Retained tests and acceptance

| ID | Evidence to collect |
|---|---|
| REST-01 | Day/calendar mapping, solo pause, held encounter and restart agree; no wall-time fatigue accrues through a paused host |
| REST-02 | Accelerated group rest processes scheduled events and stops at an interruption; a refusing/unavailable affected controller is not silently time-skipped |
| REST-03 | Interrupted sleep retains valid partial credit and supplies; repeated start/reload does not refresh full-rest charges or heal for free |
| REST-04 | Watches, cooking, research, pet care and crew duties cannot double-book the same person's sleep/work interval; delegation preserves costs and knowledge |
| REST-05 | Several campsite families and a caravan/ship camp retain geometry, supplies, companions, threats, weather and relationships after departure/restart |
| REST-06 | Actual human and virtual-build outings compare the initial cycle with alternatives: camp frequency, idle waiting, recovery exploit attempts, useful camp choices and perceived interruption burden |

These are design/test specifications. No measured rest-loop or 40-minute-cycle enjoyment result exists yet. Full camp functionality completes through the phased plan; its multiplayer clock and recovery ownership are tested earlier to prevent later redesign.

## Sources

[^dnd]: Wizards of the Coast, [*D&D 2024 Basic Rules, Rules Glossary: Long Rest*](https://www.dndbeyond.com/sources/dnd/br-2024/rules-glossary#LongRest), 2024 rules edition.
[^sos]: Sabotage Studio, [*Sea of Stars developer updates: Campfire Cooking*](https://sea-of-stars.backerkit.com/hosted_preorders/project_updates?page=7), pre-release developer update archived on BackerKit.
[^bg3]: Larian Studios, [*Baldur's Gate 3 Patch 6*](https://baldursgate3.game/news/patch-6-now-live_108), February 2024, camp behavior and lifecycle fixes.
