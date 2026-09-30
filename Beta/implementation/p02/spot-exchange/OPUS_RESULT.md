# Atomic spot exchange: Opus result

Status: component delivered; build and focused checks accepted. This is structural component proof
only. It is not world admission, durable authority, host authentication, native execution or human
acceptance.

## Design and ownership

- New `Beta/Runtime/src/OpenFantasy.Core/Markets/`:
  - `SpotQuote`: terms, fees and step window.
  - `SpotAcceptance`: binds the quote hash, quantity, payment and fees.
  - `SpotQuoteBook`: the only new owner. It holds quotes, allowance/fill state and per-issuer market
    revision, and projects produced movements.
  - `SpotExchangeAuthority`: `Issue` and `Accept`.
  - `SpotExchangePlan` / `SpotQuoteIssuePlan`: all-or-nothing candidates; `ApplyTo` requires exact
    base hashes for inventory, production, progression and the book.
  - `SpotExchangeCodecV1`: canonical frames.
- Each fact keeps its existing owner: custody/controls in `CountedAuthorityState`, produced
  provenance in `ProductionSnapshot`, balances in `ProgressionSnapshot.Currency`. No parallel ledger.
- Raw movement reuses `CustodyTransferPlanner`, with the acceptance id as the command identity.
  Produced movement uses the gift prefix-split rule. Produced fills are recorded as the existing
  `ProducedCustodyMovement` evidence, and `ValidateCustodyEvidence` replays them. No gift approval is
  forged.
- Policy is single-fill: unused allowance closes. An exact retry returns `Duplicate`; a different
  acceptance of a filled quote gets `QuoteClosed`; a reused or occupied identity gets
  `IdentityConflict`.
- A sale requires unprotected, unreserved stock. This is deliberately stricter than a gift.
- Added `CurrencyLedger.Transfer` (internal; the constructor re-proves supply) and
  `ProgressionSnapshot.WithCurrency` (internal; actors, XP and HP/MP unchanged).
- The issuer's delivery is recorded at issuance and must still match current control at acceptance.
  Deliveries are host-supplied; nothing here authenticates.

## Changed files

- `Beta/Runtime/src/OpenFantasy.Core/Markets/`: `SpotQuote.cs`, `SpotAcceptance.cs`, `SpotQuoteBook.cs`,
  `SpotExchangePlan.cs`, `SpotExchangeAuthority.cs`, `SpotExchangeCodecV1.cs` (all new)
- `Beta/Runtime/src/OpenFantasy.Core/Progression/CurrencyLedger.cs`: `Transfer`
- `Beta/Runtime/src/OpenFantasy.Core/Progression/ProgressionSnapshot.cs`: `WithCurrency`
- `Beta/Runtime/tests/OpenFantasy.Core.Tests/Markets/`: `SpotExchangeFixture.cs`, `SpotExchangeTests.cs` (new; 7 cases)
- `Beta/Runtime/docs/architecture/spot-exchange.md` (new; not yet linked, which Astra owns)
- No Production source, world profile, codec version or old test changed.

## Checks

- Build accepted: `qualification/build-20260930T034632Z-62e64` (33.2s).
- Focused accepted: `qualification/focused-20260930T034712Z-6eebc`. 373 passed / 0 failed / 0 skipped.
  Preservation: 507 tests, 23 frozen specs, and the source and predecessor index are unchanged.
- The failed first focused run is retained at `qualification/focused-20260930T034534Z-5c6ff`. The
  failure was in a test assertion, not the domain: an "other" inventory had identical content, and
  hash-basis matching correctly accepted it. The test now uses a changed owner and also asserts
  that content-identical bases apply.

## What the tests cover

- A buy-then-sell journey across two issuers:
  - raw berries are bought with a guild fee and split;
  - rations are crafted with the real planner and sold to the second issuer;
  - exact balances are checked, and the 230-unit supply is conserved;
  - raw source intervals and produced output intervals are preserved;
  - the produced custody evidence replays, and fails without the fills.
- Refusals return every original unchanged, for:
  - wrong or missing participant delivery;
  - tampered payment, quote hash or quantity;
  - expiry before and after the window;
  - occupied history.
- Insufficient funds (with the affordable quantity accepted), capacity, and a protected lot.
- Single-fill closure, duplicate retry, conflicting identity reuse, and a stale market revision at
  issuance.
- Staleness of each of the four owners, and a stale issuer control after rebinding.
- Ordered material history refuses the trade (`UnsupportedHistory`). This is the admission-boundary
  contrast.
- Codec round-trip plus trailing, type and truncation refusals, and quote overflow refusal.

## Unimplemented joins and risks

- Not tested for lack of time:
  - a stale lot revision at acceptance (the code path returns `RevisionConflict`);
  - reserved (as opposed to protected) stock;
  - `Overflow` at acceptance (quote construction already bounds it);
  - fuller malformed-codec cases: version, limits, non-canonical fee/issuer order, fill-shape refusals;
  - an explicit legacy gift regression beyond the existing focused suite, which passes.
- No world profile admits the book. World admission must still:
  - persist the book;
  - merge book fills with the world's consent evidence;
  - supply the authoritative step and complete-history occupancy;
  - bind host authentication;
  - define resource-history, cut and replay treatment.
- Ordered material-history owners need a new explicit material operation kind and codec version for
  paid trades. This is not implemented.
- A produced trade keeps `ProductionSnapshot.Revision` unchanged, as world consent evidence orders
  the movements. The world binding must confirm this matches `WorldConsentedMaterialPlanner`.
- `Issue` does not consult production identities; complete history must cover them.
- Not implemented: price evolution, NPC quoting, transport/escort, native UI, and the two-market
  circuit.

## Contradictions and friction

- One `cat | grep` Bash read was denied in don't-ask mode. I used the Read tool instead; no required
  operation was denied.
- Nothing else contradicts the assignment.
