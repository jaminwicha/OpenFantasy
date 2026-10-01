# Paid-quote lifecycle repair result

Worker: claude-opus-5-5[1m], Max effort, no nested agents, no retry. Only owned paths were written.
No staging, commit or branch change. No tool permission was denied. Status: implemented and verified
with the three permitted wrappers. Not accepted; Astra owns integration and broad regression.

## Changed behavior

- **Supersession (finding 1).** `WorldPaidExchangeBook.Fill` now closes the filled quote, advances the
  issuer revision once and, in the same owner candidate (same complete transaction), closes each of
  the issuer's other open quotes as `Superseded`. Each keeps its exact quote and names the actual cause
  (`WorldPaidSupersession`: acceptance id and filled quote id) at the fill's world sequence. No money,
  goods, progression/currency revision or extra issuer revision. The resolver checks `CanFill` so the
  window holds the fill and all supersessions. The book now refuses any open quote whose market revision is
  not its issuer's current one, so a stale sibling cannot stay open.
- **Withdrawal (finding 1).** New `WithdrawSpotQuoteIntent(quoteId)` (action codec 17 frame
  `world.action.spot-quote-withdraw`). Ordinary order applies: current control, own presence revision,
  then advancing/unheld/ready. It requires an open, not due quote issued by the submitting actor;
  missing, closed, due or foreign quotes all give `Unavailable`. It has no payment, goods, revision
  or work effect. The outcome records `WorldPaidWithdrawal` (command id, delivery, batch ordinal). Host
  submission uses the existing `SubmitPaidExchangeCommand`: a wrong delivery is refused before disclosure,
  and an exact retry returns the original receipt.
- **Canonical order (finding 5).** The outcome frame is now `world.paid-quote-outcome.v2` (typed
  cause for all four kinds; book frame tag unchanged). The book requires one strict order: sequence;
  command outcomes before expirations; batch ordinal; a fill before its supersessions (quote-id order);
  expirations in quote-id order. Two primary outcomes at one position are refused, and decoding refuses
  permutations without normalizing them. Fill order equals acceptance order, so currency replay is unchanged.
- **Root admission.** Withdrawal command ids are unique `CauseReference` identities. Supersession causes
  must resolve to the fill's acceptance and quote. Withdrawal delivery must control the issuer under the
  admitted, currently immutable bindings. New `ValidateLifecycle` runs after the unchanged
  `ValidateIssuerRevisions` for fresh and post-cut segments. It checks issuance revision causality,
  that each fill supersedes exactly the quotes still open, and one issuer command per step, none in a
  step settling its quote. Carried quotes cannot take a cause from before the boundary.
- **Issuer bound (finding 2).** Not reachable. Revisions are never reset or pruned. Identity admission
  now also requires each issuer to have admitted presence (at most 64), and tests pin
  `MaximumIssuers >= 64` for presence and batch actors. The future roster-change obligation is documented.
- **Docs.** `world-paid-exchange.md` now covers the lifecycle table, canonical order, limits,
  root-admission proof boundary, trusted submit versus privileged `Commit(batch)`, the
  immutable-control assumption, issuer-buys disclosure and no session/native/migration/archived lookup.
  It also states that a refusal preserves owners while time and passive processes advance.
- `WorldStepProcessor.cs`: comment-only change.

## Verification (terminal outcomes observed)

| Mode | Report | Result |
|---|---|---|
| build | `qualification/build-20261001T000418Z-c3232/report.json` | accepted (source before tests) |
| build | `qualification/build-20261001T001151Z-4cf09/report.json` | accepted, final source |
| new-core | `qualification/new-core-20261001T001219Z-73d42/report.json` | 10 passed: 5 existing, 5 new |
| new-host | `qualification/new-host-20261001T001241Z-efbbd/report.json` | 15 passed: 12 existing, 3 new, 35.6s |

All reports show 513 existing tests/fixtures, 23 frozen specs, the source database and predecessor
staging preserved before and after, with `sourceUnchanged: true`. The docs and this file are outside
the pinned source set.

New tests:

- `PaidQuoteLifecycleTests`:
  - supersession keeps exact terms and causes;
  - withdrawal cause, bytes and profile refusal;
  - all 15 pairwise swaps and the full reversal of a fill/supersede/withdraw/expire step are refused;
  - forged, foreign or wrong-step supersession, a stale open sibling and a duplicate position are refused;
  - issuer-bound coupling.
- `PaidWorldExchangeTests.Lifecycle` (actual SQLite owner, quest-earned funds, real commands):
  - a full 16-quote book refuses another actor; one fill supersedes 15 with exact terms and causes, one
    payment and unchanged actors; then another actor and the issuer issue again;
  - an actual gift makes stock stale; wrong-actor withdrawal is refused (owners unchanged), a
    wrong-delivery withdrawal throws, the issuer withdraws, an exact retry returns the same result, and the
    slot serves another actor;
  - 3 fills, 45 supersessions and 1 withdrawal precede a real automatic cut with 14 carried quotes;
  - after reopening, identities stay occupied and the retry returns identical receipt bytes; a carried
    quote is then withdrawn and one fill supersedes the rest, and a second reopen keeps the exact head.

## Limitations and remaining work

- Protected stock: no admitted profile-32 action or hosted content creates protected stock, and no state
  was injected. The hosted proof covers stale (gift-spent) stock only. Withdrawal reads no stock, but no
  protected-specific test exists.
- Only new-core/new-host ran, as instructed. Full Core/Host, legacy suites and process kills remain for
  Astra. Existing profile-32 WIP bytes with v1 outcome frames, such as earlier kill fixtures, no longer
  decode. They remain evidence of earlier source; fixtures must be regenerated.
- Root admission is structural. Durable journal replay remains the only history authentication. The
  control and ownership comparisons assume immutable bindings until an explicit session/roster transition.
- Still not built: decline, a per-issuer cap, archived participant quote lookup, native UI/session binding,
  visibility validation of issuer-buys disclosure, and pricing or discovery policy.
