# Native trading dependency notes

This is a next-step implementation note from actual client inspection, not a native completion claim
or a replacement for the full [cargo integration plan](../spot-exchange/INTEGRATION_PLAN.md).
Build on the [accepted scheduled-trading host increment](INTEGRATION_REVIEW.md), preserving its stated limits.

## Existing entry points

- [FarmClientSession](../../../Runtime/client/scripts/FarmClientSession.cs) opens explicit gift worlds
  through `OpenGiftsAsync` and validates gift fixtures as profile 30. `Attach` currently supplies gift
  preferences to `LocalWorldSession`; it does not supply trading configuration.
- [FarmGame](../../../Runtime/client/scripts/FarmGame.cs) chooses those existing save modes and already
  manages a quick-action wheel, inventory inspection, movement suppression and host readiness.
- [MarketCircuitPolicies](../../../Runtime/src/OpenFantasy.Hosting/Content/MarketCircuitPolicies.cs)
  binds rules to admitted residents without creating stock or money. It is a content seam, not a
  production market-world factory or a complete legacy merchant import.

The native increment needs an explicit fresh market-world entry, production content composition and
trading configuration. Use admitted stock and real funding, such as the retained earned reward, and
preserve source facts. Do not reference a test fixture from production or silently upgrade an existing
gift/retained save. Lived-save migration and the full legacy import remain required later work; a fresh
native proof does not satisfy them. Reuse source-backed ordinary actions and the existing save boundary.

The production composition can start with `RestorativeJourneyAdmission.CreateEmbedded`, pass that
unplayed ordinary-care root directly to `WorldPaidExchangeAdmission.CreateFresh`, bind Wren's berry
stall, and then call `WorldMarketAdmission.CreateFresh`. The paid admission expects the ordinary-care
root; do not insert the separate profile-30 gift admission first. The existing `CareFarmBuilder`
already assigns Wren six source-counted berries and her medicine supplies. Use those admitted lots;
the test fixture's extra third person and three specially constrained lots are unnecessary for this
first native flow. These are source-derived preparation notes, not an executed admission proof.

Begin the economic journey with the existing earned quest reward funding a purchase from Wren.
Her proceeds can then fund a later purchase from the player; neither wallet needs an injected
balance. Berries used for trading and medicine are the same finite supply. Verify that contention
through ordinary actions and communicate unavailable stock instead of creating a second market bag
or replenishing it for the demonstration. The later multi-town circuit remains a separate dependency.

## Player flow and timing

Introduce one local need and one commodity. Use the contextual wheel for buy, sell/show a selected
own lot, and review offers. Keep the normal display to icon, quantity and total; provide hover/focus
inspection for fees, provenance, condition, price age and expiry. Show only relevant enabled concepts.
An inquiry does not accept a trade. The final confirmation must restate the exact inspected quote;
cancel, changed selection, stale terms or focus loss must not silently confirm or request new terms.

The session answers an inquiry only at an eligible advancing cutoff. Do not enter a world reading
hold and then wait indefinitely for an answer that cannot be selected during that hold. Define the
client's inquiry/inspection/readiness policy explicitly, and keep movement suppression separate from
world pause and command consent. Use the existing explicit background inspection API for archived
results rather than replaying history in a render callback.

The current `LocalMerchantPreference` default offer lifetime is 32 advancing steps; the berry/ration
content rules cap windows at 64. At the local session's nominal 50 ms cutoff, those correspond to
about 1.6 and 3.2 seconds of continuously advancing host time, respectively. They are bounded fixture
and authored-rule values, not qualified human reading times. A native configuration needs a deliberate,
measured offer window and inspection-time policy. Author a suitable finite window for the new world
and preserve already issued terms and existing expiry tests; do not mask the issue with auto-requote
or treat a single-player pause policy as a multiplayer solution.

## Native acceptance still required

Exercise pointer, keyboard and controller selection/confirmation/cancellation, settled visual offers,
late/stale quotes, focus changes, actual buy and sell, saved outcomes and reopen. Use project-specific
background rendering fixtures so other Godot conversations cannot invalidate controlled evidence.
Physical-device/foreground and human comprehension/quality acceptance remain distinct. Continue the
approved art direction and separate artist ownership; backend tests prove no sprite, sound or UI polish.
