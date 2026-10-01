# Scheduled-trading review and repair result

Status: **repairs implemented; all four required checks observed terminal and accepted on one final build**.
Awaiting Astra's review of these repairs and integration. Reviewer: a fresh `claude-opus-5-5[1m]` run (Max
requested) on the preserved delivery (`codex/checkpoint-scheduled-trading-delivery-20261001T042625Z`). The
critique in the first section reviews the **input delivery only**. It was written before any source edit and
is not an independent review of my own later repairs; Astra reviews those. No nested agents, model fallback,
paid retry, installs, staging or commits. No Core, client, content, frozen-spec, predecessor or pinned older
test file was edited; every wrapper report shows 532 existing tests, 23 frozen specs, the source database
and the predecessor index unchanged.

## Pre-change critique of the input delivery

Read before editing: the original ASSIGNMENT, RESULT, LEAD_REVIEW_NOTES, INITIAL_CHECKS, the delivered
host source and tests, the delivered architecture, and the Core rules they depend on (`WorldStepBatch`,
`WorldCommitPreparation`, `WorldStepProcessor`, `WorldPaidExchangeResolver`, `WorldMarketResolver`,
`WorldMarketLocality`, `WorldPaidExchangeAdmission`, `WorldMarketAdmission`). Line numbers refer to the
delivered files. "Lead" marks a hypothesis from LEAD_REVIEW_NOTES that I rechecked; "new" marks an issue
I found independently. Each was confirmed by reading source; reproduction status is recorded under repairs.

### Lead hypotheses, rechecked against the returned source

1. **Permanent NPC request suppression (lead, confirmed by source).** `LocalTradingController.RequestOnce`
   (`LocalTradingController.cs:267-277`) stores `_lastRequests[actor]` before `Answer` applies the in-person
   gate, and `LocalTradingIdentity.Request` (`LocalTradingIdentity.cs:27-30`) keys the request only by the
   shopper's own holding, balance, units and showing sequence. `SelectShopper` asks to buy with no locality
   check (`:179-185`). A host buyer that first asks while its merchant is distant gets `Unavailable` and is
   never asked again after a real arrival; a buyer declined for missing merchant stock, or one whose offer
   closed unfilled, is likewise suppressed for the life of the session. The sell direction recovers only by
   accident: a showing that expires is re-shown under a new sequence, which perturbs the key once per showing
   lifetime (undocumented, and as frequent as every second step with a one-step showing lifetime).
2. **Held paid/market input faults the session (lead, confirmed by source).** `ValidateTradeSelection`
   (`LocalWorldSession.Trading.cs:73-80`) checks only that a paid/market owner exists. During a reading hold
   `SubmitAction` queues the command; `TickAsync` continues because a pending action exists; `SelectBatch`
   builds a `HeldInteraction` batch, and the `WorldStepBatch` constructor throws `ArgumentException` for any
   `PaidExchangeIntent`/`MarketIntent` outside `AdvancingStep` (`WorldStepBatch.cs:83-93`). That construction
   happens before the only try/catch in `SelectAdmissibleBatch` (`LocalWorldSession.cs:532`), so the
   exception escapes `TickAsync`, `RunAsync` calls `Fail` and the whole session faults.
3. **Gift/care composition is not live in profile 33 (lead, confirmed by source).** Core admits saved
   proposals in 30/32/33 (`WorldStepBatch.cs:77-81`, `SqliteWorldAuthority.MaterialProposals.cs:28`) and
   ordinary use/revocation in 26/27/28/30/32/33 (`WorldStepBatch.cs:94-98`), but the host gates name only
   profile 30 for proposals (`LocalMaterialProposalSelection.cs:17`, `LocalMaterialGiftController.cs:31,67`,
   `LocalMaterialProposalPreparation.cs:94`) and 26/27/30 for ordinary care (`LocalWorldSession.cs:239`,
   `LocalRestorativeController.cs:24`, `LocalRestorativeView.cs:40`). In a market world the player's gift
   actions are refused with a misleading boundary message, configuring a gift policy throws at session
   construction, the creation form and self-use/permission views are null, and helper aid never runs. The
   delivered architecture's arbitration list ("saved-proposal reservations and NPC gift decisions" before
   trading) therefore describes controllers that cannot act in the only profile trading runs in.

