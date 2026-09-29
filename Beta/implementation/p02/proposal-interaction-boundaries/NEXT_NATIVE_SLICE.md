# Next native gift slice

Read-only explorer mapping from the existing client; no native implementation or acceptance is claimed.
The Host creation/inspection boundaries are in this packet. Build one coherent player interaction next.

- Add an explicit profile30 saved-origin opening route in `FarmClientSession`, `FarmGame`, `Bootstrap`
  and launch tooling. Preserve profile27's exact fixture validation and older restorative routes.
  Never upgrade or relabel the retained personal-dose save. Reopening must supply the same explicit
  `LocalMaterialGiftPolicy` advice; policy is not recovered automatically from the canonical world.
- Select a physical lot and positive quantity in the pack, a named recipient, and an explicit offer
  row for replies. Existing inventory groups can combine lots and are not commitment identities.
  Retain exact selection through refresh; disappearance becomes unavailable, never first-row fallback.
- Use `LocalWorldView.MaterialProposals.Preparation` to prepare the intent. Do not inspect authority
  or infer full item definitions, recipient storage or a private advancing count from client state.
  Preserve captured expiry through confirmation. The running wheel does not pause Host time: comparing
  a captured creation intent with a freshly regenerated expiry would immediately disable it. Qualify
  a precise current-eligibility comparison that checks the frozen terms and remaining lifetime without
  silently replacing expiry. A stale captured command must still receive the actual authority outcome.
- Add separate semantic Create/Accept/Decline/Withdraw actions and a stable four-slot Gifts page.
  Existing quest Give remains its own concept. Generalize the current binary Actions/Care page labels
  and next-page hints. Add icons for all actions (the disc indexes its icon table directly).
- Route through `FarmClientSession.CreateOffers` / `SubmitOffer`; retain foreign-client rejection,
  captured scope, one command identity and no automatic retry. Freeze complete offer identity/grant/
  terms/presence and creation selection while a wheel is open. Inspect/cancel never submits.
- `MaterialProposalView.PrepareAcceptance()` pins the grant/terms/observed sender. Incoming open offers
  allow accept/decline; outgoing open offers allow withdrawal. Terminal rows remain inspectable.
  State plainly that creation reserves nothing and acceptance transfers stock without healing or
  granting treatment permission. Show named partner, exact quantity/item, expiry and terminal status.
- Explicit historical inspection may call `LocalWorldSession.InspectMaterialProposalAsync` once per
  user request; do not call it from frame refresh. It can delay the serialized simulation while replay
  runs. The client must surface pending/failure and preserve input ownership without resubmission.

Relevant managed seams: `RestorativeActionOfferTests`, `FarmInventoryTests`, `FarmQuickWheelTests`,
`FarmInputRouterTests`. New engine-free client files must be explicitly linked by the Hosting test
project. Native route should follow retained `RestorativePlayProbe` controls/deadlines, not edit old
probes: actual rendered input, cancellation/focus, paused inspection, exact public state and separate
process reopening. Background controlled input is distinct from physical device/OS/human acceptance.

Source explorer: native Astra `/root/gift_client_route`; read-only, no build/test/window/external work.
