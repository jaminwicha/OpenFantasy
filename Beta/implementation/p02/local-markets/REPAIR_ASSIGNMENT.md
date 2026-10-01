# Market privacy and policy repair

This is a new bounded implementation assignment after independent lead inspection of the delivered
market draft, not a retry of the earlier denied setup operation. Use exactly
`claude-opus-5-5[1m] --effort max`, at most3,600s/$30 client estimate. No nested agents, installs,
model fallback, extra paid requests, staging or commits. Astra integrates. You are not alone: preserve
all other writers and every existing test pinned by PRESERVATION.json.

The delivered draft builds. Initial checks:17 Core cases pass;36 of37 Host cases pass. The failing
`Learned_prices_are_local_dated_private_and_never_refresh_from_a_distance` cannot find the assumed
route in `LocalMarketWorldFixture.DistantGoal`. Reports are in INITIAL_CHECKS.json. The lead fixed the
wrapper's missing qualification parent. The granted exact wrapper commands now run successfully.
First create REPAIR_RESULT.md with an in-progress note; complete real checks before returning.

Read root/Beta AGENTS, handoff, world-markets.md, world-paid-exchange.md, world-composition.md and the
original ASSIGNMENT.md. LEAD_REVIEW_NOTES.md contains preliminary source findings, not binding claims:
the final draft already removed the suspected carried-quote creation-sequence rejection, and the
actual cut/settlement test passes. Do not reintroduce or 'repair' that nonexistent defect. Profile33
has not been accepted; its new format can be corrected coherently, but preserve all earlier bytes.

## Deliverable

Repair the market boundary and prove these concrete contrasts through the actual authority:

1. `PrepareObservation` currently returns a distant merchant's live presence revision for a guessed
   market. Gate preparation on genuine local visible availability, before disclosing a revision;
   unknown and unavailable projections must be indistinguishable. Actual raw command rejection must
   still be tested. Move the observer by real advancing steps along an existing path, rather than
   teleporting or weakening reach. Correct the fixture's route selection and retain old-memory,
   revisit, NPC observer and foreign-reader contrasts. The uncompiled MarketPrivacyReviewTests.cs.txt
   is an optional lead sketch, not a test that must be copied literally.
2. `PrepareOffer` searches all private lots and reveals a guessed lot's definition/revision before
   checking its custodian. A market sell offer can inspect only its own eligible selected lot. A buy
   offer must obtain an explicit seller-authorized disclosure of that selected lot; knowing its ID or
   standing nearby is not consent to inspect private inventory. Fix both the preparation API and the
   raw market-command issuance oracle, not merely the getter. Keep functional buy-side trading.
   Use a narrow typed world fact/command for a seller to show one selected lot to this market if that
   is the cleanest fit. Pin merchant/market, seller, lot definition/revision and scope/window as needed;
   no manufactured actor delivery, account consent, automatic sale or private global bag view.
   Keep this disclosure distinct from gift permission, quote acceptance and stock ownership. Bound its
   lifecycle, canonical bytes, identity, time and replay; preserve still-live facts at a real automatic
   cut and retain actual causes. Test forged disclosure, foreign/unknown lot indistinguishability,
   expired/revoked/stale lot behavior where supported, lawful positive resale, reopening and cut.
   Profile32 keeps its old explicitly limited interpretation. Explain how ordinary bilateral issuance
   in profile33 interacts with the new privacy boundary; do not leave a bypass or silently drop it.
3. Reproduce the fee coalition counterexample before fixing policy: constant source/min/max5,
   markup1, markdown1, fee3 to a third actor gives ask6/bid4. Buying then reselling one unit leaves the
   cycling actor -5, payee +6, merchant -1, restoring stock. Currency conservation alone misses the
   cooperating actor/payee gain. Make the normal all-in merchant bid/spread safe for this supported
   1–4-player setting. Preserve positive nonzero-fee trading and finite real funding; no banning fees
   merely to pass. Test pure split/order edges and at least one actual funded Host round trip with the
   third party's balances included. Document the precise supported guarantee and its limits.
4. Reconcile structural admission with the promised fixed quote terms: validate policy fee/payee
   equality as well as price and allowance. Check overflow/high-stock inputs and surface ordinary
   refusals instead of uncaught MarketQuoteRecord bounds failures. Add discriminating regressions.
5. Treat the current HomePlaceId binding honestly: it survives movement and is origin identity,
   not geometric stall containment. Prefer explicit mobile actor-market semantics with actual mutual
   reach and presence checks unless real venue geometry is needed. Update names/docs where relevant.
   Explain that an OPEN stock-conditional quote may become eligible again when nontrade stock returns;
   a CLOSED/filled quote never revives. Preserve issuer fill revision semantics and exact terms.

Own only the paths from the original ASSIGNMENT's runtime scope, including new/modified Markets tests
(not the526 pinned prior tests), world-markets.md, and REPAIR_RESULT.md. The original RESULT.md,
assignment/review notes, wrappers, preservation, handoff and all qualification output are read-only to
file tools. The permitted checker writes its own evidence. Runtime source outside that scope remains
read-only; report an essential extra seam instead of editing it. No unrelated refactor, native UI,
source migration or phase-gate reduction. Keep readable C# domain owners and complete transactions.

## Checks and report

Run these exact shell calls (Bash timeout600000), serially, observing each terminal report:

python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/local-markets/run_integration_checks.py build
python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/local-markets/run_integration_checks.py new-core
python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/local-markets/run_integration_checks.py new-host
python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/local-markets/run_integration_checks.py focused-core
python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/local-markets/run_integration_checks.py focused-host

Only these shell grants; Read/Glob/Grep for discovery. Do not scan parent temporary directories. If a
tool is denied, report it without bypass or broader grants. Begin the build early enough to repair
ordinary errors within the same finite request. Retain every failed test and do not weaken its actual
behavioral requirement. New disclosure checks must actually execute, not exist as detached source.

Return a compact implementation map, decisions, exact evidence paths/counts, failures/dispositions and
remaining gaps. Explain any cases the scope/time did not finish. The lead will independently review
the diff and tests before committing; a worker summary is not acceptance.
