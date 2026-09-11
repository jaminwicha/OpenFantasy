# Read-only input integration review

Reviewed 2026-09-10 against project revision bc674c9. This is a retained work-packet proposal
within the existing interface plan, not an implementation milestone or reprioritization.
The worker read the shared project instructions, living interface supplement, current handoff,
installed input-handling skill and relevant client code. The lead checked the cited lines.

## Accepted findings

- `Runtime/client/scripts/FarmGame.cs:151` suppresses movement for pause, lost focus and the
  existing reading hold. General modal ownership and release-to-neutral after a context change
  are not implemented. Preserve the existing reading-hold behavior; this is not a claim that
  current dialogue fails to stop movement, or that an implemented wheel is broken.
- `FarmGame.cs:169` directly routes pause, talk, give, gather and craft. `FarmInputMap.cs:17`
  maps crafting to controller B and the following line maps pause to Escape/Start. Before new
  overlays arrive, semantic context routing must make Cancel unwind the top layer and prevent
  its input leaking into gameplay. Exact default buttons remain test candidates under the
  supplement; no binding was changed or approved by this review.
- `FarmHud.cs:26` exposes separate action callbacks, while `FarmGame.cs:203` has a separate
  submission gate. A small client-owned semantic action route can unify HUD/direct-key/context
  actions without moving authority into the UI or introducing a universal action framework.

## Bounded proposed packet

Within the existing P01 input-focus seam, qualify one client input-context owner with world,
dialogue and pause behavior. Route existing HUD and key actions through it, preserve current
host legality checks and reading holds, send neutral movement when context blocks it, and
verify fresh input on return. Prove cancel order and opening press/release consumption with
focused tests and native input checks. Ownership would cover FarmGame, FarmInputMap, FarmHud
and a small new client input component. General wheels and full management screens remain P02
work. Host/Core changes require separate evidence rather than being assumed necessary.

The actual review took about 48 seconds through the subscription login. Both synchronization
hooks passed. No runtime code was edited or executed; net credit savings are not established
by this small qualification task. Later implementation must receive its own reviewed ownership
packet and fresh context snapshot.
