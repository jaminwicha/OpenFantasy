# Encounter completion and deliberate continuation

Outcome: implement a complete, separately interpreted encounter-outcome continuation across portable
Core, durable Host and native client. An encounter must not remain stuck because its initiator is
incapacitated. This is actual implementation/testing, not another research-only deliverable.

Owner authorization: substantial bounded Opus5.5 work is approved. Exact `claude-opus-5-5[1m]`,
requested Max, 7,200 seconds/$40 client limit. No recursive agents, automatic paid retry, model fallback,
package/toolchain changes, Git/index/branch operations or desktop focus takeover. Astra owns integration,
independent critique, checkpoints and commits. You are not alone; preserve unrelated predecessor work.
Base is accepted `cdeb919081ee8fe0ad9533f0ce2cd85fab4e2a43`; IMPLEMENTATION_CHECKPOINT preserves it.

Read shared AGENTS/handoff, INVESTIGATION.md, the immutable architecture input snapshots named in
INPUT_SNAPSHOTS.json, current world-composition/pacing/encounter/care contracts, and relevant baseline
Wait/co-op/time/death requirements. Do not reload the research archive. Begin with DESIGN.md as an actual
owned write, confirming edit access. Explain the selected rule, owner(s), profile/codec interpretation,
human readiness, care interaction, old-format preservation and verification before implementing.

## Required behavior

1. Separate world-owned zero-duration encounter completion from controller-owned outcome reading and
   acknowledgment, and from voluntary bodily actions and actual care. Completion cannot require a
   living initiator or be granted to an arbitrary bystander. Prefer the smallest cohesive typed owner
   and operations that express these distinct facts; no property bag, duplicate canonical state,
   UI-only unlock, free self-release impersonating an NPC, or survivor-only workaround that still
   strands the zero-survivor case. If a materially better implementation meets every contract, document
   why. The result must remain extensible to the specified 1–4 players and world simulation.
2. Resolve from actual combat/body/resource state, with the exact episode/action boundary. Finalization
   earns zero time and preserves bodies, position, HP/MP, readiness debt, possessions/source intervals,
   history, exposure/carry, scenario and episode identity. It neither issues rewards, deletes bodies,
   resurrects, teleports nor advances another actor's choice. Incapacity at HP0 is not a declaration of
   full biological death, corpse/succession/ghost behavior or game-wide resurrection rules.
3. Full Wait survives outcome inspection and replay. Human acknowledgment is authenticated to the
   relevant controller even when its actor cannot act; it grants no bodily action. Required humans
   cannot be silently acknowledged by a host. A fresh two-human managed fixture must contrast one
   acknowledgment with all required acknowledgments, disconnect/not-ready holds and an unrelated
   bystander. No network or physical-device acceptance is implied by that local managed contrast.
4. Victory has an explicit readable continuation to lawful exploration and later ordinary movement/
   inventory use with retained debt. Defeat must show why ordinary action is unavailable and offer
   only genuinely admitted/available care or an explicit save-and-leave/terminal choice. Do not leave
   an unexplained disabled Continue. All-positive-sides incapacitated must also have a stable readable
   result and deliberate terminal/available-care route. Leaving never resets/erases/overwrites the save.
5. Preserve the real care discipline: acknowledgment, standing permission, capable helper commitment,
   exact owned dose and bodily effect remain distinct. Existing profiles retain their original
   Outcome-care-resume-same-fight law and exact bytes. In the new interpretation specify and test
   whether care resumes an unresolved episode or occurs after a resolved episode; never silently
   resume a completed fight or let finalization discard a pending obligation. No invented helper,
   stock, automatic heal, immunity window or reward may make a demonstration pass.
6. Version the new interpretation explicitly (a new profile and only necessary owner/action codecs
   and schema dispatch). Do not change the meanings/bytes of accepted profiles1–34 or edit their
   journals. Fresh selection is explicit, e.g. `--cargo-outcomes`; existing native selections keep their
   profile. State the full non-throwaway integration path and retained-save migration obligation.
   Reject incompatible saves rather than upgrading them implicitly. Original source actors/statistics,
   content receipts, cargo, retreat, care, histories and atomic publication must remain integrated.
7. Prove actual entry, resolution, completion, acknowledgment and continuation in real committed
   sequences; reopening at each meaningful boundary preserves outcomes/receipts/readiness without
   rerolling. Include forged/wrong controller, side-zero, wrong/stale episode/hash/revision, changed
   content under the same command identity, exact retries, premature Active release, occupied identities,
   repeated finalization, pending care, debt/carry and positive/negative outcomes. New codecs/owners
   need actual canonical/malformed/admission/continuation obligations; do not rely on happy serialization.
