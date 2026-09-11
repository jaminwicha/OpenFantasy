# Implementation progress

The latest [P01 action-preview increment](p01/action-offers/README.md), code commit `3ae7855`,
adds immutable offers for the existing farm actions. Buttons and hotkeys submit the displayed
target, recipe/gift/quest operation and revisions without silently selecting replacements.
Visible costs and hover/focus details explain available and blocked actions. Same-offer retries
reuse their outcome, and offers cannot transfer to another client instance reopening a save.

The [current integrated qualification](p01/action-offers/qualification/final-verification.json)
passes **544 Core + 288 Hosting = 832 tests**, zero skips, 22 independent fixtures, 68 host
inspections and every retained native journey. All 669 runtime inputs remained unchanged and
the naming scan passed. Nine native input checks and six offer checks pass. The independent
[review and corrections](p01/action-offers/REVIEW.md), inspected rendered states, preserved
probe failures and final results are retained together. The prior [input-focus checkpoint](p01/input-focus/README.md)
recorded 825 tests; its cancellation and held-control behavior remains covered.

The playable farm connects movement, finite gathering, ration crafting, Wren's funded quest,
routed NPC work and a persistent physical seedling guard. Existing saves continue on verified
copies. [Controls and launcher](p01/construction/FEEDBACK.md) remain available. The owner tried
the prototype and described its gameplay as basic and graphics as poor; those concerns remain
open. This increment improves action clarity and safety, without claiming new art or polished
full-game interfaces. A world change can still reject a displayed offer and require a new choice.

Read the [shared handoff](AGENT_HANDOFF.md), [living UI supplement](PLAYER_INTERFACE_AND_NAVIGATION.md),
[construction packet](p01/construction/README.md) and [runtime setup](../Runtime/README.md).
UI-01/UI-03/UI-11 have partial P01 evidence; textual inspection is a prerequisite to UI-15.
All 14 full UI acceptance cases, wheels/management screens, physical-device checks and human
acceptance remain open. **All 181 requirements and the existing phase sequence remain retained;
full P00/P01/P02 and production/export/art/audio exits are open.**

Owner steering now keeps Astra working directly by default. Each optional Fable assignment
requires explicit approval first, after its concrete task, expected benefit and usage limit are
ready. Earlier authorization does not carry forward. [Measured delegation evidence](p01/action-offers/delegation.json)
records material integration corrections and unverified credit savings. This policy is in the
project, shared skills, ecosystem and both global entrypoints. Follow the [Git milestone doctrine](GIT_MILESTONES.md)
for incremental commits and preserved qualified branches.

The earlier [construction Debug](p01/construction/qualification/final-verification.json) and
[local Release](p01/construction/qualification/release/runs.json) results recorded 805 tests.
Those are preserved checkpoint results, not Release or export evidence for the new client
code. The older [rendered review](p01/construction/FINAL_VISUAL_REVIEW.md) does not supersede
the owner's later visual feedback.

## Preserved quest-stage checkpoint

Implementation now includes a native persistent farm with movement, dialogue, finite gathering, actual crafting and Wren's source-pinned six-wood quest through one portable C# authority. The quest atomically reserves delivered materials and pays 420 XP/200 gil from an explicit finite reserve; Human Squire progression and Wren's fixed source NPC stats are admitted separately. Existing gathered saves retain their earlier production upgrade, and progressed production saves now continue through a preserving profile-three→four copy upgrade. See the [quest source and content contract](p01/quest-construction/CONTENT_CONTRACT.md) and [independent save review](p01/quest-construction/HOSTING_REVIEW.md).

The first coordinated Debug runs passed **508 Core and 207 Hosting tests**, and an actual rendered three-process journey gathered/crafted, upgraded/accepted/delivered, then reopened the exact reward and continued moving. [Visual/input evidence](p01/quest-construction/VISUAL_REVIEW.md) distinguishes that scripted proof from human acceptance. The subsequent [final integrated Debug pass](p01/quest-construction/qualification/final-verification.json) succeeded with **508 Core + 213 Hosting = 721 tests**, zero skips, all **21 independent Python fixtures**, **68 host inspections**, native recovery/binding and all retained gameplay journeys, against **590 frozen runtime inputs**. The clean [local Release build and tests](p01/quest-construction/qualification/release/runs.json) also passed **508 Core + 213 Hosting**, zero skips; its client `ExportRelease` output is not a packaged desktop export. **P00/P01/P02 and the first-feedback-prototype goal remain open.** Physical NPC construction is the next connected work; Wait, co-op, frontier history, broader mechanics and approved art/audio still require their planned implementation and acceptance. Prior [production evidence](p01/production/README.md) remains preserved.

The latest owner naming instruction is recorded in [project instructions](../AGENTS.md): stable OpenFantasy names in source and project metadata. Historical planning filenames and the reviewed requirements are preserved. The original 181 requirements and production plan remain the scope baseline.

Start with the [current progress and next work packet](p00/PROGRESS.md), the [runtime build instructions](../Runtime/README.md), and the [P00 inventory](p00/README.md). The inventory author's initial checklist records what was pending when that packet was prepared; the coordinator progress record supplies subsequent executed evidence.
