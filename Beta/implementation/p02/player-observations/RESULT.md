# Player observations

Qualified bounded observation transport on `codex/runtime-foundation`, preserved by
`codex/milestone-player-observations-20261002`. [Final checks](FINAL_CHECKS.json) bind the source,
binaries, checker, retained cases, checkpoints and preservation evidence. Complete multiplayer and
all original campaign/phase/production gates remain open.

## Implemented boundary

Authenticated channels explicitly subscribe to immutable player views. The host-selected actor and
regional observer determine scope. Typed reconstruction retains all existing view families, regional
geometry and private captured terms needed to prepare gifts and trades, including exact grant hashes.
It creates no private world root or gameplay authority. Default diagnostic JSON retains its existing
fields/bytes, including its intentional regional omissions.

Connection-local visible revisions, bounded prefix/suffix deltas and full recovery never use private
world commit counters. The replica publishes view/revision together only after complete validation;
invalid, foreign, stale or missing-basis input retains its last coherent snapshot. Actual committed
actions/movement, lost application frames, recovery and unchanged-view suppression are exercised.
Generation/enqueue is bounded to 10 Hz with one observation queued/in flight. Slow output cannot hold
host commits or input processing; existing queue, rate, send-deadline and teardown limits remain.

Occupancy uses lossless packed bits. Bodies above 900 KiB of canonical JSON use a versioned lossless
Brotli representation, retaining the **900 KiB wire cap**. The **16 MiB expanded JSON cap** is a new
explicit decoding/writing budget, with fixed 128 KiB writer reservation headroom. Exact decompression
consumption/length and canonical expanded values are required; local Brotli bytes need not reproduce
another implementation's compressed spelling. Extra buffers/working memory are real costs, not a
claimed optimization of peak allocation. An incompressible body that cannot fit is still refused.

## Verification

| Check | Result | Elapsed / unchanged limit |
| --- | --- | --- |
| Solution build | Passed | 4.25 / 240 s |
| Networking + observations | 147 passed | 8.30 / 120 s |
| Affected retained projections, action preparation and restorative behavior | 78 passed | 10.52 / 600 s |
| Core restorative resource evidence | 3 passed | 2.68 / 120 s |
| Shared-player host behavior | 9 passed | 38.56 / 600 s |

There are **237 distinct passing cases**, including **87 new observation cases**, all 60 prior
network cases and all nine prior shared-player cases. The actual production regional view is
97,122 bytes. An actually admitted 4,096-lot saved-proposal world roundtrips completely in 15,440
encoded bytes and reaches its authenticated player through both in-memory and real loopback TCP
WebSocket transports. These sockets do not exercise HTTP Upgrade or TLS.

The [independent critic](CRITIC_REVIEW.md) exposed that admitted inventory capacity defect before
acceptance. Its original red regression is retained; a focused follow-up reviewed the added compression
boundary. No further concrete defect was found. The lead reviewed actual code/evidence and ran the
checks. No provider request, provider fallback or credit-savings claim was made.

All 645 distinct protected files remain exact, including 23 frozen specs and the prior tests/fixtures.
The unrelated 19-file predecessor staging retains its exact binary diff hash. The 93 local links in
the five affected architecture reference pages resolve. Pure C# application/runtime names remain
`OpenFantasy`; no authority schema, gameplay rule, content or original acceptance threshold changed.

## Retained failures and checkpoints

- `build-20261002T082917174345Z`: four nullable dereferences in new codec tests; explicit nonnull
  assertions repaired the test code. The runtime compiled.
- `observations-20261002T084200192068Z`: 51 passed, five failed. Production occupancy JSON exceeded
  the wire-body limit (one direct and two transport failures); packed bits retained every cell.
  Two new fixture assertions were corrected: paid acceptance uses canonical command bytes instead
  of object identity, and the foreign-actor replica test supplies a coherent foreign projection.
- `observations-20261002T085205847886Z`: 62 passed, one failed. The critic's admitted 4,096-lot
  reproduction exceeded the body limit; bounded lossless large-body encoding repairs that defect.
- `network-20261002T090131964162Z`: 145 passed, two failed. New network capacity fixtures changed
  the fresh admission identity but retained its old empty resource-history anchor. They now assert
  fresh/unearned history and reanchor it explicitly. Production admission checks remain unchanged.

The clean pre-change ref preserves `cc073fdf`. `PACKING_CHECKPOINT.json` preserves WIP `bf00a142`
before packed geometry; `CAPACITY_CHECKPOINT.json` preserves WIP `e3e74af2` before compression.
Each snapshot excludes unrelated predecessor staging and is a recovery point, not an acceptance claim.
The final build/network/retained checks above run on identical current source and binaries. The earlier
failed `observations` pointer stays as historical evidence; the final networking selection includes
all observation cases and is the complete current-source result.

## Scope and next dependencies

The new Core availability factory only reconstructs read-only advice; the existing authority still
reevaluates every intent. Existing view construction and diagnostic serialization retain their behavior;
new reconstruction seams and codecs receive explicit parity/boundary tests. No native scene, input,
art or device behavior changed. Earlier full-game/native evidence is not reissued against these new
binaries; this checkpoint claims the affected checks above only.

Next dependencies are authenticated trade inquiries and historical quote/proposal inspection,
HTTP/TLS binding, the actual Godot client/session and scene-metadata boundary, solo envelope parity,
native 1–4-client validation and recorded disconnect grace/controller handoff. Complete combinations
of maximal world facets, ongoing world growth, bandwidth/allocation/latency across four clients,
Windows/macOS/Linux qualification and all full phase/campaign/art/human gates remain open. The
4,096-lot proof does not establish that every maximum composite view fits either encoding budget.
The full plan and preserved specification remain authoritative.

Final source hygiene removed surplus end-of-file blank lines in four new DTO files. The complete
affected selection was rebuilt and repeated on those exact final bytes; the table and FINAL_CHECKS
point to that terminal run. All earlier reports remain preserved.
