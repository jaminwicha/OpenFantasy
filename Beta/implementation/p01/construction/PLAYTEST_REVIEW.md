# First-feedback trace and interface review

The recorded construction loop has no outstanding material blocker from this bounded review. The misleading unaddressed G action is corrected and its new ordinary-input regression passes: no action was queued, and player stock, quest and progression fields remained unchanged over 305 ms and 45 observations. The subsequent gather/craft/talk/accept/deliver sequence still succeeds.

This is an independent static/trace review using the playtest-analysis workflow. Initial diagnosis used `Runtime/artifacts/construction-diagnostics/native-first.json`. Execution closure uses `qualification/headless/native-construction.json`, including `freshJourney.questPreparation.OutsideQuestAction`, and `qualification/final-verification.json`. Reviewed client sources and all three recorded client/runtime binaries match the final headless report's hashes. The integrated verifier reports 544/544 Core and 261/261 Hosting tests passing. The reviewer did not run the game, open the active rendered window, inspect screenshots or observe a human. Successful scripted routing does not establish discoverability, comfort, art acceptance or enjoyment.

## Material finding

**F01 — Closed: ordinary gift and quest delivery shared a misleading control.** The original `FarmHud` exposed “Give wood [G]” outside dialogue; after acceptance, its task status asked for six wood. The original `FarmGame.Give` fell back to `FarmClientSession.GiveWood` without an addressed offer. That method sends up to six units to Wren's primary inventory, while the quest requires six available units in the player's pack to transfer into its reserved project custody. The player's interpretation can reasonably be “I delivered the requested wood,” while the task remains incomplete and now requires replacement gathering. This is recoverable, but it obstructs a useful first-feedback session.

Owner: coordinator/client. The correction preserves ordinary gifting in the older profile and requires an addressed quest request in the quest profile. The unaddressed button is disabled and labelled “Orchard task”; a keyboard G attempt reaches the speak-first message. The new trace presses G before approaching/talking, asserts immediately that no action was queued, and continues observing while ordinary time advances. Exact inventory quantities/lots/revisions, quest fields and progression remain unchanged. This detects the old routing even though the initial actor position is outside gifting range: the old route would still queue a request. E → G → G subsequently accepts and delivers once. Source inspection establishes the message route; the trace does not record rendered message text.

## What the trace establishes

The fresh profile begins with eight wood and two real tools, level 1, zero XP/gil, no revealed quest and no disclosed project. Two ordinary F inputs gather two berries; C consumes two berries and one wood and retains two rations plus one offcut. E opens the actual offer. First G accepts without consuming wood; second G delivers six and yields the exact level 3, XP 38, HP 78/78, MP 14/14 and 200 gil result. One wood, two rations, one offcut and both tools remain.

The fresh and upgraded construction journeys both record work beginning at zero, dialogue holding partial work for at least 600 ms, interruption/recovery at 60/200, completion at exactly 200/200, collision with the guard, a valid walk around it and unchanged completed state on reopening. All seven directly reported construction/fresh-quest process entries exited successfully; focus requests were zero. These are observed state/command checks, not a simulated player's subjective reactions.

## Usable next actions for the feedback guide

1. Start with meeting Wren: WASD/arrows to approach, E to talk. G accepts the task and a second G delivers six available wood. **E / Continue closes the conversation; it does not accept the offer.**
2. Make gathering/crafting explicitly optional. The initial eight wood already covers the task. For the optional branch, collect two berries with F near the bush and press C to pack two rations using one wood. A briefly disabled gather action can be effort recovery; a disabled craft action needs its ingredients. These reasons are not explained directly by disabled buttons, so ask the tester where they hesitated before adding more UI.
3. After delivery, close the conversation and keep Wren's route and crop plot clear. He performs the work; no separate player build button is required. The HUD distinguishes paused, routing, percentage fitted and completed states. Observe whether a new tester recognizes this causal sequence without being led through it.
4. Pause with Esc, then close/reopen and check that the guard remains. If inviting gamepad feedback, supply the currently mapped controls: A talk/close, X accept/deliver, Y gather, B craft, Start pause; the visible labels currently teach keyboard bindings only.

No additional material blocker was found in this bounded review. The next useful evidence is a brief unfamiliar-person session: record the first hesitation, whether the person understands acceptance versus delivery, and whether they notice that closing dialogue releases Wren to work. Do not label the automated journey a usability pass.

## Runnable feedback instructions

`FEEDBACK.md` links to the existing executable `Play OpenFantasy.command` and the same `python3 tools/play.py` entry point from `Runtime/`. Static inspection confirms the launcher resolves its own directory, checks pinned executables, restores/builds/imports and opens the normal farm without probe-only flags. The guide distinguishes this macOS development route from exported desktop releases and documents retained saves, keyboard/controller actions, optional crafting, work interruption and unfinished presentation. This reviewer checked the launch route statically and did not execute the launcher.

The coordinator also clarified the numbered sequence: accept and deliver are separate choices; the optional gathering step explicitly requires closing dialogue with E; and the following step returns to Wren for delivery if needed. Independent reinspection confirms those instructions now follow the actual hold and input behavior. No documentation finding remains open.