### Independent findings

4. **Restart reuses deterministic host request identities (new).** A host shopper's request identity is a
   pure function of its unchanged circumstances, and the merchant's answer identity is derived from it
   (`LocalTradingIdentity.cs:16-17`). After a stop and reopen with unchanged holdings, the new session derives
   the same answer identity, finds it occupied and `PrepareAnswer` returns null (`LocalTradingController.cs:
   328-332`), so the inquiry is `Declined` regardless of the merchant's current stock or funds, and the new
   session then suppresses it again. Restart therefore cannot restore NPC trading for unchanged circumstances.
5. **A refused host showing is never reconsidered (new).** The showing identity
   (`LocalTradingIdentity.cs:22-24`) depends only on the lot state and prior showing. A showing refused in a
   committed batch (for example when an earlier command in canonical order opens a reading hold) leaves that
   identity occupied, so the sell desire for that lot state stops permanently.
6. **The same unconstructible-batch failure exists at other boundaries (new, generalizing 2).** `TickCare`,
   `TickLifecycle` and `TickEncounter` (`LocalWorldSession.cs:403, 418, 457`) also construct their batch with
   the pending command outside any handler. Submission-time validation keeps them consistent only while the
   world does not change between admission and the cutoff; any other writer of the same authority (the public
   market, paid and proposal services, or a hold or care obligation published in between) can make the queued
   command unrepresentable for the new boundary kind and fault the session instead of refusing the request.
7. **Service bound omits most exclusions (new).** `scheduled-trading.md` ("Service and fairness policy")
   bounds service by earlier inquiries plus cutoffs reserved by valid acceptances. The code also defers an
   answer when the merchant is a reserved saved-proposal sender, a gift participant, a host shopper acting
   under its own control, the issuer reserved by a host shopper's valid acceptance, or when the first batch
   fails preview and the fallback batch omits every NPC action (`LocalWorldSession.cs:541-545`). A declined
   inquiry does not spend the merchant's opportunity, which the bound also does not say.
8. **"Unchanged" claim is too broad (new).** The architecture says older profiles and unconfigured sessions
   "select exactly the batches they selected before". In paid profile 32 (and unconfigured 33) a pending valid
   paid acceptance now reserves its issuer and strips both participants' movement (`SettlingParticipants`,
   `LocalWorldSession.cs:578-615`), and paid intents in older profiles are now refused at submission instead
   of faulting at batch construction. Both are improvements, but the claim as written is false.
9. **Inquiry expiry is effectively unreachable for review (new).** The lifetime is a fixed 32 steps, and a
   waiting inquiry is closed at once when its market is unavailable, so expiry needs the merchant to stay busy
   for 32 consecutive trading cutoffs. With the delivered fixtures that path cannot be exercised, which is
   why the original RESULT lists it as unexercised.

### Delivered properties rechecked and kept

Participant views carry no principal, session, stock basis or superseding fill and reveal the issuer revision
only under mutual reach (`PaidQuoteView.cs:165-182`); unknown and foreign quotes both give null; the archived
lookup authorizes first and reuses the existing archive replay without a second owner; an answer becomes an
offer only from its committed receipt; one acceptance identity per quote prevents silent acceptance retries;
FIFO order with "skipped keeps its place" holds for the cases it covers; and the four delivered cases and
their assertions are sound. Remaining reproduction and repair evidence is recorded below.

## Reproduction on the delivered source

Before any source change I added three session tests written only against the delivered API, built them
with the delivered source and ran them. Evidence: `qualification/build-20261001T045236Z-a5ae1` (accepted) and
`qualification/new-host-20261001T045252Z-1bbf0` (**50 passed, 3 failed**, report retained, not accepted):

- Hold (finding 2): `Paid_and_market_commands_during_a_reading_hold_are_refused_and_an_inquiry_waits_over_it`
  faulted the whole session: `InvalidOperationException: The local session has failed` with inner
  `ArgumentException: Paid spot exchange requires its explicit complete-world profile` thrown by
  `WorldStepBatch..ctor` from `SelectBatch` ← `SelectAdmissibleBatch` (`LocalWorldSession.cs:532`) ← `TickAsync`.
