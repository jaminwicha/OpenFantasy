# Paid transfer and placed-liquid rollback

Qualified test-only proof; runtime remains unchanged. The actual profile32 farm already owns a
non-primary water reservoir, so no new genesis or invented stock is needed. Quotes bind any storage
owned by the named actor, not only its primary pack.

`A_locally_valid_paid_water_transfer_rolls_back_when_it_would_detach_live_liquid_placement` earns
quest funds, issues a real one-unit quote from the helper's water reservoir, and proves three separate
boundaries on the same actual owners:

1. Public `SpotExchangeAuthority.Accept` accepts the diagnostic goods/payment candidate. It names
   the exact pending command and quote; no diagnostic candidate is published.
2. Reservoir receiving capacity and physical volume accept those candidate owners.
3. `LiquidCellState.Rebind` refuses them because detailed cell placements would no longer exhaust
   their custodian's actual source intervals. The specific exception text is pinned.

Actual `SubmitPaidExchangeCommand` returns `Unavailable`, leaves inventory/production/progression,
source baseline/anchor, gift/consent/paid books and resource history exact, and creates no split-child
identity. Reservoir/liquid/time state matches an independently decided empty advancing step.
The receipt and command identity are retained; reopening and exact retry reproduce the same head
without effects. This is a genuine late validation failure after the local material plan succeeds.
It closes the original late-reconciliation rollback requirement, not general liquid handling,
container drawing, arbitrary physical failures, native UI or phase/production acceptance.

Build and all25 paid-world Host cases pass in
[build-20261001T010519Z-afb8e](qualification/build-20261001T010519Z-afb8e/report.json) and
[new-host-20261001T010530Z-d527f](qualification/new-host-20261001T010530Z-d527f/report.json).
The preceding compile failure remains retained: an assumed coordination hash property, nullable
flow, Assert.Single analyzer and explicit test-cancellation argument were corrected in the new test.
No old test, runtime source, frozen spec, original database or predecessor staging changed.

The unchanged paid-world architecture now explicitly documents secondary owned storage and the
placed-liquid constraint. The remaining required proofs are actual capacity/protected/reserved
stock and two eligible buyers; the earlier report's inability to locate a suitable fixture is not a
world-admission prohibition.
