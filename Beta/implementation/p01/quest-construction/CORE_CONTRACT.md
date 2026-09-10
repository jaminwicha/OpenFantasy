# Funded delivery quest contract

This increment implements an actual delivery obligation, actor progression and finite reward transfer in the portable authority. It extends the retained gathering and crafting loop. It does not construct a windbreak, apply wind/crop protection, complete the NPC planning system or close the first inhabited prototype gate.

## Definitions, participation and funding

`DeliveryQuestDefinition` pins a giver, minimum level, exact original raw item, quantity, dedicated receiving custodian, admitted material reservation and `ActorRewardDefinition`. No action supplies its own reward amounts. The current imported source obligation is Wren's six wood, 420 XP and 200 gil. Ordinary gifts do not create acceptance or satisfy this commitment retroactively.

`ActorQuestState` retains acceptance and completion command identities and calendar instants. An absent participation has expected revision 0; accepted and completed records have revisions 1 and 2. Readiness is computed from currently available inventory. The quest snapshot retains every participation rather than deleting completed records. Its revision counts those transitions exactly.

Acceptance requires current control, an exact quest pin, the correct reachable giver, the participant's current presence/progression/participation revisions and a previously committed addressed conversation that remains open. The world admission has already checked the conversation's speech, hearing and language bindings. A caller-supplied matching-looking dialogue is insufficient. One actor cannot speak and submit a second action in the same selected batch.

An accepted reward is an outstanding funded obligation. For every reserve, the sum of all accepted and unpaid rewards must fit its current balance. This prevents two participants from accepting two unconditional 200-gil promises backed by only 200 gil. Tests explicitly admit enough funds and material for independent player and host-controlled participants. The current source farm admission contains one 200-gil reserve and one six-unit project; it is a single-claimant diagnostic content profile, not the production four-player economy.

## Atomic material and reward operation

`SourceDeliveryPlanner` interprets explicit bounded selections from raw source lots only. It checks current control and receiving policy before private recipient state, exact item pins, source storage and lot revisions, protected/reserved flags, selected quantities and combined raw/transformed storage capacity. Transformed outputs cannot masquerade as raw source intervals.

Full-lot transfers retain the lot ID and increment its revision. Partial transfers retain the remaining interval under its old ID and allocate the delivered prefix using the command ID and sorted selection index. Several partial lots therefore receive distinct child identities. Source intervals, total raw quantities and source admission budgets remain unchanged. The custody and each touched storage revision advance once for the whole delivery. Received material carries the admitted project reservation, so ordinary gift/craft operations cannot spend it.

`DeliveryQuestPlanner` composes this material candidate with the actual progression and funded currency candidate. The participation becomes complete in that same immutable result, releasing its outstanding reward obligation while paying its exact reserve debit. Any failing check or arithmetic overflow preserves every original component and yields no created identities. The world processor publishes this result once, together with its ordinary command receipt and identity reservations.

The source's 420 XP advances a fresh Human Squire from level 1 to level 3 with 38 residual XP, HP 78, MP 14, attack 18, magic 6, defense 12 and speed 10. Level gains refill HP/MP; source-derived details and limits are in `PROGRESSION_CONTRACT.md`. The admitted gathering policy remains fixed at gain 10. Stats do not silently change previously pinned work pacing.

## World profile and preserved-copy admission

World profile 4 adds progression and quests beside the exact exploration, gathering and production components. The composed admission joins all definition pins, actor sets, currency accounts, projects, reservations, participant causes and calendar bounds; cross-domain identity aliases fail. Old snapshot/action/step codecs retain their formats and refuse components they cannot represent. Quest actions use version 4; old action versions remain nested unchanged in a profile-4 batch.

`WorldQuestAdmission` contains only new definitions, fresh actor progression, finite currency accounts and empty project specifications. It does not serialize existing inventory or source cursors into an upgrade request. `WorldQuestMigrationProcessor` requires the exact old profile-3 head, source and target admission pins, world/branch, revision and advancing-step count. It checks every new definition/account/project/reservation identity against both current and historical occupancy. It then adds empty project storage and its policy/reservation while preserving all old inventory entries, rights, recipes, retirements, outputs, residents, dialogue, geometry, calendar and work. Only the root revision advances.

The admission hash is independent of previous gathering/crafting progress. Initial progression in an old profile-3 save is explicitly new admitted state: that profile never retained prior XP, HP/MP or gil. It is not a reconstruction of nonexistent history. No migration resets finite plants, restores consumed wood, invents rescue stock or claims that all exhausted predecessor saves can still finish the newly introduced quest.

The complete migration request is bounded to 256 KiB and its new admission to 255 KiB, fitting the retained schema-3 journal without DDL. The hosting copy/replay/publication contract and failure tests are recorded separately in `PERSISTENCE_REVIEW.md` and `HOSTING_REVIEW.md`.

## Verification and remaining work

The coordinated Debug runs passed 508 Core and 207 Hosting tests, with zero skipped cases. They include independent Python progression and quest framing vectors, malformed/truncated inputs, earlier-format fixtures, funded acceptance, source interval conservation, actual crafting competing for wood, player/host contributor parity, once-only rewards, held time, migration, historical identity conflicts, real SQLite replay/retry and the prior sidecar guards. Native/rendered and Release results are recorded only after their separate runs.

The subsequent integrated qualification added six independently authored client/save-selection regressions and passed **508 Core + 213 Hosting tests** with no skips. All 21 independent fixtures, 68 host inspections, old native regressions and the new three-process quest journey passed. The same 721 tests passed in Release. The separately rendered native run uses the exact same three Debug DLL hashes as the integrated quest run and passed independent visual review. See [the final increment evidence](README.md#executed-evidence); these later results do not expand the earlier reviews' historical scope.

The retained first-prototype checklist remains open. In particular, the six 50-gram wood units are reserved materials, not six structural cells. Construction needs a real calibrated bill, site/support/clearance/access rules, actor work, authoritative geometry and persistence. Wren's original protective aftermath line is retained as source content and withheld until its claimed physical outcome exists. NPC planning, cooperative combat, field staging, broader art/audio, accessibility and multiplayer requirements remain in the campaign plan.
