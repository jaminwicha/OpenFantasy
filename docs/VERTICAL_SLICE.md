# OpenFantasy — Vertical Slice Plan

**Slice name:** *Verdance March* · **Target:** playable multiplayer proof

## Goal

Prove the four pillars in one build: a shared persistent 2D world with
FF6-inspired presentation, real-time ATB combat against AI monsters,
multiple simultaneous players, and a deterministic server-authoritative sim.

## Proof Points

1. **World** — one seamless 128×128-tile region (town → meadow → forest →
   mine mouth) rendered from data; collision honored; safe zone works.
2. **MMO** — a headless server accepts N clients; players see each other
   move, fight, chat, and level in real time; characters persist across
   reconnects.
3. **Combat** — ATB-gated abilities per class; 3 monster archetypes with
   aggro/leash AI; XP/levels/ability unlocks; death + respawn at crystal.
4. **Determinism** — self-test proves same seed + same inputs ⇒ same state
   hash over 1000 ticks; server logs periodic state hashes.
5. **Aesthetic** — FF6-flavored UI panels, 16-bit sprite/tile look, damage
   numbers, name plates.

## Acceptance Criteria

- [ ] `dotnet build` succeeds; Godot project imports and runs.
- [ ] `--selftest` passes (determinism, combat math, data validation, XP).
- [ ] Solo mode: create character, walk the full region, kill each monster
      type, level to 3, unlock an ability, die, respawn, reconnect with
      progress intact.
- [ ] Two clients on LAN: see each other, chat, co-kill a monster, both get XP.
- [ ] Server tick stays under budget with 64 mobs (log timing).
- [ ] All content files pass schema validation; a deliberately broken file
      fails with a readable error.

## Milestones

| # | Milestone | Exit criterion |
|---|---|---|
| M1 | Spec + scaffold | docs merged; project builds empty |
| M2 | Sim core | selftest green (determinism, combat, XP) |
| M3 | Netcode | 2 clients sync movement + chat on LAN |
| M4 | World & content | full region + 3 monsters + validation |
| M5 | Client presentation | FF6-style HUD, sprites, damage numbers |
| M6 | Persistence + polish | reconnect persistence; acceptance list green |

## Cut Lines (if time pressure)

Cut in order: level-up ability unlocks → Rust Golem elite → persistence
autosave (keep disconnect save) → chat system messages. Never cut: pillars.

## Post-Slice Backlog

Interest management, delta snapshots, parties, quests, shops/economy, audio
score, hand-drawn art pass, DB persistence, more regions, PvP flagging.
