# Local-market integration review

Status: accepted bounded market binding after independent critique, repairs and final regression.
This record does not close a phase or game acceptance gate.
The preserved baseline is `cf5556538e5c53ad5a2e3c69653c3adc3ba88c17` and the qualified paid-world
milestone. The market binding is a fresh profile33 interpretation; no lived-save migration is claimed.

## Evidence and preservation

- [Initial checks](INITIAL_CHECKS.json): solution builds;17 selected Core cases pass;36 of37 Host
  cases pass. The failing local-knowledge test assumes an unreachable distant goal. The original
  failure remains in its TRX/log/report. All526 pinned earlier tests,23 frozen specs, source DB bytes
  and unrelated predecessor staging are unchanged.
- The first two delegated wrapper attempts failed before dotnet because the qualification parent
  was absent. The lead initialized it and made the permitted wrapper create it recursively. The
  worker's out-of-scope file creation was denied; its original result and dispatch retain that fact.
- [Delivered checkpoint](DELIVERY_CHECKPOINT.json) preserves the unqualified implementation.
  [Pre-repair checkpoint](REPAIR_CHECKPOINT.json) also preserves actual lead overlay and initial
  results; neither is an acceptance claim. The shared Git index was left intact.
- [Repair assignment](REPAIR_ASSIGNMENT.md) and [dispatch](REPAIR_DISPATCH.json) define the explicit
  new work. No automatic model retry, fallback, limit extension or permission expansion occurred.

## Lead findings and disposition

| Concern | Observed basis | Disposition |
|---|---|---|
| Distant presence disclosure | Preparation returned a guessed market's live merchant revision before locality | Shared local gate; real/unknown remote preparation both null; raw submissions both refuse |
| Private selected-lot disclosure | Preparation searched all lots; issuance could test an undisclosed buyer-side lot | Seller-authored disclosure, exact pins/window, withdrawal, expiry, raw market and bilateral bypass contrasts pass |
| Cooperating fee recipient | Constant-price ask6/bid4/fee3 restored stock while buyer plus fee recipient gained1 | Merchant bid includes its fee; funded Host cycle restores stock and charges coalition2; split/interleaving Core sweep passes |
| Complete fixed terms | Structural admission checked price but omitted exact fee list | Shared fee/payee equality; forged-fee root refusal passes |
| Mobile location semantics | HomePlaceId survives actor movement | Origin pin at admission; actual reach determines interaction; no fixed-venue claim |
| Real distant route | Initial Host test could not find any of its sampled goals | Four bodies genuinely enclosed the player; third human now walks aside before the player walks away and revisits |
| Carried quote settlement | Preliminary review targeted an earlier draft condition, absent from final delivery | Initial automatic-cut/live-settlement test passes; no repair warranted |
| Stock returning to old count | Quantity is a condition, not a price revision | An open conditional offer can regain eligibility; a closed offer cannot revive |
| Competing policies over one stock pool | Two authored demand targets price the same goods differently | New admission regression failed before guard and passes afterward; distinct stores still admit and reopen |
| Stale showing reveals remaining quantity | Independent critic F1: partial changes returned a different reason from insufficient stock or a full move | Both disclosed buy routes now give one lot-pin refusal; real partial/full gifts and allowance probes preserve all transaction owners |
| Fee recipient as counterparty | Independent critic F2: sell preparation threw; buy preparation returned an unusable command | Either direction returns null before quote construction; a different counterparty still issues lawfully |

The [repair report](REPAIR_RESULT.md) records its final five passing selections and their exact limits.
The lead's additional stock-pool test failed in `new-host-20261001T030412Z-6dd89` with no exception from
fresh admission. After the constructor guard, final build `build-20261001T030512Z-ae505` and selections
`new-host-20261001T030552Z-0972a` (40 passed) and `new-core-20261001T030638Z-c0412` (26 passed) pass on
the same source and binaries. The change rejects contradictory world definitions without banning
ordinary trade between actual distinct stock pools. No failing assertion or existing test was removed.

## Final verification and independent review

The [independent Opus review](INDEPENDENT_REVIEW.md) returned two actionable findings. Both are
repaired with failing-before/passing-after tests; [dispositions](CRITIC_DISPOSITION.md) preserve the
reasoning and documentary corrections. No second paid review was commissioned for these small fixes.
The final build and **1,915 distinct managed cases** pass: all 1,753 Core cases and 162 selected Host
cases, including 46 exchange Host cases. The 30 new market cases comprise 16 Core and 14 Host cases.
[Final evidence](FINAL_CHECKS.json) pins the exact 1,451 source files, 11 assemblies, report hashes and
unchanged 526 old tests / 23 frozen specs/source DB/predecessor staging. The pre-critic 1,909-case result
is retained separately in [PRE_CRITIC_CHECKS.json](PRE_CRITIC_CHECKS.json).

All selections ran serially under their original limits; none failed or skipped at final acceptance.
Dependent architecture pages and the living integration plan are reconciled. File links pass in
[DOCUMENT_CHECKS.json](DOCUMENT_CHECKS.json); the descriptions distinguish stock inference from direct
disclosure and replay-authenticated causes from merely structurally valid state.

The implementation, bounded repair and independent review all returned verified Opus 5.5 / 1M metadata
with Max requested and synchronized context. Their client estimates total about $31.91, not a billing
or net-credit-savings claim. The initial wrapper setup failure, separate repair and useful critic
findings are retained in [DELEGATION_OUTCOME.json](DELEGATION_OUTCOME.json). All external jobs are terminal.
Ecosystem commits `cc2900d` and `19e8200` retain the wrapper, privacy and economy lessons with verified
portable skill synchronization. No automatic paid retry or model substitution occurred.

The accepted scope is preserved by `codex/milestone-local-markets-20261001` at its recorded commit.
The [pre-critic-fix checkpoint](CRITIC_FIX_CHECKPOINT.json) also preserves the tested but uncorrected
delivery and original critique. Neither checkpoint changes the working checkout or predecessor staging.

## Remaining gameplay and production obligations

The complete cargo/escort loop, scheduled trading, native market controls, Marla/Kegga content binding,
participant archived-quote inspection, migration and production multiplayer/device/art/play gates
remain in the [integration plan](../spot-exchange/INTEGRATION_PLAN.md). Previous profile32 process-kill
evidence is retained at its original milestone; it is not new profile33 crash evidence.

## Next application dependency

Scheduled trading needs a participant-safe quote view and an authenticated way for a shopper to ask
for a quote. A shopper must not obtain or supply the merchant's delivery credentials. The merchant's
controller chooses and issues its own offer through the existing authority; the shopper accepts its
exact terms separately. A controller can use its own stock/funds, public local evidence and showings
addressed to it. Another actor's private price memory is not proof that the merchant heard a request.

Inspect the existing NPC scheduling and participant-proposal services before choosing that interface.
A nonbinding request may remain application input until a world commitment exists; introduce durable
request state only if its gameplay contract needs restart, expiry or a retained promise. Prove an
ordinary scheduled buy/sell with real funding, opportunity arbitration and absence/staleness before
building native menus. This is a next-step design constraint, not an implemented merchant loop or a
new required framework. The full integration plan retains the larger cargo and content obligations.
