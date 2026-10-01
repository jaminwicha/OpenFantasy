# Additional lead inspection during the bounded repair

This is a source/arithmetic review target, not an executed game result. Recheck the final delivery.

## Duplicate policies over one stock pool

The original WorldMarketBook allows two distinct market identities to bind the same merchant,
custodian and commodity to different policies. They then price the same actual stock pool.
For stock8, source5, low target8/soft4/markup1 and high target32/soft1/markdown1, both fee0:
buy1 through the low rule costs6; sell1 back through the high rule returns17. Stock returns to8
and the trader gains11 from a sufficiently funded merchant. Both rules use the same source price;
only their authored demand targets differ. The issuer-fill revision requires fresh quotes but does
not prevent another cycle. Fee-safe pricing within each rule does not close this cross-rule loop.

Decide whether one priceable custodian+commodity pool must have exactly one market policy in an
admitted world. If so, reject duplicate bindings structurally (including genesis and decoding),
while allowing genuinely distinct pools/commodities. Do not forbid ordinary inter-market trade
whose stock distributions really change. A constructor/admission regression is appropriate if
it makes the contradictory world unrepresentable; demonstrate the arithmetic and a lawful contrast.

This was found independently after dispatch; the active worker has not been told to implement it.
Do not mutate its owned code during its run or count this note as a new paid request.
