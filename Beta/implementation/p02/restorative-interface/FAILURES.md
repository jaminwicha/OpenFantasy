# Retained failures and corrections

- `qualification/build-01.log`: build failed with six nullable dereference diagnostics and two
  xUnit assertion-overload diagnostics in new test files. No gameplay, rule, threshold or test
  expectation is relaxed to correct compilation.
- Source review found the native runner's five routes include two distinct raw-self result saves,
  while the result verifier initially expected only four name rows. The verifier must require the
  complete supported fixture set and authenticate every row, including both raw input paths.
  The root and independent critic identified this before native execution. Neither result is dropped.
- `qualification/affected-01.log` / `.trx`:334pass,11fail,0skip across345affected cases in6m56s.
  All11failures came from the new full-dose helper/native fixture assumption: actual source-stat
  victory leaves21HP, so a40HP remedy has only39HP of headroom and the conservative helper correctly
  selects nothing. The new named fixture variant adds one actual player Wait before victory, retaining
  real wolf damage evidence and requiring livingHP1–20. Original fixture defaults, runtime policy,
  stats, remedy effect and assertions remain unchanged. Failed cases and their affected fixtures are
  rerun after compilation; passing unaffected cases do not need a ceremonial full-suite repeat.
- `qualification/build-03.log`: the additional actual combat fixture lacked its encounter namespace
  import. Root added the import; no behavior or assertion changed.
- `qualification/care-followup-01.log` / `.trx`:66pass,1fail,0skip across67cases. The sole failed
  player-crafted fixture reached a real post-combat gathering command that the Host rejected.
  `qualification/crafted-diagnostic-01.log` exposes the exact processor exception: `GatherPlanner`
  compared gathering's process boundary with advancing batches. Complete admission and progression
  correctly use `ProcessIntervals`, which includes elapsed tactical time. Root preserves WIP snapshot
  `fafeff2` on `codex/checkpoint-before-gather-clock-fix-20260928T030838Z`, then corrects the comparison.
  The actual fixture explicitly requires the two counters to differ, preflights its exact batch,
  and compares that candidate with the actual durable commit. Legacy clocks keep their property fallback.
- `qualification/native-restorative-01.json`: the first rendered pointer route committed its actual
  dose, then failed the unchanged health-cue assertion. `FarmGame` supplied only encounter HP to actor
  presentation, leaving exploration observations null. Root binds the controlled player's permitted
  progression HP across both contexts; other actors retain their existing public encounter boundary.
  No dose retry occurs in that failed save. The next native qualification uses fresh fixture copies.
- `qualification/native-restorative-02.json`: eight native processes passed through helper reopening,
  but the runner expected only the resume Escape input and forgot the final pause Escape in that same
  recorded stage. The assertion now requires exactly those two named inputs and still forbids any
  patient item action. Actual journal authentication independently requires the helper's own command.
  No runtime input policy, world effect or deadline changes; the interrupted route set is not a pass.

Later successful runs do not erase these failures or qualify unrelated release gates.