8. Native production controls must show accurate outcome/reason, pause for reading, choose a lawful
   continuation, and move/use retained inventory afterward. Demonstrate actual win and actual loss,
   available/unavailable-care states as applicable and separate reopening. Use admitted declared fresh
   calibrations when necessary (e.g. a wounded chief) and label them honestly; never fabricate lived
   history or claim production balance. Authenticate complete saved journals and exact native commands,
   with rendered images and actual post-draw visibility. Existing road and retreat behavior remains.

## Ownership

You own necessary changes within Runtime/src/OpenFantasy.Core/{Worlds,Encounters}, a new dedicated
encounter-outcome domain folder if needed, Runtime/src/OpenFantasy.Hosting/{Worlds,Simulation,Content},
and Runtime/src/OpenFantasy.Host application entry/fixture commands. Preserve unrelated rules. You own
needed client application classes, new client/scripts/EncounterOutcome/, new NativeEncounterOutcome.tscn,
new tools/check_native_encounter_outcome.py and tools/check_encounter_outcome_durability.py, and affected
Runtime README and architecture references. Keep C# modular, object-oriented and human-readable.

Add new Core/Hosting tests under Encounters/EncounterOutcome, Worlds/EncounterOutcome,
Simulation/EncounterOutcome, Content/EncounterOutcome or Client/EncounterOutcome. Existing test/support
files are protected byte-for-byte. Only additive new pure-client Compile links may extend the Hosting
test project. No old probe/checker/scene changes, no assets/content source changes. Do not edit input
snapshots, frozen specs, wrappers/pins, handoff/AGENTS, predecessor, ecosystem or old evidence. Shared
Runtime code is exclusively yours during this assignment; Astra works on independent analysis.

## Verification and transport

Use Read/Glob/Grep/Edit/Write and only exact permitted commands:
`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/encounter-outcome-continuation/run_checks.py MODE`
Modes and actual finite bounds are in the immutable wrapper. No shell listing/reading, pipelines,
sleep, parent-temp scanning or broader shell grants. A denied required operation is a reported gap,
not permission to work around it. Adapter foreground960s transports checks; it extends no gate.
Observe terminal results before returning and serialize heavy checks via the wrapper lock.

Required coherent checkpoints: build; outcome-unit; core; regression; retained-auth (early after
public/persistence changes); outcome-fixtures; outcome-native; outcome-auth; outcome-durability;
fixtures; road-native/road-auth; native/native-auth. Run targeted outcome tests while developing, then
one coherent integration suite. Test-only changes need explicit dependency comparison, not automatic
invalidation of unchanged rendered journeys; never call stale affected evidence current or waive a
required gate. The lead will inspect actual dependencies and findings. Preserve every failed attempt.

Fixture conventions: `NativeEncounterOutcomeFixtureTests` exports a closed fixture manifest and actual
saves under OPENFANTASY_NATIVE_ENCOUNTER_OUTCOME_FIXTURES. Native checker receives --dotnet, --godot,
--fixtures, --output, --rendered; reports status=passed only on success, records execution/artifact
hashes, and returns managedAuthenticationManifest for `NativeEncounterOutcomeResultTests` through
OPENFANTASY_NATIVE_ENCOUNTER_OUTCOME_RESULTS. Keep existing native limits: opening30s, keyboard720/750s,
other routes300/330s, reopen90s, aggregate900s. No automatic input/command retry or mocked native proof.

`EncounterOutcomeDurabilityFixtureTests` exports under OPENFANTASY_ENCOUNTER_OUTCOME_DURABILITY_FIXTURES;
check_encounter_outcome_durability.py receives --dotnet, --fixtures, --output. Exercise real process kills
before/after publication at the new acknowledgment/finalization boundaries and any new schema/owner,
exact replay and continuation. Preserve the unchanged per-operation30s and aggregate600s bounds.
It records actual killed PID/exitCode=-9/boundary in processKills; result authentication reads
OPENFANTASY_ENCOUNTER_OUTCOME_DURABILITY_RESULTS in EncounterOutcomeDurabilityResultTests.

Return RESULT.md with changed responsibilities, chosen laws, actual terminal evidence paths, exact
old/new compatibility, source calibrations, retained failures, remaining gaps and observed friction.
Do not claim full combat/phase/production acceptance, balance, migration, human art/UI or credit savings.
Useful complete progress with honest limits is preferable to a success label over missing contracts.
