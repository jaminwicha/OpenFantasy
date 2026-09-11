# Implementation progress

The latest [P01 input-focus increment](p01/input-focus/README.md) adds one semantic action route
for HUD and devices, top-layer cancellation, release handling and movement isolation across
pause/dialogue/focus. The [current qualification](p01/input-focus/qualification/final-verification.json)
passes **544 Core + 281 Hosting = 825 tests**, zero skips, 22 independent fixtures, 68 host
inspections and every retained native journey. Nine native input checks pass. The 659 runtime
inputs present at the start remained unchanged; the editor added two recorded script UIDs.
The naming scan passes. Code commit: `58b1f28`.

The playable farm still connects movement, finite gathering, ration crafting, Wren's funded
quest, routed NPC work and a persistent physical seedling guard. Existing saves continue on
verified copies. [Controls and launcher](p01/construction/FEEDBACK.md) include the current
cancel behavior. The owner has tried the prototype and described its gameplay as basic and
its graphics as poor; those experience concerns remain open. This input increment does not
change the art or establish a polished game.

Read the [shared handoff](AGENT_HANDOFF.md), [living UI supplement](PLAYER_INTERFACE_AND_NAVIGATION.md),
[construction packet](p01/construction/README.md) and [runtime setup](../Runtime/README.md).
UI-11 has partial P01 evidence; the 14 full UI acceptance cases, general wheels/management,
physical-device checks and human acceptance remain open. **All 181 requirements and the
existing phase sequence remain retained; full P00/P01/P02 and production/export/art/audio
exits are open.** Delegation usage, lead corrections and upstream skill learning are retained
in the new packet; net credit savings are not established.

The earlier [construction Debug](p01/construction/qualification/final-verification.json) and
[local Release](p01/construction/qualification/release/runs.json) results recorded 805 tests.
Those are preserved checkpoint results, not Release or export evidence for the new input
code. The older [rendered review](p01/construction/FINAL_VISUAL_REVIEW.md) remains bounded
technical evidence; it does not supersede the owner's later visual feedback.

## Preserved quest-stage checkpoint

Implementation now includes a native persistent farm with movement, dialogue, finite gathering, actual crafting and Wren's source-pinned six-wood quest through one portable C# authority. The quest atomically reserves delivered materials and pays 420 XP/200 gil from an explicit finite reserve; Human Squire progression and Wren's fixed source NPC stats are admitted separately. Existing gathered saves retain their earlier production upgrade, and progressed production saves now continue through a preserving profile-three→four copy upgrade. See the [quest source and content contract](p01/quest-construction/CONTENT_CONTRACT.md) and [independent save review](p01/quest-construction/HOSTING_REVIEW.md).

The first coordinated Debug runs passed **508 Core and 207 Hosting tests**, and an actual rendered three-process journey gathered/crafted, upgraded/accepted/delivered, then reopened the exact reward and continued moving. [Visual/input evidence](p01/quest-construction/VISUAL_REVIEW.md) distinguishes that scripted proof from human acceptance. The subsequent [final integrated Debug pass](p01/quest-construction/qualification/final-verification.json) succeeded with **508 Core + 213 Hosting = 721 tests**, zero skips, all **21 independent Python fixtures**, **68 host inspections**, native recovery/binding and all retained gameplay journeys, against **590 frozen runtime inputs**. The clean [local Release build and tests](p01/quest-construction/qualification/release/runs.json) also passed **508 Core + 213 Hosting**, zero skips; its client `ExportRelease` output is not a packaged desktop export. **P00/P01/P02 and the first-feedback-prototype goal remain open.** Physical NPC construction is the next connected work; Wait, co-op, frontier history, broader mechanics and approved art/audio still require their planned implementation and acceptance. Prior [production evidence](p01/production/README.md) remains preserved.

The latest owner naming instruction is recorded in [project instructions](../AGENTS.md): stable OpenFantasy names in source and project metadata. Historical planning filenames and the reviewed requirements are preserved. The original 181 requirements and production plan remain the scope baseline.

Start with the [current progress and next work packet](p00/PROGRESS.md), the [runtime build instructions](../Runtime/README.md), and the [P00 inventory](p00/README.md). The inventory author's initial checklist records what was pending when that packet was prepared; the coordinator progress record supplies subsequent executed evidence.
