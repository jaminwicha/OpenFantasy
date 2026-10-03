# Orchestration remediation and integration plan

Prepared 2026-10-03 against `a9828333`. This is a provisional response to the owner's request for
immediate, medium-term and long-term remediation. **The separately commissioned orchestration report
has not been supplied or located.** None of its findings is attributed here. Its analysis remains a
required input before the expanded audit closes. This plan uses the retained project audit and a new
inspection of actual launch/admission code; proposed work is not an implementation claim.

## Evidence and diagnosis

The [progress audit](roadmap-audit-20261003/PROGRESS_AND_EFFICIENCY.md) establishes a large difference
between qualified development routes and the complete ordinary player experience. It records132 P02
packets and67 custom verifier scripts, alongside real integrated gameplay. These counts establish
maintenance surface, not its entire cost or a reason to discard evidence. The six measured full Core
runs consumed about11.4 minutes in total; that alone cannot explain a multi-day goal.

The [provider review record](roadmap-audit-20261003/FABLE_DISPATCH_RESULT.json) records one successful
456.468-second review,61 turns and a $5.7991455 list-price estimate. The [disposition](roadmap-audit-20261003/FABLE_DISPOSITION.md)
accepts useful findings and rejects concrete errors. Earlier dispatches include permission and
unfinished-verification failures. Neither this sample nor elapsed goal time measures subscription
savings, total integration effort or an intrinsic capability difference between models.

Fresh source inspection identifies a specific obstacle to the next integrated journey:

- [The launcher](../Runtime/tools/play.py) still defaults to the preserved construction farm and does
  not select the hosted shared-control world. Updating an entry flag alone cannot establish its admission.
- [Cargo creation](../Runtime/src/OpenFantasy.Hosting/Content/EncounterOutcome/CargoOutcomeWorldAdmission.cs)
  admits profile35, including its ordered one-to-four-player roster. The inspected ordinary Host creation
  commands use that admission. They do not automatically add the profile36–40 policies.
- [The explicit human-construction admission](../Runtime/src/OpenFantasy.Hosting/Content/HumanConstructionWorldAdmission.cs)
  composes those policies through profile40, but deliberately makes Wren the fourth human. It is a
  qualification scenario and cannot stand in for an inhabited world with Wren's autonomous work.
- [The hosted opener](../Runtime/src/OpenFantasy.Hosting/Networking/CargoPlayerHost.cs) can accept the
  applicable profile39/40 cargo roots. It opens an existing authority and authenticates players; it does
  not create that population or silently migrate profile35. The corresponding
  [native remote entry](../Runtime/client/scripts/Networking/NativePlayerEntry.cs) requires an invitation.
- [The local client selection](../Runtime/client/scripts/FarmClientSession.cs) has exact cargo/outcome
  profile guards. A new ordinary local selection needs an explicit compatible binding, not a weakened
  guard or a fixture argument repurposed as ordinary play.

The demonstrated delivery problem is therefore broader than subprocess dispatch: selecting and
accepting many bounded capabilities has not reliably produced an ordinary integrated entry with the
required population and player journey. The adapter already checks context, finite limits, denied
tools and terminal provider results. That observation does not prove the broader orchestration sound,
nor that its current adapter is sufficient for every future assignment.

## Immediate actions

| Action and owner | Concrete outcome | Acceptance boundary |
| --- | --- | --- |
| Integration lead: obtain and assess the separate report | For each finding, compare its cited evidence with current code/dispatches, distinguish corrected and still-current defects, and record accept/reject/unresolved with reasons in that report's disposition. | Report-specific assessment remains pending. Do not attribute our existing findings to its author or close unknown findings through a generic policy. |
| Integration lead: retain the corrected full roadmap as the work-selection authority | The next assignment names a player outcome, all required dependencies and the full phase rows it advances. Known production gaps stay visible. | Already established by CURRENT_ROADMAP and the audit corrections; actual implementation still has to follow it. |
| Runtime/content builder with lead integration: author the common fresh world and normal entry | Preserve the source NPC population and finite economy while composing the retained controller/recovery/permission policies; expose ordinary creation/opening and shared play without fixture setup. | The next delivery contract below must be satisfied. Existing saves keep their exact meanings. No new profile number is presumed necessary. |
| Lead and one bounded critic at the coherent integration boundary | Inspect the actual change and complete journey evidence, including ordinary launch and what remains unproved. | Critic findings require disposition; repeated agreeable summaries and a new full audit per small fix add no acceptance value. Do not dispatch a new paid review just to assess this provisional plan. |

The current checkpoint preserves all corrected documents and earlier failures. No provider request,
model switch, runtime-law change, gate relaxation or evidence deletion is required for this assessment.

## Next coherent delivery contract

Implement a reusable fresh-world admission over the existing cargo composition and policies, keeping
Wren as an NPC and supporting the admitted one-to-four human roster. Keep full starting resources,
health, costs and population semantics; do not import stress/calibration wounds, pre-funded quests,
grants or test-only setup. Recovery policy availability is separate from consent, permission and
actual treatment. Give new content/policy identities their stable canonical ownership.

