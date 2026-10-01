# Native trading refinement integration

Accepted as a bounded client and evidence refinement on 2026-10-01. The independent Opus critique
preceded its edits; Astra reviewed the resulting code, checks and representative rendered screens.
This does not close the full native interface, cargo circuit, P02 or production gates.

## Result and review

Keyboard and controller probes now navigate from the focus established by the game. Every route
opens, reaches, cancels and selects dropdown rows using its own input; the former keyboard substitution
is removed. The review exposed an actual controller defect: B did not dismiss an open list. The
panel now handles its own popup's Cancel event without choosing a row or closing the stall.
An enabled confirmation can be traversed without sending an action. The unchanged five-command
verifier authenticates the completed buy/show/sell journey and its separate paused reopen.

Native keyboard contrasts cover expiry, insufficient funds and a moved issuer. Fixture preparation
uses real scheduled actions and the authored offer lifetime. Journal authentication reconstructs the
state at the recorded attempt, checks the intended condition and the single lawful recovery acceptance,
and verifies exact goods and currency changes. A disabled confirmation submits nothing: these are
client refusal proofs, not server rejection receipts. The attempt follows the sampled state and a
screenshot; it is not an atomic sample of a hardware press. Expiry and funding refusals are brief;
the independent paused-reading journey supplies the longer reading proof.

The merchant-and-commodity guard now covers offer lookup, review, confirmation and history. It prevents
labelling a merchant's quote for other goods as this stall's offer. Same-merchant/same-commodity stalls
still need an explicit market identity and selection; this remains a one-stall binding.

Ordinary cards show price, total, purse, shortfall and remaining running time. Step diagnostics stay
in hover text. Single berries are named correctly, and a completed purchase/sale reports Done.
Astra inspected controller buy review, pointer sale completion, unfunded refusal and moved-issuer
recovery at 960×540: the displayed controls and explanations fit without clipping in these images.
This is a readability inspection, not human aesthetic or usability acceptance. The old request line
can still say that terms were offered after the outcome; the Done card and completion notice state
the result. Localization needs authored grammatical names rather than the limited English suffix rule.

The source review covered the input driver, popup cancellation, quote guard, confirmation/card changes,
boundary preparation/probe/authentication and source-to-architecture descriptions. The new cancel
probe compares selection, quotes, lots, showing and pending action instead of a whole-world hash while
time runs; it retains the command-sensitive invariants without asserting that a running clock freezes.
Pointer motion now carries relative motion/velocity; it remains injected engine input, not hardware.
Compact public-view observations retain hashes and relevant facts; canonical databases remain available.

## Verification and preservation

[FINAL_CHECKS.json](FINAL_CHECKS.json) records the lead's independent evidence audit. All nine final
reports match the current **1,608 source files and 14 binaries**. The audit checks report dependencies,
actual TRX names/outcomes, tool/source/fixture pins, native artifact hashes, final database bytes and
process exit/deadline results. It reuses completed execution evidence rather than rerunning unchanged
suites. Build and **459 distinct managed cases** pass: 456 affected regression cases include the
18 focused and three fixture cases; three additional native journal authentication cases are distinct.
Core was unchanged and not rerun.

Trading uses six native processes (three routes and three reopens); boundary contrasts use three;
the unchanged gift regression uses six and its original all-route journal verifier. All 546 existing
test files, 707 Core files, protected Host/application files, 23 frozen specs, the original 520-use
database and the unrelated predecessor index remain exact. No test-project link was added.

[RESULT.md](RESULT.md) retains the original failures. Three Godot imports reported an uninstantiated
EditorSettings/android SDK path; identical reruns passed, but the cause remains uninvestigated.
An earlier pointer injection and an overly broad running-time cancel assertion failed before repair.
A passing regression whose source changed during execution remains rejected; the accepted final
regression and other checks share the final source. No deadline or acceptance assertion was waived.

Physical controls, OS focus, controller disconnect hardware, human comprehension, art/audio, shared
multiplayer Wait, archived-only outcome discovery, lived-save migration, multi-stall identity/choice,
the cargo/escort circuit and all full phase/production gates remain open. Boundary routes are keyboard
only. The existing [cargo dependencies](../spot-exchange/CARGO_DEPENDENCIES.md) remain the next coherent
application direction; this refinement does not prove broader geography or a complete economy.

## Delegation and checkpoints

[DISPATCH.json](DISPATCH.json) records the actual terminal request: exact `claude-opus-5-5[1m]`,
returned canonical Opus 5.5 and one-million context, requested Max, verified synchronized context,
3,600-second/$25 client-estimate limits. It completed in 2,511.901 seconds with a **$14.2986962 client
estimate**, no permission denials and no substitution or model retry. This produced useful code,
native evidence and independent critique; it is not proof of net credit savings or actual billing.

The original foundation remains at `codex/milestone-native-trading-foundation-20261001`.
Before-change checkpoint `codex/checkpoint-before-native-trading-refinement-20261001T071027Z`
resolves to `35b0797fa78ac73557ad4ed704af72f12c8bfd49`. The prepared WIP checkpoint is
`codex/checkpoint-native-refinement-dispatch-20261001T071324Z` at
`a832835e4d3cd2f6cba7a808481368cdcc41cc20`. The [delivery snapshot](DELIVERY_CHECKPOINT.json),
`codex/checkpoint-native-refinement-delivery-20261001T075706Z`, resolves to
`b0e756dab2b71a59944c5156da25a8fdcff91fb3`. Both WIP checkpoints exclude predecessor staging and
are preservation records, not acceptance claims. The accepted increment is preserved by
`codex/milestone-native-trading-refinement-20261001` after the integration commit.

Architecture and runtime instructions describe the same contracts in present tense. Upstream
aggregate-verifier guidance was already synchronized in ecosystem commit `a557436`; see
[UPSTREAM.json](UPSTREAM.json). No new cross-project policy or provider switch is implied.
