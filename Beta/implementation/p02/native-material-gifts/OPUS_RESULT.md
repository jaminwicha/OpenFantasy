# OPUS_RESULT: native controller gift button activation

Status: **source repair written, UNRUN.** Nothing was built, tested or launched (Read/Glob/Grep/Edit/Write only).
Worker: `claude-opus-5-5[1m]`, Max effort. The owned files are the only files changed.

## Diagnosis (supported by evidence)

Controller A is bound to both `talk` (routed) and `ui_accept` (FarmInputMap). Keyboard Enter is only `ui_accept`, which is not routed. That is why native3 keyboard passed.

1. **A press, GiftToggle focused, Inventory layer.** `_Input` ignores presses. BaseButton processes the `ui_accept` press but does not accept it. The event falls through to `_UnhandledInput`. There the router records `talk` as held and consumes the press with no action, because Inventory binds `talk` to nothing.
2. **A release.** `_Input` runs before the GUI. The router finds `talk` tracked, so FarmGame calls `SetInputAsHandled()`. GiftToggle never receives the release. Buttons use the default `ActionMode.Release`, so `Pressed` never fires and the probe times out.

The evidence matches. `controller.active.failed.json` shows only the inputs `I` and `A` in this stage, so `talk` was not held beforehand. The trace shows the press reaching GiftToggle (`Pressed:true, Accept:true, Focused:true`) and no release. FarmGame is the only `_Input`/`SetInputAsHandled` consumer in the client.

The same fault affects every native Button in the pack and wheel with controller A: items, ClosePack, gift buttons, ConfirmChoice, CloseWheel and NextWheelPage. FarmWheelDisc is unaffected because it calls `AcceptEvent()` on the press.

## Owned changes

**`FarmInputRouter.cs`**
- `FarmInputSignal.FocusedControlAccept` (init): the event is also `ui_accept` for a visible focused Control.
- Held presses now record an owner: `Dictionary<string, PressOwner{Router, Control}>`.
- In **Inventory/Wheel only**, a focused-Control accept press that the layer binds to no command (`LeavesAcceptToControl`) is recorded as Control-owned and consumed with no action. A layer command, such as a rebound Cancel, always stays router-owned.
- A release is consumed only when the router owned its press. Control-owned and untracked releases propagate. Bookkeeping still runs early in `_Input`.
- A duplicate press (a release was missed) makes already-held names router-owned. A lost popup release can therefore never let a later world press activate the current focus owner.
- `ReconcileHeldActions` makes every press held across focus return router-owned, so it still needs a fresh press.

**`FarmGame.cs`**
- `Translate` became an instance method that supplies `FocusedControlAccept`: the event matches a routed action, is `ui_accept`, and the focus owner is visible in the tree.
- The `_Input` comment now states the ownership invariant. The callbacks are otherwise unchanged.

**Unchanged by design:**
- World (A is Talk), Dialogue (A closes, the HUD Close button is focused), Encounter (Strike/Care: CarePlayProbe focuses the HUD button, and A must act once) and Paused all stay router-owned, with the release swallowed. That prevents a double activation.
- Keyboard and pointer paths are unchanged.

## Counterexamples and remaining risks

- Ownership is decided after the GUI. A future panel Control that acts on the accept **press** without `AcceptEvent()` could change the layer first. Such a Control must accept the event, as FarmWheelDisc does.
- The OptionButton popup window receives the A release, leaving a stale Control entry. The next panel press reclaims it. In the world, one A press is swallowed, with no activation. This is the same class as the existing one-press loss.
- A focused HUD button in the world keeps a stale pressed look after A. This is cosmetic and existed before.
- A controller disconnect does not reconcile held names. `talk` is tracked by name, so E and A share it. Both existed before.
- The Godot 4.6 BaseButton behaviour was inferred from the evidence and engine knowledge, not executed.

## Proposed new test file (not created: unowned)

`tests/OpenFantasy.Hosting.Tests/Client/FarmControlAcceptTests.cs`, with `A = Press(Interact) with { FocusedControlAccept = true }`:
1. Inventory or Wheel: A returns `(true, None)`; its release returns `(false, None)`. After closing the panel, A returns Speak.
2. World returns Speak and Dialogue returns CloseDialogue, each with the release consumed. With Inventory open, `Press(Cancel, Craft)` plus the accept flag returns CloseInventory and the release is consumed.
3. A in Inventory, then close the panel: the next A returns `(true, None)`, its release is consumed, and the following A returns Speak.
4. A in Inventory, then focus loss and `ReconcileHeldActions([Interact])`: the release is consumed.

## Minimal lead checks

1. Build the client and tests.
2. Run the existing router, encounter, inventory, wheel, target, care, restorative and gift-wheel managed tests, plus the new file.
3. Native: `check_native_gifts.py` in controller mode (the native4 failure), then re-run keyboard and pointer.
4. Native regressions: the controller runs of FarmInventoryProbe (A now completes item inspection), FarmWheelProbe, CarePlayProbe and Encounter.

Docs are left to the lead. `presentation-and-interaction.md` could add one sentence on held-press ownership.
