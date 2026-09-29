# Explicit NPC material decisions

This increment continues the [scheduled-offer assignment](../scheduled-material-proposals/ASSIGNMENT.md)
from accepted `d2c136c`. Astra implemented directly because the prior Fable job remains terminal at
its provider spend limit. No provider call, paid retry or model switch was made for this increment.
Fable5.1/1M/Max remains approved; this is not evidence of provider recovery or credit savings.

## Implemented behavior

The session accepts copied immutable `LocalMaterialGiftPolicy` configuration: one actual Host actor,
named partner and pinned ordinary self-use definition per policy. Defaults select no gifting policy.
Invalid/duplicate configuration is refused before starting the loop, and client collection mutations
do not change the running policy. Policy is host configuration, not a new canonical or private ledger.

NPCs choose their own exact offer, wanted acceptance or unwanted decline under current control.
Giving requires a living partner who can absorb a full dose, supported bodies, actual mutual reach
and available stock exceeding the configured reserve. Defaults retain one dose, seek at most three
when receiving and give offers 64 advancing steps. Raw then produced lot identity determines the
preferred dose; both owners exclude protected/reserved lots. Recipient decisions take priority only
when answering is enabled. All effects still use the one normal cutoff and complete authority.

Proposals/declines spend only their initiator opportunity; an invalid partner action cannot suppress
them. Acceptance additionally needs the sender opportunity and uses complete preview to reserve it.
An outstanding configured offer defers the giver's volunteering ordinary aid, so waiting for an
answer does not immediately turn into treatment. The deferral applies only to supplying aid, not
receiving it, and changes no permission or item reservation. Other authorized material uses remain legal.

Stable offer identities bind world/branch, partners, exact use definition, preferred item identity/kind
and current/maximum patient HP. Reply identities bind the proposal and participants. Neither uses
clock, movement, unrelated owner revision or refreshed observation. Permanent occupied identities
suppress unchanged refused, declined or expired decisions across archive rotation and reopening.
There is no fresh-ID, alternate-lot or alternate-partner fallback from an occupied preferred choice.

These are bounded local preferences, not general NPC psychology. Configuration must be supplied again
on opening a session. Returning to an identical earlier semantic key does not authorize a new attempt;
recurring need episodes need an explicit future contract rather than a clock-based retry. The broader
world simulation and its full acceptance obligations remain intact.

## Evidence and source review

[Final checks](qualification/FINAL_CHECKS.json) pin the final build and executed affected tests.
The 23 focused cases include 11 preserved player-session cases and 12 new NPC cases; those cases are
also part of the 154-case affected regression and must not be added twice. All 154 distinct cases pass.

The new tests prove:

- A real ordinary-care world reaches source-stat defeat/recovery/victory, consumes the helper's raw
  dose, then crafts two doses from finite berries. The NPC independently offers an actual crafted
  dose. Player acceptance and exact received-dose self-use occur at later cutoffs. Production lineage,
  HP, time, stock reserve, current control, immutable receipts and exact SQLite reopening are checked.
- Wanted and unwanted player gifts receive the configured NPC's own acceptance/decline; absence of
  configuration leaves an offer unanswered despite existing care permission. Caller-owned policy
  collection edits and invalid configurations cannot create implicit control or consent.
- Offers-only and answering policies both make declarations alongside an invalid partner request.
  A genuinely active treatment permission and actual reach do not cause an outstanding gift to be
  auto-consumed. A separate real-state controller contrast checks supplier-only aid deferral.
- Real decline and expiry preserve the observed injury. The decline case fills the actual outcome
  window through session-selected create/withdraw commands and triggers one automatic complete archive
  cut. Movement changes observations; reopening restores reach by actual movement, verifies the same
  decision key and occupied identity, and produces no additional NPC command. Authenticated recorded
  lookup retrieves the archived decline. The expiry case independently checks later ticks/reopen.
- A player offers a real dose and then legally consumes it before the helper decides. The NPC's wanted
  acceptance receives a durable refusal exactly once; later ticks and reopening do not retry it.

One bounded native Astra critic, reused for relevant corrections, performed source review only.
It found three initial defects: unfiltered produced medicine; overbroad counterpart exclusion for
declarations; and incoming offers blocking an offers-only policy. The lead fixed all three. The critic
confirmed those corrections, then found that aid deferral incorrectly excluded the giver as a patient
too. A separate helper-only exclusion and a directionality regression fix that issue. The critic ran
no tests and made no runtime acceptance claim. Protected-output filtering is source-reviewed here;
this packet does not add a lived protected-output mutation protocol or claim such a fixture.

## Retained failures and corrections

- A build failed on a missing `OpenFantasy.Core.Care` import in a new test. The import was added;
  no runtime rule or assertion was relaxed.
- The first focused command retained the prior packet's `ScheduledMaterialProposal` filter and
  ran only its 11 old tests. That result remains historical compatibility evidence, not NPC coverage.
  The filter now includes `ScheduledMaterial`, and the final manifest requires the expected new cases.
- The first expanded run had 17 passes and five fixture failures: the bulk-stock fixture has no
  ordinary-care permission and retains a raw helper dose. The actual crafted-gift and active-aid
  tests now use the ordinary-care journey and real crafting. Archive/expiry tests explicitly assert
  the bulk fixture's absence of aid permission rather than claiming it was revoked. The spent-stock
  test uses the ordinary player-owned dose and explicitly revokes actual aid permission before its
  independent rejection exercise. No played world, stock, source definition or HP was rewritten.

All failed logs and original runs remain in qualification/. Every old test/fixture and frozen spec
is preserved. The existing 5-second test waits, build240s/test600s limits and all acceptance gates
retain their thresholds. No Core, client, canonical format or schema change is made.

## Preservation and remaining work

PRESERVATION.json records the clean pre-change checkpoint, accepted source revision and exact unrelated
19-file predecessor index. `codex/milestone-npc-material-decisions-20260929` is a backup pointer on
`codex/runtime-foundation`, not a checkout switch, push or merge. The same-change architecture pages describe policy, scheduling,
identities and permission boundaries in present tense.

The larger assignment's archived session-inspection API and pending-excavation/profile joins remain
unqualified. Current archived lookup uses the existing authenticated service outside frame callbacks.
Native proposal controls, production policy authoring, general NPC knowledge/motivation and recurring
need episodes remain required work. This packet has no new process-kill, native/device, human or
performance evidence. Control rollover, multiplayer, paired-profile composition, retained-save migration,
the original 520-dose journey and every full phase/production gate stay open.
