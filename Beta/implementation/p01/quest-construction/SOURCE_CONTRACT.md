# Wren quest, progression and construction: exact source contract

This bounded source audit supports actual quest acceptance, six-wood delivery and 420 XP/200 gil rewards, followed by separately authored construction. The source **does not build a windbreak**, reserve a site, deposit wood with Wren, or fund rewards from an NPC purse. This note identifies those differences before implementation; it changes no runtime, predecessor file, frozen packet or existing admission.

The coordinator selected the following new-profile policies after this audit: admit the player as an actual **Human Squire**; retain Wren as a fixed **Unassigned** stat profile using the source NPC values; start both actors' personal gil balances at **zero**; separately admit one authored **200-gil quest reserve**; retain the existing fixed gathering gain **10** in both profiles three and four. These are now explicit implementation inputs, not unresolved requests to invent a Farmer class or alter gathering tempo.

## Retained records and fields

All four records below are already in the frozen [records.json](../../../Runtime/content/legacy/aldermere-first-loop/records.json). Source pointers address the unchanged [source-support/data](../../../Runtime/content/legacy/aldermere-first-loop/source-support/data/) files.

| Record | Exact source pointer | Canonical payload SHA-256 |
| --- | --- | --- |
| Wren's Saplings | `data/quests.json#/quests/44` | `bbb996bb2ca6ca4e27b942f7f40f9ea624813909e037ec86980ed8aaa4ab86cb` |
| Wren | `data/npcs.json#/npcs/116` | `f0374a9cd788d14de3c45233cf31c6b67904ed209a6a5f8d1a0b8de547dd1f30` |
| Squire | `data/classes.json#/classes/0` | `b6defa5b6c7a49d13fe3297938c41952c78b22de5ba2187138654d84b2507cf9` |
| Wood | `data/items.json#/items/12` | `613f7643e208f8da2a49c4fac27cc3671a6b6fbc1b1a0e39a7e3f0f29af0505d` |

