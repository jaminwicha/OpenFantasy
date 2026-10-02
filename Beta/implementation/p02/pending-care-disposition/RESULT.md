# Pending-care decisions checkpoint

Explicit profile 38/Care V5 adds patient cancellation and named-helper decline without fabricating
healing. A real request has exactly one treatment or disposition. The closure preserves HP, medicine,
time, readiness, permission and outcome acknowledgments. Human helpers receive their own treatment
and decline choices; the automatic helper policy remains Host-only and never invents a decline.

The implementation adopts the profile/version proposal in the preserved [design review](DESIGN_REVIEW.md).
The [architecture reference](../../../Runtime/docs/architecture/care-dispositions.md) describes the
actual contracts. Production selection and lived-save migration remain disabled/unimplemented.

## Scope and evidence

- Typed domain dispositions, separate latest-closed-defeat and latest-treatment witnesses, exact totals,
  chronology and Care V5 continuation. Old V1–V4 constructors, formats and limits remain intact.
- Root/step 38, action 21 and schema-nine journal binding; authenticated participant decisions,
  permanent identities, terminal headroom and coordinated archive progress.
- Host scheduling prioritizes lawful queued decisions before automatic treatment. Invalid traffic
  receives its own receipt without starving treatment or defeating another queued lawful cancellation.
- Pending patient/helper control can return safely, unready, while preserving paid-work restrictions.
  Zero HP does not prevent a deliberate cancellation.
- Observation body 4 pins the capability even when empty. Patients see cancellation; helpers see
  their own treatment/decline and dose terms. Captured wheel choices never silently retarget. Terminal
  wording distinguishes cancelled, declined and treated requests from encounter acceptance.

[Final checks](FINAL_CHECKS.json) retain **2,544 distinct cases with passing evidence, including all
69 new cases**. Build is warning-free. Full Core: 1,870; new Hosting decisions: 30; client: 179;
existing care: 9; care compatibility: 21; controllers: 73; shared sessions: 26; recovery: 42;
Networking: 265. Counts overlap across selections. New domain cases are included in full Core.

**The managed Server aggregate remains failed:** 76 pass, the unchanged four-player TLS movement
observation misses its five-second deadline, and one Windows ACL case is skipped on macOS. Earlier
Networking also retained two archived-query socket timeouts; the final Networking selection passes,
but that does not establish their cause. No deadline, movement lease, old assertion or parallelization
policy changed. Overall selected aggregates are therefore not all accepted.

[Dependency-based reuse](EVIDENCE_REUSE.json) compares exact sources and actual binaries. After full
Core, only Hosting projection/new tests changed; Core source, tests and every Core assembly are exact.
After the broad final selections, only one new excluded test method gained bounded queue/caller-task
synchronization. Runtime/selected test/fixture/discovery dependencies remain unchanged. The affected
30-case decision selection is rerun on the final build. Original failures and reports are retained.

All **691 older test/fixture files**, **23 frozen specs** and the exact **19-path predecessor staging**
remain unchanged. All 1,286 architecture links resolve. There is no new native, platform, human art/UI,
full-suite, production latency/scale or full-campaign acceptance claim.

## Review and failure disposition

The [independent critic](REVIEW.md) found two real defects: historical treatment could hide a later
same-episode defeat, and invalid rotated traffic could let automation beat a queued valid cancellation.
Both were fixed and independently rechecked in source, with retained regressions.

Verification additionally found and fixed an exact-constructor compatibility break: the legacy
five-argument `LocalCareView` constructor remains alongside the new seven-argument constructor.
All 16 affected UI cases and the affected recovery wording case pass unchanged. Early compile errors
and one new composite-fixture identity collision are corrected with original failures retained.

The repeated-defeat fixture initially assumed immediate helper availability. Actual recovery debt
remains binding; the regression now requires the fresh chronological eligibility/current refusal to
be visible, without granting readiness. Another new test assumed the async caller resumed at the
same moment as publication. It now proves both requests are queued and awaits receipt delivery under
the existing five-second fixture deadline without another simulation tick. Its first-cutoff priority,
no-treatment and losing-receipt assertions remain intact.

No external provider request occurred. Two native workers produced disjoint domain/world units and
one native critic reviewed the coherent integration. These use native agent capacity; no Claude credit
savings are claimed. The recorded account spending-limit failure remains terminal without a retry.

## Remaining qualification

The feature is a verified bounded managed foundation. Complete-world pending-cut/dispose/re-cut,
disposition-only capacity-pressure cuts, maximum composite world frames and new process-crash injection
remain unproved. Domain pending/re-cut, full-window pressure and maximum eight-patient composite
frontiers pass; real journal closure/archive/reopen passes using ordinary proposal pressure. Those
are separate claims. Native profile-38 wheel/device journeys, production selection, authenticated
lived-save migration, broader survival/restart/co-player policy and full campaign gates remain open.
The multiplayer/query timing failures need direct diagnosis rather than a green isolated repetition.

## Preservation

Before this major change, `codex/checkpoint-before-care-dispositions-20261002T182611Z` preserved
`6358c9b77a16f0ffbaf429db790be6496d5e9b41`. The successor was clean; unrelated predecessor staging
was retained separately and pinned in [PRESERVATION.json](PRESERVATION.json). The verified managed
scope is recorded at `codex/milestone-care-dispositions-20261002`, without switching the working
branch or closing any broader gate.
