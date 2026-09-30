# Retained proposal transition — Opus result

Status: implemented; build and focused gates accepted; regression gate result below. Managed Host
evidence only — no native, device, SIGKILL or full-P02 claim.

## Design (smallest safe form)

One explicit **transition cut**, no relaxed guard or public boolean:

1. Fresh admission untouched; a separate typed path (`WorldProposalTransition` →
   `WorldProposalTransitionProcessor`) seals the exact lived profile-27 head as an ordinary joint
   archive (ordinal N+1) and publishes revision+1 as profile 30.
2. Capability pins stay: only a transition anchor (built with the request) selects consent + an empty
   proposal roster from a profile-27 closing root. `ProductionContinuationContext` gains one internal
   factory adopting consent from the *exact named prior context hash*; ordinary cuts still refuse any
   change. `WithTransitionAnchor` is the only way to add both owners.
3. Schema-8 kind-3 commits dispatch only to `WorldProposalTransitionReplay` (strict decoder); schema-3
   dispatch unchanged. Head≠genesis profile is admitted only as 27→30 with exactly one replayed transition.
4. The anchor hash appends the request digest (`TransitionHash`) only for the transition anchor, so
   ordinary anchors keep exact bytes. Archive replay feeds the request from the replayed journal to
   `DecodeTransition`; later roots bind that anchor. No root/ancestor chain is retained; the next
   ordinary cut yields an ordinary anchor.

Wire contract: `world.proposal-transition` v1, 13 fields (tag, version, source `27`, target `30`,
cause, world, branch, revision, steps, head hash, admission, anchor hash or empty, ordinal). Exact names,
no range; no profile number reserved. Existing kind 3; no physical schema change.

Effects: world revision +1; the encounter lifecycle mirrors it via existing `RetainBoundary()`.
Inventory, controls, bodies, resources, care/uses, permissions, quests, calendar, steps, pacing: asserted unchanged.

## Changed paths

Core (`Beta/Runtime/src/OpenFantasy.Core/`): new `Worlds/WorldProposalTransition.cs`,
`WorldProposalTransitionCodecV1.cs`, `WorldProposalTransitionProcessor.cs`; edited
`Worlds/WorldMaterialCareArchiveSegment.cs`, `WorldMaterialCareContinuation.cs`,
`WorldMaterialCareArchiveAnchor.cs`, `WorldCoordinatedContinuation.cs`, `Production/ProductionSnapshot.cs`,
`Production/Continuation/ProductionContinuationContext.cs`.
Hosting (`…/OpenFantasy.Hosting/Worlds/`): new `WorldProposalTransitionPreparation.cs`,
`WorldProposalTransitionReplay.cs`, `WorldDatabaseProposalTransition.cs`, `WorldProposalTransitionUpgrade.cs`;
edited `WorldJournalRecovery.cs`, `WorldMaterialCareArchiveReplay.cs` (also checks retrieved anchor equals
replayed anchor), `WorldUpgradePublication.cs` (`"proposal"` staging label), `SqliteWorldAuthority.cs`
(public `UpgradeCoordinatedToProposals`).
New tests: `tests/OpenFantasy.Core.Tests/ProposalMigration/ProposalTransitionCodecTests.cs`;
`tests/OpenFantasy.Hosting.Tests/ProposalMigration/{RetainedTransitionFixture,RetainedTransitionTests,RetainedProposalJourneyTests,RetainedTransitionRecoveryTests}.cs`.
Docs: `Runtime/docs/architecture/{coordinated-world-continuation,material-proposals,persistence-and-history}.md`.

## Checks (actual)

- Build accepted: `qualification/build-20260930T005606Z-08f21`.
- Focused accepted, 21/21 (Core 8, Host 13; 167 s): `qualification/focused-20260930T005620Z-58324`.
- Earlier failures retained: build `…005408Z-6fbce` (internal encoder in test), focused
  `…005446Z-90ce9` (lifecycle revision mirror; fixed with `RetainBoundary`), `…005105Z-f6b58` (unbuilt sources).
- Regression (same accepted build): `qualification/regression-20260930T005916Z-96ed7`. Core part passed
  38/38 (includes the 8 new cases). Hosting part (includes the 13 new cases) was still running when this
  record was last written; its `report.json` there is authoritative. Not rerun.

Proven on a disposable copy of the pinned source (SHA verified before/after): 1,579 prior commits,
outcomes, receipts, identities (+cause only), checkpoints and 3 archives byte-equal; genesis exact;
transition archive = stored source head. Helper (CareCompanion lookup) offers 1 of its 80 real raw
Potions; offer rides a real automatic cut (ordinal 4, ordinary anchor, carried exact); player accepts
after it (79/1, conserved, HP unchanged, +1 step, controls unchanged); exact retries return identical
receipts without commits; later self-use: 521 uses, HP rises, dose consumed. Refusals: player offering
the helper's owner-only lot, foreign delivery, stale revision. Reopen authenticates everything;
recorded reads at 1579 (exact source hash) and 1580; archived withdrawn outcome retrieved. Forged,
stale, mismatched, reused and occupied requests; used/identical destination; forged request, missing
transition archive, damaged lived archive, unscheduled checkpoint all fail closed. Injected faults
before/after commit and after backup (same implementation).

## Residuals

- No out-of-reach acceptance case; owner-only refusal shown via non-owner offer, not a direct custody intent.
- Never-cut profile-27 path (empty anchor, ordinal 0) implemented but not exercised on a real world.
- No recorded read at a post-cut revision (live head/reopen equality and archived lookup only).
- No SIGKILL executed; native upgraded-save selector/journey, devices and full P02 remain root work.
