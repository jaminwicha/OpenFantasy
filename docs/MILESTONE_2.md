# OpenFantasy — Milestone 2: *Life in the March*

**Version:** 1.0 · **Date:** 2026-07-07 · **Builds on:** vertical slice (M1, commit `1fcc0f1`)

## Theme

M1 proved the skeleton: shared world, ATB combat, persistence. M2 makes the
world *worth inhabiting* — things to earn, people to talk to, reasons to
group, and a dungeon to descend into.

## Feature Set

### F1. Items, Inventory & Equipment
- **Item kinds:** `Consumable` (Potion, Ether, Tonic), `Weapon` (attack or
  magic bonus), `Armor` (defense/HP bonus).
- Players get a 24-slot stacking inventory and two equip slots (weapon,
  armor). Effective stats = class stats + equipment bonuses; equipment
  changes apply instantly and show in HP/damage.
- **Loot:** each monster gains a data-driven drop table; on a kill, *each
  tagger* rolls independently (same generosity rule as XP). Drops go
  straight to inventory (no ground loot — keeps net + griefing scope out).
- Consumables usable from the inventory window; healing respects max HP/MP.
- Inventory, equipment, and quest state persist in the character save.

### F2. Shops & Economy
- NPC **vendor** in Brasshollow (Marla, general store). Interact → shop
  window: buy consumables and basic equipment with gil; sell any item at
  half price. Gil finally has a sink.

### F3. Quests & NPCs
- New entity kind: **NPC** — non-combat, stands in town, name plate, interact
  with **E**.
- Data-driven **kill quests** (`quests.json`): "slay N of monster M" with
  gil/XP/item rewards. Accept and turn in by talking to the quest giver
  (Elder Bran). Kill credit counts for the tagger and any party member within
  range. Three quests ship: wisp cull → wolf pack → the Warden (boss).
- Quest log window (**J**) shows active/completed quests with progress.

### F4. Parties
- Chat commands: `/party invite <name>`, `/party accept`, `/party leave`,
  `/p <msg>` (party chat).
- Shared XP: when a tagged monster dies, every party member of a tagger
  within 20 tiles receives full XP/kill-credit (tagger rule generalized).
- Party frame under the status window: member names + HP bars.

### F5. The Hollow Mine (dungeon)
- The mine clearing now opens into a carved **interior**: tunnels and three
  chambers cut through the rock massif, gated by tougher spawns.
- New monsters: **Cave Bat** (fast, weak, swarms), **Ore Slime** (slow,
  tanky, good drops). New **boss: the Magitek Warden** — level 9, guards the
  deepest chamber, long respawn, drops the best weapon tier + quest target.
- Loot/XP scale up with depth; the Warden quest closes the M2 arc.

### F6. Presentation: day/night cycle
- Server tick drives a deterministic world clock (10 real minutes per day).
  Client tints the world through dawn/day/dusk/night via CanvasModulate.
  Pure presentation; safe zone and simulation ignore it.

## Protocol Additions

| Op | Dir | Payload |
|---|---|---|
| PlayerState | S→C (owner only) | inventory, equipment, gil, quest states — sent on change |
| Interact | C→S | npc entity id → S replies with NpcInfo (dialog, shop list, quest offers) |
| NpcInfo | S→C | npc id, dialog line, shop item ids, quest offers/turn-ins |
| ItemOp | C→S | op (buy/sell/use/equip/unequip), item id |
| QuestOp | C→S | op (accept/complete), quest id |

Party commands ride the existing Chat message (server parses `/party`, `/p`).

## Sim Rules (deterministic, server-side)

- Effective stats recomputed from (class, level, equipment) — one function,
  used by combat and snapshots.
- Drop rolls use the world RNG in entity-order (deterministic).
- Quest kill credit: taggers ∪ (party members within 640 units of the corpse).
- NPCs are entities (stable ids, in snapshots) but skip AI/combat/ATB.
- StateHash extended over inventory, equipment, gil, quest state, and party
  membership — the determinism test now covers the whole M2 surface.

## Acceptance Criteria

- [ ] Selftest green, including new cases: equipment math, loot-drop
      determinism, quest lifecycle (accept → kill-credit → complete →
      reward), party shared XP, boss present in the mine, M2 content
      validates (and broken M2 data fails readably).
- [ ] Determinism test still passes with all M2 systems active.
- [ ] Two-bot LAN smoke test still passes.
- [ ] Solo: buy a Potion, use it, equip a weapon, accept the wisp quest,
      complete it, see the reward; walk into the mine and meet new monsters.
- [ ] Character save round-trips inventory/equipment/quests.

## Out of Scope (M3+ backlog)

Trading between players, guilds, auction house, crafting, instanced dungeons,
PvP, mounts, audio score, interest management, DB persistence.