- Reconsideration (finding 1): `A_remote_host_buyer_asks_nothing_until_its_merchant_actually_arrives_then_buys`.
  Wren walked her own route into reach, but the remote buyer had already been answered `Unavailable` and never
  asked again: "Once in reach, the host buyer must trade within a few cutoffs."
- Gift composition (finding 3): `A_valid_gift_acceptance_reserves_the_sending_merchant_and_her_answer_follows`
  failed at the profile-33 creation form (`Preparation` was null); the gift acceptance behind it is refused by the
  profile-30-only gate.

An earlier build (`build-20261001T045212Z-93cc1`) failed only on my own missing `using` in the new test file.

## Repairs (host-only)

| Finding | Repair | Source |
|---|---|---|
| 1, 4, 5 | Host shoppers act only while `WorldMarketLocality.FindAvailable` holds where they stand (in-person perception; no merchant stock or funds read). Decisions are grouped into **attempts**: at most one showing and one inquiry each, under identities derived from the shopper's own circumstances (market, direction, holding, balance) and the attempt's starting step. A new attempt starts on a change of own holding/balance or after `LocalShopperPreference.ReconsiderAfterSteps` (default 32, bounded by the quote lifetime maximum), never while an inquiry waits or an offer is open. Attempts are session memory, so a restart's first attempt is a new decision whose answer identity cannot collide. `RequestOnce`/`_lastRequests` are removed. | `LocalTradingController.cs`, `LocalTradingIdentity.cs`, `LocalTradingPreferences.cs` |
| 2 | `ValidateTradeSelection` refuses paid/market commands before queueing unless at an advancing unheld exploration boundary (no hold, retained consequence, care obligation, encounter or non-exploration lifecycle phase). | `LocalWorldSession.Trading.cs`, new `LocalProfileBindings.cs` |
| 6 | `RefuseUnrepresentable` probes a queued player command against the cutoff's batch kind before every batch is built (main advancing/held path, Care, EncounterLifecycle, EncounterDecision) and refuses it with an ordinary `AuthorityDispatchException`; NPC-caused construction errors still fail fast as before. Consequence continuation keeps its deliberate integrity failure. | `LocalWorldSession.cs` |
| 3 | One `LocalProfileBindings` gate binds saved proposals and NPC gift preferences in profiles 30/32/33, and ordinary self-use, revocation, helper aid and the restorative view in 26/27/30/32/33. Paired 28 and older profiles keep refusing. A public constructor accepts gift preferences and trading together. | `LocalMaterialProposalSelection.cs`, `LocalMaterialGiftController.cs`, `LocalMaterialProposalPreparation.cs`, `LocalRestorativeController.cs`, `LocalRestorativeView.cs`, `LocalWorldSession.cs` |
| 9 | `LocalTradingConfiguration.InquiryLifetimeSteps` (default 32 = `LocalWorldSession.TradeInquiryLifetimeSteps`) bounds every inquiry in fixed-world advancing steps; time rules (no progress while paused or held) are unchanged. | `LocalTradingPreferences.cs`, `LocalTradingController.cs` |
| 7, 8 | Architecture corrected: complete service-bound exclusion list, accurate "unchanged" claim, reconsideration policy, decision/retry/confirmation distinction, hold/expiry/cancel/stop/restart semantics. | docs below |

No `Worlds/` file needed a change. A host shopper still submits its one acceptance of a wanted quote even
when the advisory preview predicts refusal (kept deliberately: it yields one honest durable refusal and is
never retried; documented).

## New session contrasts (actual cutoffs, SQLite authority, manual time; no selector shortcut)

`tests/OpenFantasy.Hosting.Tests/WorldExchange/Scheduled/` (helpers added to `ScheduledTradingWorld.cs`:
far-side walking, a genuine pre-session stall sale, a sender's saved offer, the player's speak, ranged journal reads):