The quest's complete explicit fields are `id=wren_orchard`, `name=Wren's Saplings`, `kind=Fetch`, `giver=farmer_wren`, `targetItem=wood`, `targetCount=6`, `rewardXp=420`, `rewardGil=200`, `minLevel=1`, and these exact lines:

- Offer: “Six wood for a windbreak and my saplings might see spring.”
- Turn-in: “The wind can howl all it likes now. Thank you kindly.”

Omitted fields resolve through `QuestDef` in [GameData.cs.txt](../../../Runtime/content/legacy/aldermere-first-loop/source-support/src/Sim/GameData.cs.txt), lines 171–195: `Main=false`; `Prerequisite`, `RewardItem`, `TargetMonster`, `TargetRegion` and `TalkNpc` are empty strings. Wren therefore has no item-reward fallback, required earlier quest, kill counter, exploration region or second recipient in this task.

Wren's other fields remain name `Wren`, tile `(103,322)`, wander radius `2`, empty `sprite`, quest list `[wren_orchard]`, the three retained flavor lines, and appearance `{race:0,skin:5,hairStyle:5,hairColor:13,outfit:8,outfitA:10,outfitB:10,acc:3}`. Omitted shop is empty. The existing opening is “Just me, the rows, and the crows. Suits me.” The source's wolves/lambs and rain/sun lines do not establish those simulated events.

Wood is source `Material`, price `6` gil. Keep its current counted item ID **`f20a24bbcf04b36ebcb90a1d04ca58c0`** and version **`c603d104fd21c40819d8dc0f6a4f5f3bc976936f7ac2c2cfe6ffefd62820614d`**. Its separately authored production calibration is 50,000 mg per unit. Neither the raw source payload hash nor that physical-material version replaces the counted-item version. The six-wood requirement is six counted units, not six cubic terrain cells.

## Acceptance and completion guards

[World.Systems.cs.txt](../../../Runtime/content/legacy/aldermere-first-loop/source-support/src/Sim/World.Systems.cs.txt), lines 73–79 and 222–312, is the authoritative preserved behavior.

`NpcInRange` requires a currently resolved NPC entity on the player's map and squared center distance at most `InteractRange²`. The constant is `3 × 32 = 96` source units. Existing target conversion is **768 subunits**, inclusive, with its already admitted realm/floor/body/occlusion rules. Original distance alone does not implement those extra checks.

`AcceptQuest` applies this order: resolve nearby NPC; require that NPC's quest list contains the requested ID; reject any existing quest-log entry, including completed; require any prerequisite completed; require current level at least `MinLevel`; create `Active` progress with `Kills=0`; emit quest-progress value zero. For Wren the prerequisite check is vacuous and minimum level is one. Acceptance does not require wood, reserve it, grant money or XP, or buy elapsed work.

`CompleteQuest` applies this order: resolve nearby NPC; require a quest-log entry; reject `Completed`; require the correct turn-in NPC (giver for Fetch); require the objective; remove required Fetch items; mark completed; add gil; optionally award one reward item or its full price in gil when it cannot fit; grant XP; emit quest-progress value minus one. Wren's empty reward-item field makes that item/fallback branch inapplicable. Minimum level/prerequisite are acceptance gates and are not checked again at turn-in.

Fetch readiness is **current `CountItem(player, "wood") >= 6`**, not cumulative gathered amount, gifted wood, earlier inventory, recipe completion or a hidden order of actions. Any legally owned six matching wood are eligible. Existing ordinary gifts to Wren cannot retrospectively become an accepted quest's delivery. A connected native test should exercise gathering/crafting, but those actions are not source quest prerequisites.

The source methods and supplemental [ServerNode.cs](../../../../src/Net/ServerNode.cs), lines 219–232, do not add an actor-alive guard, manipulation requirement, line of sight, action cost, formal reservation or revision check. The server selects the session's player, but there is no per-operation canonical request/receipt or transactional retry log. The target should use its existing authenticated actor, same-world spatial interaction and immutable command authority; a stronger valid-body/living-actor gate must be identified as an intentional correction rather than an imported source algorithm.

## Item removal and material destination

`CountItem` sums all inventory stacks with matching string item ID. `RemoveItem` prechecks the total, then consumes from the **end of the inventory list toward the beginning**, reducing/removing matching stacks until the count is met. It returns false without mutation if insufficient. `CompleteQuest` ignores its return after the immediately preceding count check; the source's single-threaded mutation order normally makes it succeed. There are no protected/favorite/heirloom/reserved-item semantics in this source removal.

The target should select explicit exact-version lot prefixes through the existing custody authority, check each lot/storage revision and protection/reservation, and preflight the entire operation before publishing. Its canonical stable selection order is an explicit target policy, not the original reverse stack order. Six selected wood must leave player custody once, with source rights preserved through transfer/reservation into the real project recipient. The source destroys them from inventory; keeping them as construction stock is the necessary authored conservation improvement. The same wood cannot also be retired by ration crafting or consumed by another build.

Delivery, quest state, reward spending/credit, XP/vitals and receipt must commit atomically. A full Wren/project storage, exhausted reward budget, stale lot, invalid target or identity collision cannot leave completed quest state, lost wood or a partial reward. A retry after reopen returns the prior outcome rather than withdrawing, rewarding or refilling twice.

## Exact progression rules

Source definitions are in [World.cs.txt](../../../Runtime/content/legacy/aldermere-first-loop/source-support/src/Sim/World.cs.txt), lines 68–90, 152–194 and 585–598, [World.Systems.cs.txt](../../../Runtime/content/legacy/aldermere-first-loop/source-support/src/Sim/World.Systems.cs.txt), lines 301–312, and [SimConstants.cs.txt](../../../Runtime/content/legacy/aldermere-first-loop/source-support/src/Sim/SimConstants.cs.txt).

XP required to advance from level `L` is `floor(sqrt(10,000 × L³))`, computed by integer square root, **not** floating-point rounding of `100 × L^1.5`. Source maximum level is **10**. This is a pinned source profile cap, not a permanent limit on the new game's other classes/profiles.

| Current level | XP to next level | Total XP from fresh level one to reach this level |
| --- | ---: | ---: |
| 1 | 100 | 0 |
| 2 | 282 | 100 |
| 3 | 519 | 382 |
| 4 | 800 | 901 |
| 5 | 1,118 | 1,701 |
| 6 | 1,469 | 2,819 |
| 7 | 1,852 | 4,288 |
| 8 | 2,262 | 6,140 |
| 9 | 2,700 | 8,402 |
| 10 | No further level | 11,102 |

The source function can calculate 3,162 for level ten, but the leveling loop never consumes that threshold. XP at the maximum remains retained and further grants add to it. No respec points, selectable stat allocations, level-based currency or automatic ability execution occur in this loop.

`GrantQuestXp` adds the **entire** reward to residual XP. While below the cap and at or above the current threshold, it subtracts that threshold, increments level, recomputes stats, sets current HP/MP to their new maxima, then emits one level-up event for that new level. Multiple level-ups are processed sequentially. A grant that crosses no threshold does not refill HP/MP. The refill changes resource counters; it does not regenerate missing body parts, change `Alive`, clear scars or rewrite body-condition provenance. Those would be additional systems.

Fresh `AddPlayer` defaults to level one, XP zero and gil zero, computes stats and starts with full HP/MP. Restored source level is clamped to 1–10 and restored XP/gil to nonnegative values, but XP is **not normalized** against the current level. Consequently a source save with excessive residual XP can level on a later zero grant. A new canonical target admission may require normalized residual XP below the cap, but must reject or explicitly migrate incompatible old data rather than pretending every legacy save already satisfied it. Source arithmetic uses ordinary signed `int` addition and lacks reward/XP overflow protection; target finite checked counters must reject overflow atomically rather than wrap or silently discard XP.

## Squire stats and source NPC defaults

The Squire record carries six base stats and six integer growth values. At level `L`, start with `base + growth × (L−1)`.

| Stat | Base | Growth per level | Level 3, Human/unequipped | Level 10, Human/unequipped |
| --- | ---: | ---: | ---: | ---: |
| Maximum HP | 60 | 9 | 78 | 141 |
| Maximum MP | 10 | 2 | 14 | 28 |
| Attack | 12 | 3 | 18 | 39 |
| Magic | 4 | 1 | 6 | 13 |
| Defense | 8 | 2 | 12 | 26 |
| Speed | 8 | 1 | 10 | 17 |

Source class `moveSpeed=4` is separate from the Speed stat and does not grow here. Its hotbar order is `strike`, `cleave`, `breaker`, `rally`, whose source unlock levels are 1, 3, 5 and 7. Retaining these metadata references does not implement the abilities or combat. `AddPlayer` requires a caller-selected valid class; Squire is the chosen native admission, not an unconditional default for every source player.

The existing farm player has an authored humanoid body and a provisional Squire sprite, but the old admission has **no species or class field**. Human/Squire therefore becomes a new, explicitly pinned profile admission while preserving that actor/body identity. It is not a species fact inferred from the old sprite. Wren's source appearance race index zero remains independent presentation provenance.

`RecomputeStats` first derives class base/growth, then adds the source race modifiers and clamps minima in order: HP≥1, MP≥0, Attack≥1, Magic≥0, Defense≥0, Speed≥1. Race modifiers in `(HP,MP,Attack,Magic,Defense,Speed)` order are Human `(0,0,0,0,0,0)`, Elf `(-8,4,0,3,0,2)`, Dwarf `(12,0,1,0,2,-2)`, Gnome `(-4,8,-2,1,0,2)`. It then adds equipped weapon and armor bonuses to Attack, Magic, Defense and maximum HP, without a second minimum clamp, and clamps current HP/MP downward to maxima. Only the level-up caller subsequently refills them. The current unequipped Human profile can preserve the exact zero-modifier result while later equipment/heritage remain explicitly pending. Do not infer species stats or comprehension from sprite palette membership.

`SpawnNpcs` instead initializes Wren with **level 1, HP/maxHP 1, Speed 1, MoveSpeed 2**. `Entity` defaults give XP/gil/MP/maxMP/Attack/Magic/Defense/ATB zero and `Alive=true`. His `ArchetypeId` comes from his empty sprite field, not Squire; `RecomputeStats` is never called for him. A shared actor schema must represent those zero stats. A fixed NPC profile derived from this code is honest; an expandable Farmer class with stronger stats would be newly authored content. Neither should masquerade as a sourced Squire class for Wren.

Fresh Human Squire plus 420 XP yields **level 3, residual XP 38, HP 78/78, MP 14/14**, with Attack 18, Magic 6, Defense 12, Speed 10. The source ATB formula `6 + Speed / 2` uses integer division, so Speed 8→10 changes gain 10→11. The current gathered-world effort profile is already pinned to gain ten. Stats admission must preserve that older mapping or introduce an explicit new rules profile; it must not silently change gathering replay, movement speed or saved readiness.

## Currency and reward allocation

Original gil is an integer field, not inventory, a coin item or a finite bank account. Wren starts with zero gil by default, yet completion adds 200 directly to the player without debiting him or a treasury. XP similarly comes from a reward rule, not an NPC XP balance. There is no repeat reward for a completed actor quest, but each source player has a separate quest log and can claim the full reward once; there is no shared world funding limit.

For the new finite simulation, the selected policy is a **separate pinned currency definition and explicit 200-gil quest reserve**, distinct from both actors' zero starting personal cash. Wallet balances remain separate from bag capacity. Funding that reserve is an authored admission delta, and must not reset existing raw/derived inventory or reinterpret the source's zero gil as an existing 200-gil balance. The selected bounded implementation uses a ledger whose account balances sum exactly to the admitted currency supply; it does not claim per-coin source-interval lineage. Currency definition, actor wallets, reserve and reward identities remain distinct from actor, item and material-lot IDs, with each personal wallet explicitly referring to its actor.

The bounded initial allocation should explicitly bind one eligible actor/quest entitlement to 420 XP and 200 gil. Completion spends that funded allocation once and credits exactly the selected actor. XP authorization is a finite reward entitlement, not physical currency conservation; transferring 200 gil must conserve currency rights. Future multiple eligible actors require corresponding funded allocations, a finite shared pool with an explicit unavailable state, or a declared different rule. There must not be an implicit 200-gil mint for every actor who acquires the quest. Funds failure must be adjudicated before wood delivery and completion.

The source has no XP-sharing or party-splitting rule for quest rewards: it calls `GrantQuestXp(player, quest.RewardXp)` directly. Monster XP/kill-credit sharing is a different path and does not justify dividing or duplicating Wren's reward among party members.

## Dialogue, construction and deliberate corrections

Supplemental [ServerNode.cs](../../../../src/Net/ServerNode.cs), lines 320–375, shows the source `TurnIn` text as soon as the objective is ready, **before** successful completion. For an actual simulated windbreak, use a truthful authored receipt/acknowledgement while stock is merely delivered, retain the original line and its source provenance, and show the completed-windbreak claim after actual construction commits. Quest delivery/reward and construction status may be separate retained facts; do not silently change the original six-wood quest into a requirement to finish an invented blueprint before any reward without declaring that design choice.

The following are deliberate target improvements, not source fidelity claims:

- Atomic selection, ownership/protection/reservation, exact revision/pin checks, and retry-safe once-only completion/rewards.
- Finite funded currency and explicit reward allocation, with checked arithmetic and no overflow mint/wrap.
- Conservation of delivered wood as actual Wren/project stock. Source fetch removal has no destination or build job.
- Known quest states and valid-body/living interaction admission. Source restoration accepts a stored progress enum without a dedicated canonical quest-state validator, and source quest methods lack a life check.
- Truthful post-commit construction narration. The original completed text neither proves a built structure nor protects saplings.
- New profile migration that preserves old progress. Supplemental source `HandleHello` discards a save when selected class changes; that slice shortcut is unsuitable for the retained new world and must not become a migration path.

General source item-reward fallback is not needed for Wren. It should remain pending rather than be accidentally generalized from `AddItem`, whose partial-stack failure behavior is already documented in [the ration source audit](../production/SOURCE_CONTRACT.md). Source class validation checks base stats and move speed, but does not validate the complete growth envelope, equipment-plus-race results or arithmetic bounds. The target's pinned progression definition must validate all admitted levels and modifiers with checked arithmetic.

The later construction design still needs an explicitly authored blueprint, site permission, actual builder, material allocation, work duration, footprint/support/clearance and resulting occupied structure/terrain. Six 50g wood units imply 300g of current calibrated stock, not an arbitrary full-size fence mass or six solid cells. Resolve physical scale/bill of materials openly; do not erase the existing calibration or create the structure's mass from nothing. Reuse the retained fence atlas motif only as a provisional presentation adaptation, not as an existing source windbreak implementation.

## Minimum retained progression/quest cases

Threshold equality and one-less cases; 420 XP from fresh Squire; a grant crossing multiple levels; no refill below threshold; full HP/MP on each crossed level; cap-ten residual retention; checked XP/gil overflow; exact class/rules pins and common NPC zero-stat representation. Reward isolation must cover different actors, duplicate/replayed completion, exhausted funding, protected/stale/insufficient wood, wrong NPC/range/realm, old gifts, and valid currently held starter or gathered wood. Migration/reopen must preserve time, existing crafting batches/retirements, source cursors, body conditions, inventories and quest/reward identity. Actual construction needs its own later material/occupancy and persisted visual proof.

This audit read source, matched canonical payload hashes and independently computed threshold/stat tables with Python integers. It ran neither game and no managed build. Supplemental current-checkout sources are identified separately from the frozen closure. The later [content admission](CONTENT_CONTRACT.md) additionally verified `Entity.cs` against the preserved original archive and retains that exact unchanged witness in the new quest content directory; the older closure remains unchanged. Its [independent admission evidence](CONTENT_VALIDATION.json) records this byte-level provenance check.

| Reviewed source file | SHA-256 |
| --- | --- |
| Frozen `classes.json` | `27c5956e9b269acdc36ecba339522c426380ea2f35bc33fead49ae778add6234` |
| Frozen `quests.json` | `3ce9eabab223fc66131a9e048a43e47c8c745d2fe6e33542e762291a74148045` |
| Frozen `npcs.json` | `a75e0f3e565e21cd751dc3697f948b773439935478053c29d5467e868d439746` |
| Frozen `World.cs.txt` | `fc55db3ec2c8a9602e0432a7359e460b8b6a6e92a4123620df4df5190eaea4e2` |
| Frozen `World.Systems.cs.txt` | `c9a9d49ca4021cded3f06c4568075cdf4956149b4736779d4ab4b7711325886e` |
| Frozen `GameData.cs.txt` | `b5c47c56d41d0c17984df96a20a7a336b450ae45c1a52a610b7d460ad0873ab8` |
| Frozen `SimConstants.cs.txt` | `7bdcec4fa6f7017bab1cb9c7ff028ee7e28b15ae37bc4503174f91b89e431742` |
| Supplemental `src/Sim/Entity.cs` | `9c24378d705934022a8f07c901a7f4ddcc2e1ee977488a956e4609da935222a3` |
| Supplemental `src/Net/ServerNode.cs` | `72119d8781707526b051447cb761dfa6cc88c4723a2b19bb8abc6dd356510074` |
