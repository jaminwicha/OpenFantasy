# Solo adventure continuation

The bounded solo continuation is qualified through retained native input, exact command/receipt
matching and a separate paused reopening. This is progress within P02, not its completion or human
acceptance. Work is direct, as requested; no independent critic or external agent was invoked.
The previous unfinished record is preserved in [RESULT_WIP_20261003.md](RESULT_WIP_20261003.md).

## Integrated player outcome

The same earned ordinary adventure from [the opening](../adventure-entry/RESULT.md) continues at
Marla. Native keyboard input buys one ration for20gil, walks to the bandit camp, observes the warning
before contact, and meets the chief's own encounter. One Strike receives32HP of actual damage.
The player inspects/cancels/reopens/confirms the existing Retreat, walks its ordinary samples,
uses one Potion through the Care wheel for32HP, detours to Kegga, sells that exact bought lot for38gil,
and walks home. The final purse is218gil, HP78/78; both originally crafted rations and Wren's completed
construction persist. The Potion is consumed, not restocked on encounter exit or reopening.

This joins existing production systems without a new profile, world rule, save schema, permission,
fixture wound, granted resource or simulated trade. Bootstrap exposes only an explicit disposable
`--adventure-journey` proof route; ordinary launch is unchanged. The new continuation partial reuses
the existing road-threat actions and navigation. Its passive observer retains six exact native
command/receipt pairs; all six match the saved outcomes, with no retries or rejections. The original
source save, genesis and historical commit/outcome/occupied-identity prefix remain exact.

## Qualified evidence

- Focused client build: zero warnings/errors in `checks/build-draw-diagnostic-02.log`.
- Native active journey:395.827s within its690s probe/720s process bounds,18 actual post-draw captures,
  maximum observed capture wait0.014018s within the unchanged five-second bound. The original raw
  runner result is [native-draw-diagnostic-03.json](checks/native-draw-diagnostic-03.json); its overall
  failure is retained because its later whole-database-byte assertion was wrong, as explained below.
- Its native paused reopen restored the exact public hash and replayed the original complete history.
  A focused [row-level reopening diagnostic](checks/reopen-row-diagnostic-04.json) independently
  opens the returned save again:39.265s process, unchanged30s opening and30s history-await limits,
 90s outer process limit. All10 table definitions and schema version are exact; every row outside
  the operational fence remains identical, including8614 commits and9077 occupied identities.
  The fence advances exactly4→5 with its checksum independently recomputed; allowance rows remain empty.
- `checks/checker-qualification-02.log`:20 Python tests pass, zero skips, no resource warnings.
  Real retained positive evidence and corruptions cover failed/missing/incomplete/stale results,
  substituted cargo/supply identities, wrong return/funds, forged receipts, changed original history,
  missing/changed dependencies, changed world rows, extra fence advancement, wrong fence hash,
  altered allowance rows and immutable inspection without sidecars.
- `checks/adventure-regressions.trx`:9 focused managed admission/client/old-road-fixture cases pass,
  zero failures/skips. No unrelated full regression is claimed.
- [Final qualification](checks/FINAL_QUALIFICATION.json) pins current source, binaries, checker,
  retained evidence and the exact inherited claim.1283 execution dependencies from the successful
  row diagnostic remain byte-identical after the managed build. Only the checker changes afterward:
  explicit SQLite connection closure and preservation of the pre-reopen database. Its semantic
  checks rerun on the retained data; the native journey is not repeated solely for these tool changes.
- Lead inspected purchase, warning, care, returned-work and paused-reopening images. The whole chief
  and traveler appear clear of the HUD at warning, and the Care wheel shows the exact dose/cost/healing.
  The crowded/wrapping HUD, simplistic sprites and provisional terrain remain visibly below the
  accepted art/UI direction. Images are execution evidence, not visual polish or human acceptance.

## Failures and corrections

The original `checks/native-01.json` timed out waiting for post-draw at the road. Added counters record
process/render frames, post-draw events, native visibility/focus and elapsed capture time. The later
run did not reproduce it. Its cause remains unresolved; no forced rendering, deadline extension,
focus grabbing or waived reliability gate follows from the pass.

The diagnostic first failed compilation on the ambiguous Godot/System `Environment` name; the
qualification build uses `System.Environment`. A later preflight correctly refused empty SQLite
sidecars left by the earlier checker. Original database SHA256 still matched and no process held it;
a byte-identical closed copy was used. Immutable reads and explicit connection disposal prevent
checker inspection from leaving evidence sidecars or open handles. The first20-test run exposed
resource warnings; the second warning-free run qualifies the corrected closure.

The first complete native continuation and reopening passed their own checks, but the Python runner
incorrectly demanded identical whole SQLite bytes across a live host opening. Source review found
`StartControllerRecovery` → `BeginControllerRecoverySegment` deliberately advances the operational
fence. The focused diagnostic preserves actual before/after databases and proves that this exact
single increment/checksum is the only row change. The corrected checker still rejects any changed
world, history, schema, activation or allowance row. This is a correction to the fixture's assertion,
not a game-rule change or a weaker simulation/persistence guarantee. The original failure stays failed.

## Documentation review

- **Plan/spec:** CURRENT_ROADMAP and the handoff now distinguish the qualified solo joined route from
  shared participation, recruitment, meaningful autonomous changes while away, mysteries, traveling
  merchants, art/audio/UI, human acceptance and the complete original P02 exit. Frozen scope is unchanged.
- **Reference:** `Runtime/docs/architecture/adventure-entry.md` explains continuous ownership across
  trade/danger/retreat/care, exact-lot selection, the explicit saved continuation and the distinction
  between paused canonical state and a new live recovery fence. Existing road-threat, ordinary-care,
  controller-recovery, admission and command handlers were checked for consistency.
- **Accuracy/completeness:** local source/evidence review confirms one authority/profile40/schema12,
  no added gameplay mutation, original resources and no fresh NPC work during the absence claimed.
  The empty-allowance solo assertion cannot substitute for populated recovery or co-player cleanup.
  No independent critic, physical controller/OS-focus test or human/art/production gate is claimed.
- **Checks:** `checks/documentation.json` records the focused maintained document check. Link/field
  validation supplements the above factual review; native and managed evidence retain their own scope.

## Preservation and remaining work

Before-change recovery is `codex/checkpoint-adventure-journey-wip-20261003` (`a2a5a65c`). Before the
reopening assertion correction, `codex/checkpoint-before-adventure-reopen-validation-20261003`
(`26d90ffa`) preserves the active journey, original failed check and incomplete negative tests.
The accepted increment receives `codex/milestone-adventure-continuation-20261003` without switching
`codex/runtime-foundation`. The unrelated19-file predecessor index remains unchanged.

Next qualify shared participation in the joined journey, then deepen NPC motivation/memory/work,
recruitment and changed-place return. Native discovery/presentation and all broader phase, platform,
scale, migration and human gates remain in the roadmap. The captured journey is one authored finite
corridor, not a procedural frontier, balanced expedition, complete cooperative adventure or fun verdict.