- `ScheduledTradingReconsiderationTests` (3): an NPC **buy** after Wren's own route actually brings her within
  reach (no command at all while remote; issuance at the first cutoff in reach; fill with its own funds; no
  movement in the settling step; supply conserved; Wren works again); a declined NPC seller that stays quiet
  for exactly its interval while the player's in-person purchase legitimately **funds** Wren, then sells
  (one showing, one acceptance, one issuance in the whole journal); a **refused NPC acceptance** (the player's
  speak opens Wren's dialogue first in canonical order) that is never retried across later cutoffs or a real
  **restart**, followed by a new decision on a new quote that fills.
- `ScheduledTradingBoundaryTests` (4): held paid acceptance and market reading refused without faulting or
  publishing, while an inquiry waits over the hold with unchanged expiry, and the same commands settle once the
  dialogue closes; a **world-refused merchant issuance** (held by the player's speak) declines the inquiry with no
  quote or stock change, then a later inquiry settles; actual **inquiry expiry** while the merchant is reserved by
  the player's valid confirmation, a fresh inquiry answered, a paused inquiry that neither progresses nor expires,
  and stop discarding it across reopening; the player's offer **superseded by the host shopper's actual fill** reads
  superseded with no fill amounts, deliveries, acceptance or other-party identity in live and background views,
  the late confirmation is refused, nothing is requoted, and money and goods are conserved across all three owners.
- `ScheduledTradingCompositionTests` (2): the player's valid acceptance of Wren's saved gift reserves her as
  sender (no answer, no work in that step) and her answer follows at the next cutoff; an explicit Wren gift
  policy and merchant preference in one market session, where her own decline of the player's berry offer defers
  the waiting inquiry by one cutoff and grants nothing, and the patient's permission revocation is admitted.

## Terminal checks on the final source

All four exact wrapper commands were run serially on the same final build and each was observed to its
terminal report. No source changed after the final build; later edits were architecture Markdown only, which the
wrapper does not pin.

| Evidence (`qualification/`) | Result |
|---|---|
| `build-20261001T050301Z-cd0bd` | accepted, 7.74 s |
| `new-host-20261001T050314Z-d88e4` | accepted, **59 passed** (46 prior exchange + 4 delivered + 9 new), 19.907 s |
| `session-host-20261001T050340Z-1224d` | accepted, **136 passed** (123 `Simulation` + 13 scheduled), 48.767 s |
| `focused-host-20261001T050434Z-b86f1` | accepted, **175 passed**, 253.266 s |

Distinct passes: **287** = 175 + 136 − 24 overlapping (13 scheduled cases and 11 `Simulation` proposal cases
that the focused filter also selects); new-host is a subset of focused-host. Intermediate evidence is retained:
`build-20261001T050122Z-dbd1c` (accepted) and `new-host-20261001T050139Z-37f2b` (57 passed, 2 failed, not
accepted). Both failures were my own wrong test expectations, corrected in the tests: mutual reach arrived
one step before Wren's work tile, and a still-walking merchant made a retained observation stale
(`RevisionConflict`). No source change was needed. No assertion or threshold of any pinned test was weakened.

## Architecture updated

`scheduled-trading.md`, `world-markets.md`, `world-paid-exchange.md`, `runtime-and-authority.md`,
`presentation-and-interaction.md`, `material-proposals.md`, `ordinary-care.md`, `aftermath-care.md`, `README.md`.

## Remaining obligations (not closed here)

- `RefuseUnrepresentable` is implemented on every tick path but no test drives a boundary change between
  admission and cutoff (that needs another writer racing the session); the exercised hold refusal is the
  submission-time check. The care/lifecycle/encounter defensive refusals are therefore source-reviewed, not proved.
- In profile 33 only permission revocation and the NPC decline path are exercised. Self-use, helper aid and an
  NPC medicine offer there need an injured actor in the market fixture and are unexercised.
- A merchant **stock** change restoring a buy uses the same attempt mechanism as the proven funding change but
  is not separately exercised. Departure followed by return to reach is not separately exercised; arrival is.
- Service-bound exclusions exercised: paid-acceptance reservation (delivered contention case), saved-gift sender
  reservation and an NPC gift choice. A merchant busy as a host shopper and preview-fallback skips are documented,
  not exercised. Expiry is proven with a one-step lifetime; default-lifetime expiry under sustained contention is not.
- Paired profile 28 is deliberately not bound. No process-kill, native trading UI, multiplayer, full demand,
  cargo/escort or P02/P00–P14 gate is claimed.

Process: Read/Glob/Grep for discovery and Edit/Write for owned files only; the only Bash invocations were the
exact wrapper commands. No tool was denied. Elapsed time and cost were not measured by me.
