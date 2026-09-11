# Shared-desktop native qualification

11 September 2026. Follow-up to the [quick-wheel increment](../quick-wheel/README.md).
That packet remains an immutable record of its partial qualification and failed attempts.
This packet records the subsequent implementation and evidence; it does not close a full phase.

## Controlled rendering and normal gameplay

Other conversations can run editors and games on the same desktop. Separate repositories and
disposable saves do not isolate window focus, CPU or memory. An ordinary rendered journey can
correctly lose readiness when another window takes focus; headless execution avoids desktop
focus dependence but not resource contention.

The wheel and action-offer runners now accept `--rendered --background-render`. Bootstrap
allows this only for their explicit disposable-save invocations. The fixture requests a
non-focusable window and detaches native focus signals from its readiness. Deliberate fixture
focus changes still call the ordinary cancellation/readiness handlers. Default gameplay and
headless probes retain the actual Window signal bindings. Cleanup disconnects only the
connections it made. No Core, Hosting, save, clock, action-rejection or IPC contract changed.

Reports distinguish the requested background mode, actual window flag, actual native focus,
logical gameplay focus and binding mode. The wheel explicitly injects a raw window notification
to check that it cannot escape this fixture boundary, then verifies that deliberate fixture
focus loss still cancels the wheel. This is adapter evidence, not a physical OS focus test.
On this macOS run the non-focusable window still reported native focus at capture; the flag
alone is insufficient evidence of isolation. Initial activation may still take focus. There is
no repeated focus-grab loop and no other task's application was closed or controlled.

The audit also found older gathering, production, quest and construction probes that could
reclaim focus up to eight times after an interruption. Those loops and their counters are
removed. Their reports now record zero requests and their runners require zero. Readiness
continues to follow the normal window; rendered runs of those older probes require a coordinated
foreground interval. Their default headless checks remain appropriate on the shared desktop.

Action and pack screenshots wait for `FramePostDraw` before reading the viewport. This corrects
the previously retained dialogue-labeled image that actually showed the pack. Native journey
assertions remain necessary alongside visual inspection; a filename cannot certify its pixels.

## Qualification record

The [summary](qualification/verification-summary.json) indexes the final evidence and exact
source/binary hashes. The integrated run passed 544 Core and 317 Hosting tests, all 22 independent
fixture generators and 68 host inspections, but failed on Godot's recurring editor-startup
`EditorSettings not instantiated yet` error for the Android SDK path. Its zero process exit did
not override that failure. No root cause or permanent engine fix is claimed.

The remaining native checks are run separately against unchanged source, with the same commands
and deadlines used by the integrated verifier. The failed integrated report remains failed;
combined qualification must not be described as one uninterrupted verifier pass. Verification
timeouts now retain partial stdout/stderr and the exact expired bound; their outcome remains failure.

After that separate pass, the four legacy refocus loops were removed and only their affected
native journeys were repeated. The rebuild changed only the client assembly; the five tested
non-client assemblies remain byte-identical. Production sources, wheel/offer probes and their
rendered behavior did not change in this final fixture cleanup. The summary records both sets
of inputs and this evidence boundary; the earlier 861 tests are not claimed as newly rerun tests.

The two background journeys verify wheel input/inspection/confirmation, one real harvest, exact
save/view reopening, action offers, targets, crafting, pack inspection and dialogue. They retain
four wheel and twelve offer/pack frames. Local visual review checks the actual dialogue panel,
pack inspection and action/wheel states. These remain provisional art and bounded farm interfaces.

One intermediate background offer run passed its gameplay assertions but failed on teardown:
it attempted to disconnect focus signals that the fixture had never connected. Conditional
cleanup corrects that error; the original failed report is retained. The earlier flag-only wheel
experiment is retained as insufficient isolation evidence, despite passing its own assertions.

## Review and limits

Local Astra review checks the strict opt-in boundary, default native bindings, symmetric cleanup,
same cancellation handlers, actual render fence, exact host-issued actions and unchanged deadlines.
Normal native focus cancellation and controlled background rendering require separate evidence.
No test is skipped because another conversation might interfere, and an interruption is never a pass.
This review does not substitute for required independent critics or human experience acceptance.

All 181 baseline requirements, all 14 full UI cases and all full phase/production gates remain.
Real OS focus switching, physical devices, other desktop exports, co-op, accessibility and user
acceptance need their specified proofs. The owner's concerns about basic gameplay and graphics
remain open. Current wheel/pack evidence does not accept production art or the full interface.

## Persistent upstream doctrine

Ecosystem commit `eb7a836` updates `GameDevAIEcosystem/AGENTS.md`, the verification skill and
the input-handling overlay. The maintained local synchronizer verified five installed files
across `.agents`, Codex and Claude skill directories, retaining a receipt and backups. No model
call or external-agent delegation was used. Project instructions carry the same owner steering.

Use headless checks for suitable behavior, explicit background fixtures for controlled rendered
evidence, and a coordinated foreground interval for OS/device acceptance. Preserve interruptions,
observed readiness/focus and original assertions. Do not close other tasks, fight focus, extend
deadlines or attribute every failure to contention. Continue useful independent work while a
foreground test is unavailable.
