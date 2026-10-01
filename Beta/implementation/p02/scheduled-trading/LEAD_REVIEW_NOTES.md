# Preliminary integration questions

These observations concern an in-progress draft, not the final delivery. Recheck the returned source
before treating any as a defect. The worker retains its assigned ownership until terminal delivery.

1. `LocalTradingController.RequestOnce` records `_lastRequests` before `Answer` decides locality.
   Its request identity depends only on own holdings, balance and showing sequence. In this draft,
   an NPC shopper that first asks while remote receives `Unavailable`; walking into range later
   changes none of that key, so it never asks again. Similarly, a decline because a merchant has no
   stock may survive a later real merchant arrival indefinitely. Check actual return-to-useful-trading
   cases without inventing stock, exposing merchant inventory or changing the NPC's wallet merely to
   perturb the key. Bounded reconsideration based on lawful local evidence or a deliberate finite
   scheduling policy can prevent per-tick flooding without permanently disabling the desire.

2. The draft `ValidateTradeSelection` checks only whether a paid/market owner exists. During a
   reading hold, `SubmitAction` can therefore queue a paid or market action, while `SelectBatch`
   constructs a held batch and `WorldStepBatch` throws for that intent before the preview try/catch.
   Test direct paid/market submission during a real reading hold and other incompatible boundaries:
   the request should be refused without faulting the session. A waiting nonbinding inquiry may
   legitimately stay queued across a hold; it is a different contract.

3. Recheck what "composes with gifts" actually proves. The existing material selection, preparation
   and NPC gift controller still bind only profile 30, while the market world is profile 33 and the
   core already permits its saved gifts. Avoid calling scheduler ordering alone a live composition
   proof when the other controller or input cannot be selected in that world. Preserve older rules
   and qualify any added host binding through real session actions and appropriate contrasts.

No runtime edit, new external request or acceptance decision was made from this preliminary reading.