Expose a normal discoverable development entry and named retained save. Continue old saves through
their existing explicit routes; refuse unsupported transitions. Use the existing C# authority and
player-session boundaries. A local entry must not add a second authority or require an IPC bridge.
Shared entry retains actual authenticated sessions and deliberate input/readiness transitions.

The acceptance witness starts from ordinary creation and actual controls: speak to Wren, gather and
craft, fund her original project, observe her autonomous work, buy finite goods, travel into danger,
resolve or retreat under the existing rules, receive permitted finite care where needed, sell retained
cargo and return to the changed place. Reopening must continue the same world and complete history.
Qualify shared participation in that same admitted composition, with existing access/reconnect gates
and clear role distinctions. Do not use another scenario's passed screenshot or direct authority
mutation as proof that this ordinary route is playable.

Before editing, declare affected versus inherited evidence from actual source/binary/fixture
dependencies, along with remaining required gates. After integration, inspect the rendered route and
seek bounded human feedback on action burden, travel, readability and onboarding. Human feedback is
still an open gate if unavailable. This increment advances LOOP and COOP; it does not close all of
P02. Deep NPC cognition/recruitment, mystery/puzzle, merchant travel, ecology and the rest of the full
closure table remain required follow-on work in this lineage.

## Medium-term remediation

1. **Deliver the missing P02 systems in the common journey.** Scope actor/narrative work around a real
   commitment, offscreen reconciliation, recruitment and changed-place consequence; connect testimony,
   the puzzle and traveling merchants to actual facts and goods. Follow the full phase dependencies.
   Assign native art/animation/audio against these actual actions and locations, keeping artists'
   production ownership separate from engine implementation. Approved concepts are not accepted kits.
2. **Consolidate verification incrementally.** Adapt one existing packet to a shared dependency/run
   description first. Demonstrate missing, failed, stale and incomplete evidence is refused before
   applying it elsewhere. Preserve original reports, exact deadlines and mandatory full gates. Bound
   this tooling work; wholesale migration of67 verifiers is not a prerequisite to gameplay progress.
3. **Reduce recurring handoff work.** Reuse maintained context recipes and the shared current handoff;
   give workers exact owned deliverables, relevant contracts and finite limits. Collect completion of
   required commands in the same assignment. Review selected diffs and evidence rather than repeatedly
   rereading entire transcripts. Pin facts once and link them instead of copying them into new reports.
4. **Make review proportional to consequential boundaries.** The lead integrates routine corrections;
   a distinct critic challenges the complete changed contract at useful checkpoints. Claims about
   preservation, native behavior, performance and human acceptance retain separate evidence. Treat a
   partial delivery as partial and finish its required work rather than shrinking its acceptance scope.

## Long-term remediation and measurement

Evaluate the next few comparable integrated deliveries before changing routing defaults. In each
existing result, record the accepted player outcome, delegated scope, returned scope, lead repairs,
defects found, provider request count and reported usage/elapsed time. Record lead active effort only
when actually measured; do not derive it from a four-day open goal, token-limit counters, cache reads
or wall-clock gaps. Missing measurements remain unknown. Avoid a separate telemetry service or an
invented credit-saving percentage.

Use those observations to retain delegation where independent deliverables repay preparation and
integration effort, and keep tightly coupled small changes local where that is more effective.
Reassess providers on returned work and allowed routes; preserve authorization, exact model/context,
Max-effort requests and no automatic paid retries. The purpose is reliable useful delivery with
lower total burden, not maximizing concurrent agents or provider calls.

At phase transitions and sustained multi-day drift, use the already maintained
[iterative audit](../../../GameDevAIEcosystem/skills/game-verification-loop/references/campaign-audit.md).
Review the complete phase exit, ordinary launch, unfulfilled content and the timeless reference
together. A clean scoped audit or successful process result never substitutes for production-scale,
three-platform, human art/play or full-campaign acceptance.

## Documentation review

- **Plan/spec:** supplements the living P02 execution sequence with a concrete common-admission dependency and proposed remediation; retains all frozen requirements, full phase exits and later owner decisions. The separate report is explicitly unresolved.
- **Reference:** runtime code and architectural contracts are unchanged; no timeless reference rewrite is warranted for an unimplemented proposal. Current-state observations link their actual source; new ordinary-entry contracts must update references with their implementation.
- **Accuracy/completeness:** inspected actual launcher, cargo admission, human-construction admission, hosted opener, local selection guards, remote entry and retained review/cost evidence. This is lead source analysis and a provisional plan, not a new independent critic or analysis of the unavailable report.
- **Checks:** the maintained documentation checker passed this work record and three affected living pages; whitespace checks passed. Runtime and frozen-spec trees remain identical to `a9828333`, and the exact19-file predecessor index hash is preserved. No runtime test or external request was run for this document-only assessment.

Before-change checkpoint `codex/checkpoint-before-orchestration-remediation-20261003T1615` resolves to
`a9828333b037f83c15cebd5d4cefe8b1d3369f8f`; the successor tree was clean before these document edits.
No unrelated staged file is included. This proposal does not close the pending report or implementation goal.
