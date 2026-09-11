# Inventory inspection integration review

The implementation lead reviewed code and actual native output locally. This is not independent
critic approval, a human usability test or proof that the broader game is feasible at scale.

| Concern | Evidence or disposition |
|---|---|
| A convenient display merges owned and protected/crafted stock or reveals another actor's goods. | The view model reads only `LocalWorldView.PlayerInventory`. Real source intervals, conserved crafting, protected stock and actor-switch/empty-pack facts check distinct display keys, exact totals and unchanged encoded authority. |
| Inspecting an item silently executes an action. | Pack controls can select details or close. Focus, hover, controller confirm and field shortcuts run against an exactly unchanged paused public view; no pending command appears. Equip/use/transfer remain unimplemented. |
| A menu becomes a second clock authority. | Opening changes only input ownership and submits neutral movement through the existing session. The screen queries the host's scheduler state. Native calendar progression, retained pause and real session reading-hold/shutdown tests verify the bounded policy. |
| Cancel activates underlying crafting or releases another pause. | B/Escape/I/View unwind the pack first. Native checks preserve the previous pause, exact inventory and prior HUD focus. Held navigation stays blocked until released. |
| Tab or D-pad navigation escapes into the covered HUD. | The HUD is hidden while covered; pack items and Close have an explicit cyclic focus chain. The native run completes the full Tab cycle and checks discrete navigation and selected details. |
| Host failure races the neutral-movement request. | The ordinary visible failure path catches refusal and uncovers the retained save/status. The change introduces no silent retry or replacement action. |
| New test fixture used an empty source interval. | Initial focused result: seven passed, one failed. `ProtectedStockNeverAcquiresAnUnrestrictedDisplayGrouping` constructed `[3,3)` by mistaking the end parameter for a count. The core correctly rejected it with `A source interval must contain positive units`. Change the fixture to `[3,6)`; all eight facts pass without altering the source contract. |
| A later rendered journey timed out before the pack tests. | Preserve the [failed report](qualification/failures/inventory-rendered-final.json), observation and images. Its SQLite save had 170 advancing steps and one harvest over a 28-second run. The [analysis](qualification/failures/inventory-travel-failure-analysis.json) identifies focus/readiness pause as consistent with the evidence, not a confirmed root cause; the old report lacked those fields. Add diagnostics, retain the same 20-second travel timeout and strict assertions. The subsequent run passed with focus true and no pending action at completion. |

The travel failure's precise external cause remains unconfirmed. A later pass demonstrates that
the current bounded journey can succeed; it does not erase that failure or qualify OS focus and
physical-device behavior. Those broader gates remain open. Do not increase a timeout, suppress
an error, mark an unrun case passed or blame the environment without evidence to manufacture a
phase exit. If the timeout recurs, use the newly retained focus/pause/final-view diagnostics before
accepting further affected changes.

The reviewed 960×540 pack has readable names/counts, fixed details and a clear host time label.
It currently uses text cards, so the icon-first design and art acceptance are not complete.
The larger-catalog focus/scroll/resize/localization/device matrix remains unrun. Descriptions
cover only admitted farm meanings; no rarity, value, weight, healing effect or hidden property
is inferred for an unfamiliar item. The scene, inspection model, native controller and host query
have separate responsibilities; there is no new runtime layer, IPC bridge or simulation authority.
