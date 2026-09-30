# Engine-free gift selection and wheel models

Native Astra bounded implementation; the integration lead owns execution, architecture updates,
Git checkpoints and acceptance. No external model request, test/build process or Git mutation was
started by this worker. Unrelated predecessor staging and fixture sources are untouched.

## Delivered boundary

- `FarmGiftSelection` requires explicit physical lot kind/identity, named recipient and positive
  quantity. Selection and preparation publish nothing. Missing choices retain their identities;
  no inventory grouping or alternate lot supplies a fallback. `PreparedGift` stays captured across
  frame refresh, and only explicit preparation captures new expiry. Existing offer selection retains
  its exact public `MaterialProposalView`, including acceptance grant/terms/sender observation.
- `MaterialProposalActionOffers` contributes four separate semantics whenever proposal capability
  is present: ProposeMaterial, AcceptMaterial, DeclineMaterial and WithdrawMaterial. Quest Give stays
  separate. Missing, incompatible, held, changed and terminal choices remain inspectable with blockers.
  Text identifies item quantity, named partner, absolute advancing-step expiry and status, and states
  no reservation, no healing and no care permission from acceptance.
- `LocalMaterialProposalPreparation.CanStillPropose` compares the entire current terms value against
  a captured creation and verifies its original expiry still survives the next creation step. It
  derives that boundary from its own published expiry minus the declared lifetime; the client does
  not read or infer the private world count and the check does not mutate or retain a world root.
- At the integration lead's explicit direction the advisory creation lifetime is 1,200 advancing
  steps, allowing approximately one nominal real-time minute at 20 cutoffs/second for deliberate
  wheel inspection. Holds spend no advancing time. This changes neither Core's lifetime maximum
  nor world timing. The generic NPC policy default remains outside this worker's scope.
- The wheel keeps Actions/Care order and cardinal slots and appends Gifts in Create/Accept/Decline/
  Withdraw order. PageName and NextPageName use stored page names. Gift slots freeze the complete
  actual intent and acting-player presence revision. Accept additionally pins sender observation
  through its intent. Per lead clarification, decline/withdraw continue to name their immutable
  proposal identity when the sender moves; irrelevant sender observation does not block closure.
- Gift actions are dispatchable only in ordinary World input. Inventory, wheel inspection, pause,
  hold, pending input and encounter ownership do not dispatch them directly.

## API for native integration

`FarmGiftSelection`: nullable `LotId`, `LotKind`, `RecipientActorId`, `PreparedGift`, `SelectedOffer`;
`Quantity` initially one. Boolean methods `SelectLot(view, kind, id)`, `SelectRecipient(view, id)`,
`SetQuantity(quantity)`, `Prepare(view)`, `SelectOffer(view, id)`. `ClearLot`, `ClearRecipient` and
`ClearOffer` serve explicit placeholder choices. Invalid zero quantity clears preparation while
retaining the previous positive quantity. Do not invoke `Prepare` or `SelectOffer` during refresh.

`FarmActionOffers.Create(view, selectedTarget = null, giftSelection = null)` accepts shared selection.
`MaterialProposalActionOffers.ItemName` and `ActorName` read only public descriptions for UI reuse.

## Verification to run by the lead

New `MaterialGiftSelectionTests` and `MaterialGiftWheelTests` reuse disposable copies of the existing
finite `MaterialProposalWorldFixture`. They cover explicit raw and actually transferred produced
stock, no fallback after consumption, complete pin comparisons, expiry across real advancing steps,
incoming/outgoing terminal rows, unchanged wheel expiry, exact acceptance observation, actor-revision
and changed-selection freeze, reading holds/input ownership and older-profile omission. A short
explicit expiry proves the expiration boundary without grinding through 1,200 ticks.

The test project explicitly links both new engine-free client source files. Source review and
`git diff --check` completed locally; builds/tests remain unrun here to avoid contending with the
lead's integrated checks. These tests are proposed evidence until the lead records their results.
