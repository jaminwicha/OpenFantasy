# Independent Opus 5.5 review

Original read-only result, retained verbatim below. Lead dispositions are separate.

I found no correctness defect that lets play be exploited or corrupts state. I did find one capacity problem that can lock everyone out of issuing quotes, and I'd hold acceptance until it's fixed. This was a read-only review: I ran nothing and edited nothing, and no permission requests were denied.

## Findings, most severe first

**1. Medium-high: quotes that can never be filled still take up the shared limit of 16 open quotes, and nobody can close them early.**
- **Where:** `WorldPaidExchangeResolver.cs:78` (the `!book.CanIssue` check), `WorldPaidExchangeBook.cs:16,100,117-129` and `WorldPaidExchangeIntents.cs`. The only intents are Issue and Accept.
- **Trigger:** an issuer offers quotes to several counterparties and one is filled. The fill advances the issuer's market revision, and revisions only go up, so every sibling quote becomes permanently unfillable. The same happens when the pinned lot's revision changes, for example by crafting, a gift or a use. A quote issued on protected or reserved stock is dead from the start, because issuance doesn't check protection.
- **Consequence:**
  - Dead quotes stay open until they expire, up to 65,536 advancing steps.
  - `ValidateSuccessor` (`WorldPaidExchangeBook.cs:205-225`) only accepts Filled or Expired, so every cut must carry them forward.
  - Sixteen of them, from anyone, block all quote issuance for every actor. The cap is shared by the whole world, not per issuer.
  - Proposals avoid this because they have Withdraw and Decline.
- **Fix:** in the same fill transaction, close the issuer's other now-stale quotes with a terminal "Superseded" outcome. This is safe for the cut reservation, because `RequiresCut` already counts every open quote as closable. Add a withdraw for the issuer and/or a per-issuer open limit.
- **Test:** issue 16 quotes and fill one. Then show that another actor can still issue, and that the dead quotes are closed and their identities stay occupied across a cut and a reopen.

**2. Low-medium: once 128 different issuers have had a fill, no new issuer can ever have a quote filled.**
- **Where:** `WorldPaidExchangeBook.cs:66`. The list of issuer revisions is never pruned.
- **Trigger:** the 129th distinct issuer's first fill. `Fill` throws, and `Resolver.cs:193` catches it as Unavailable, every time.
- **Consequence:** issuance never checks this limit, so those issuers can keep issuing quotes that can never be filled. With many NPC merchants in the full design, valid trade would be refused permanently.
- **Fix:** refuse issuance explicitly when a new issuer can't be registered, or prune issuers with no open quotes at a cut, and document that meaning.

**3. Low, uncertain: issuer-buys quotes let the issuer probe another actor's storage.**
- **Where:** `Resolver.cs:88-93,199-219`.
- **Trigger:** issuing a quote that pins a lot in the counterparty's storage costs the issuer no work. The receipt differs by case: None, Unavailable (missing lot or not enough quantity) or RevisionConflict.
- **Consequence:** the issuer learns whether the other actor used, split or moved the lot. Whether this matters depends on the local-visibility rules, which are a design obligation but aren't implemented yet.

**4. Not a bug today, but will break later: admission checks past fills against present-day state.**
- **Where:** `WorldPaidExchangeAdmission.cs:132,141-146`.
- **Current behaviour:** fills in the live segment are checked against current control bindings, and every quote against current storage and account ownership. Today these are equivalent to checking against history, because control bindings are only created at genesis and in the decoder.
- **Risk:** the handoff says control/session transitions must land before multiplayer. Once they do, a world with a legitimate fill in its live segment would become permanently inadmissible.
- **Fix:** write this dependency into the architecture doc, or check fills against recorded deliveries only. The durable replay already proves those.

**5. Low: two different byte orders of the same outcomes are both accepted.**
- **Where:** `Book.cs:51` only requires outcome sequence numbers never to decrease.
- **Consequence:** within one world sequence, fills and expirations, and expirations among themselves, can appear in either order and both pass structural decode. Durable replay only ever produces one order, so there is no authority hole.
- **Fix:** require fills before expirations and expirations in identity order.

## Areas where I found no defect

- **Old-format separation:** `WorldStepBatch:83`, the V32 dispatch and the explicit profile lists in Host recovery, archive and journal are all in place, and ProductionV12 is never selected.
- **Currency causality:** actor batches and fills replay as one ordered sequence and must reproduce the ledger exactly. Collisions can't happen because encounter and care batches use ordinal 0 in their own batch kinds, and fills only occur in advancing steps.
- **Work opportunities:**
  - The issuer's actions anywhere in the batch block settlement.
  - A fill marks both participants as having worked.
  - A refused acceptance consumes nothing.
- **Rollback:** a failure after the resolver restores every owner, and the complete-candidate check does run, because profile 32 has reservoir seals.
- **Cut reservation arithmetic:** with 1 reserved slot, a segment reaches at most 63 of its 64 outcomes.
- **Identities:** occupancy goes through the durable identity lookup, and uniqueness is checked across the separate roles.
- **Retry:** a retry is authorized before anything is disclosed.

## Coverage gaps that matter for acceptance

- The capacity cases in findings 1 and 2.
- The issuer being unavailable at settlement: in an encounter, with care pending, at 0 HP, or without a manipulating body. Only reach is proved.
- Multi-command batches in one step:
  - a gift settlement followed by a paid fill on the same lot;
  - a quest reward and a fill touching the same account;
  - two acceptances against one issuer.
- A rollback that fails after the resolver has succeeded on a paid fill.
- A fee paid to the seller's own account, and a fee paid to a third party, both through hosted replay.
- Tampering with paid rows at the SQL level, followed by a durable reopen. The existing test covers structural admission only.
- Participants can't look up an archived quote outcome, as they can for proposals via `ReadRecordedProposalForController`. After a cut, receipts are the only history.
- Process-kill qualification, which the lead is running.

## Architecture and docs

The ownership split is clean and follows the composition contract:
- the book owns quote terms, outcomes and market revisions;
- stock stays with inventory and production;
- money stays with progression;
- no parallel authority is introduced.

`world-paid-exchange.md` matches the code: the check order, the revision equation and how cuts carry quotes. It should also say:
- that quotes have no withdraw or decline, and dead quotes hold capacity until expiry;
- the 16/64/128 limits and whether each is permanent;
- the dependency in finding 4;
- the issuer-buys disclosure behaviour in finding 3;
- that the public `SqliteWorldAuthority.Commit(batch)` is a privileged path that takes the claimed delivery at face value, as it already does for proposals;
- that profile 32 has no local or native session. `LocalWorldSession` and its controllers list 30 but not 32, which is expected but worth stating so nobody assumes native play.
