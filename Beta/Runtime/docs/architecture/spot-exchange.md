# Atomic spot exchange

A standalone portable domain contract in `OpenFantasy.Core.Markets`. It couples one fixed quote's goods
and payment into one transaction over the existing owners. It is not bound to any world profile,
host, network, persistence path, native interface or price model.

## Ownership

| Fact | Owner |
| --- | --- |
| Raw counted custody, storages, controls, access policies | `CountedAuthorityState` |
| Produced lots and output provenance | `ProductionSnapshot` |
| Finite balances and admitted supply | `ProgressionSnapshot.Currency` (`CurrencyLedger`) |
| Issued quotes, remaining allowance, fills, per-issuer market revision | `SpotQuoteBook` |

The book holds no balance and no stock. `ProducedMovements()` projects its produced fills as the
existing `ProducedCustodyMovement` evidence, so `ProductionSnapshot.ValidateCustodyEvidence` replays
paid produced movements exactly like gift evidence. The raw split remains in custody with its exact source interval. Ordered material history retains
both raw and produced movements through genuine settlement evidence.

## Quote terms

`SpotQuote` binds world/branch, the issuer, the named counterparty and the side (issuer sells or
issuer buys). It also binds the raw or produced lot kind, lot id, definition/version pin and quoted
lot revision; the seller and buyer storages; and the currency definition/version with seller and
buyer accounts. Money terms are a positive whole-unit price, up to four named per-unit fees paid by
the buyer, and a finite allowance (≤ 1,000,000). It also binds the issuer's book market revision and
a visible fixed-world step window `[IssuedAtStep, ExpiresAtStep]`. The constructor refuses any
allowance whose full total cannot be represented exactly.

The issuer's principal and session are submitted claims. `SpotExchangeAuthority.Issue` compares them
with a host-supplied `TrustedCommandDelivery` and current control, checks storage and account
ownership, identity freshness, the current step and the issuer's market revision. The resulting
entry records that delivery. Issuance reserves no stock and moves no money. Its plan pins all four
owners read during the decision, including production identity occupancy; a changed owner requires
a new decision before applying the quote.

## Acceptance

`SpotAcceptance` binds the quote id, the quote's content hash, the selected quantity and both exact
money totals (payment and fees). A gift acceptance is not reused. `Accept` decides in this order:

1. Authentication, before any availability report. The quote must exist, the acceptance must bind
   it, and the issuer delivery recorded at issuance must still equal current control. The
   counterparty's claims must equal their delivery and current control. Each party must own its
   storage and account.
2. An exact retry (same acceptance id and hash on a filled quote) returns `Duplicate` with no change.
   Any other acceptance of a filled quote is `QuoteClosed`.
3. Permanent identity occupancy covers every owner plus caller-supplied complete history.
4. Expiry, the issuer's market revision, and the currency pin and fee payees.
5. Buyer funds, then stock. Stock must be the exact definition held by the seller at the quoted
   revision. It must be unreserved and carry no protection flag, so a sale is stricter than a gift,
   where manual flags travel with the lot. The buyer's combined raw+produced capacity is also checked.
6. Movement. Raw stock uses the shared `CustodyTransferPlanner` with the acceptance id as the command
   identity, so a split child derives from it. Produced stock uses the same prefix-split rule as
   consented gifts.
7. Payment through `CurrencyLedger.Transfer`, which redistributes existing balances. The ledger
   constructor re-proves the exact supply. Fees payable to the seller join the seller credit; each
   touched account advances once, and unrelated accounts and actor stats stay unchanged.

The result is one `SpotExchangePlan`: either complete candidates for all four owners or the untouched
originals. `ApplyTo` requires the exact content hash of every decided owner.

## Single-fill policy

A quote settles one fill of at most its allowance and then closes; any unused allowance is retired.
A fill advances the quoted lot's revision and the issuer's market revision, so a quote priced
against the earlier revision could never truthfully settle a second fill. Continued trading needs
a newly issued quote.

## Canonical data

`SpotExchangeCodecV1` defines canonical frames for quotes, acceptances and the book, including open
and filled entries and issuer revisions. The frames are bounded, strictly ordered and decoded exactly
(truncation, trailing data, version, type, limits and non-canonical order). A content hash is
structural only.

## Ordered material history

`ProductionSnapshot.ContinueSpotExchange` explicitly selects `SupportsSpotExchange`, context profile
10 and `ProductionCodecV12`. It captures the actual material frontier and retains any existing
`SupportsConsentedTransfer` capability without granting it implicitly. Ordinary material cuts preserve
these capabilities. Construction, geographic and complete-world contexts cannot select this
standalone interpretation. All older context and snapshot formats retain their bytes and rules.

Each paid movement appends a `MaterialSpotExchangeOperation` with genuine `SpotSettlementEvidence`
created only by an approved, funded settlement. Evidence binds the exact quote, acceptance, both
recorded deliveries, step, result identity, before hashes of all four owners, and after inventory,
progression and quote-book hashes. These observations own no balances or stock. There is no public
proof constructor or proof decoder; the V12 decoder requires the actual opaque evidence matching
each recorded hash, as other material capabilities require their own genuine evidence.

`SpotMaterialTransferStep` is shared arithmetic, not authority. Settlement calls it only after
validating both parties and funds. Ordered replay calls it against the exact prefix named by the
proof, checks controls, ownership and every lot/capacity rule again, then compares the exact result.
The quote and acceptance identities are occupied alongside all commands and spawned lots. A partial
move preserves the producing batch/output index and moves the prefix; a full move preserves the
lot identity. Neither source issuance nor money supply increases.

The planner returns complete immutable candidates or the untouched originals, including on revision
or arithmetic overflow. Material replay proves the goods consequence of a genuine settlement. It
does not independently replay balances or replace durable four-owner world validation. After a
material cut, the host remains responsible for retaining closed evidence and historical identities;
this component performs no archive publication.

## Boundaries and obligations

- Continued owners without the explicit paid capability refuse both raw and produced trades with
  `UnsupportedHistory`. A gift approval is never narrowed or repurposed as paid consent.
- No existing world profile admits the book or its candidates. World admission must still:
  - persist the book;
  - merge book fills with other ordered custody evidence;
  - supply the authoritative current step and complete-history occupancy;
  - bind host authentication;
  - define cut/replay/resource-history treatment of fills.
- Prices are fixed per quote. Supply/demand pricing, stock-driven revisions, NPC quoting policy,
  transport and native UI belong to a later market binding.

Related contracts: [production continuation](production-continuation.md),
[world resource history](world-resource-history.md), [world composition](world-composition.md).
