# Care continuation and observed health feedback

This increment proves care across longer earned histories and adds restrained restoration feedback.
It does not change simulation rules, combat statistics, source rewards or existing save formats.

## Implemented and verified scope

The raw journey starts from exact production care admission. The crafted variant starts with the
existing finite-berry fixture and makes its Potion through an actual crafting command. Both use real
strikes, defeat, acknowledgment and a separately authenticated finite treatment. They finish the same
encounter, retire actual recovery debt, complete Wren's original funded delivery, receive 420 XP and
200 gil, refill at the earned level and attack in a second encounter using earned statistics. No
reward, level or post-treatment HP is injected. The original Strike costs zero MP; the test proves
level-based MP capacity/refill, not recovery of spent MP.

SQLite reopening preserves pending, treated, rewarded and final boundaries. Duplicate receipt reads
are stable, and duplicate dispatch cannot spend another dose or reward. Three corrupt copies test a
wrong lethal witness, deletion of a pending obligation detected by actual journal replay, and conserved
currency diverted from the funded reward. The good source saves remain unchanged.

The native probe can consume a closed backup containing actual pending care. A first process proves
paused recovery and focus loss/return without work. A second requires explicit unpause before helper
treatment and a real subsequent attack; a third reopens the completed state unchanged. Four recorded
Host reads join acknowledgment, treatment, source dose, clocks, positions and the resumed attack. The
initial acknowledgment in this path is an actual managed command; fresh native control journeys
separately prove acknowledgment through wheel, keyboard, controller and pointer events.

`FarmHealthFeedback` compares consecutive public HP observations. Damage retains its prior feedback;
a gain shows a brief mint tint and signed amount with no added sprite motion. Missing observations and
new views establish a baseline instead of replaying historical health changes. Presentation cannot
spend inventory, heal an actor, infer a medical cause or advance the world.

## Architecture and future work

The same-change [item-consumption reference](../../../Runtime/docs/architecture/item-consumption.md)
corrects an obsolete statement that durable care publication remained closed. The
[native encounter reference](../../../Runtime/docs/architecture/native-encounters.md) documents the
observation and feedback contract. The runtime README explains the reproducible pending-backup mode.
The existing architecture-maintenance hook remains mandatory; implementation history stays here.

[Ordinary care design](ORDINARY_CARE_NEXT.md) identifies the next real integration boundary: finite
consumption is reusable, but complete-world care is bound to acknowledged encounter aftermath. Its
profile26/typed-use/permission proposal is **not implemented**. Ordinary self/ally use needs its own
permission, supported delivery, timing, resource history, persistence and native interaction proofs;
active-combat use must pass through the due actor's action timing. Existing profile25 bytes stay valid.

## Evidence and limits

[Verification](qualification/verification.json), [review](REVIEW.md) and [failures](FAILURES.md) record
execution scope and corrections. The clean solution build has zero warnings/errors. All **295** affected
client/content/session/care tests pass, zero failures/skips. Rendered checks pass 11 native processes
and 20 recorded Host reads; the additional headless pending check passes three processes and four reads.
The earlier full Core/Hosting qualification is retained; unchanged authority is not claimed as rerun. The managed continuation is not a rendered quest/reward
playthrough.
Native care uses raw stock; crafted-stock native interaction remains open. Controlled background
renders and injected events do not qualify human usability, physical devices, foreground OS focus,
production art, exports, scale, multiplayer or full-phase acceptance.

The [restoration image](qualification/screenshots/pending.restoration.png) shows the signed cue;
[reopening](qualification/screenshots/pending.reopen.png) does not replay it. The nearby Wren nameplate
and cue remain visually crowded. Sprite art and layout are provisional; no style acceptance is claimed.

Before this change, checkpoint `codex/checkpoint-before-care-continuation-20260928T002007Z` preserved
`53e257493bdfa60979c62f4926e0ea8a8aca6eb2`. The verified milestone is
`codex/milestone-care-continuation`. All frozen specs, earlier milestone refs and unrelated predecessor
staging are preserved. No paid Claude request was retried and no credit savings are claimed.

Regular native artifacts are retained in [native-evidence.tar.gz](qualification/native-evidence.tar.gz)
and checked against [their manifest](qualification/native-evidence-manifest.json). Extract from the
packet's `qualification/` directory with `tar -xzf native-evidence.tar.gz` to restore the recorded paths.
The archive contains no checkout symlinks. Passing and failed-run managed pending exports are kept
separately in `pending-source-02/` and `pending-source/`.
