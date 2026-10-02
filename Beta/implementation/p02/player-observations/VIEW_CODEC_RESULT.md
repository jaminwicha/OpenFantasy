# Typed view codec implementation record

Status: bounded large-body compression builds and all 49 codec/compression cases pass. The actually
admitted 4,096-lot value regression passes. Root network qualification remains pending after two
fresh-player fixture failures and their correction. Earlier 62-case results below qualify only the
pre-compression source. This is not native-client, whole-game or maximum-composite-world acceptance.

## Delivered scope

- Versioned `PlayerViewCodec.Encode(LocalWorldView)` / `Decode(ReadOnlySpan<byte>)` under
  `Runtime/src/OpenFantasy.Hosting/Networking/ObservationViews/`, with a 900 KiB body limit.
- Explicit typed transport records, codec-local converters, bounded preflight, canonical re-encoding
  and identity/participant/geometry consistency validation.
- Value-only projection reconstruction and internal access to the exact proposal/market preparation
  captures already consumed by client actions. Existing diagnostic JSON attributes and public
  property order remain unchanged.
- Minimal `RestorativeAvailability.FromObservation` factory for validated read-only advice; no Core
  network dependency, authority state reconstruction or permission grant.
- Focused tests under `Runtime/tests/OpenFantasy.Hosting.Tests/Networking/PlayerObservationViews/`
  for ordinary/production regional round trips, default diagnostic byte parity, unsupported regional
  frames, all projection families, proposal hash and prepared gift/showing/quote/care/restorative/outcome
  intent parity, immutable collections, malformed fields and size/depth rejection.
- Timeless contract in `Runtime/docs/architecture/player-observation-views.md`.

## Boundaries and gaps

No player-session publication, delta/recovery, admission, native client connection or host scheduling
changes belong to this unit. The integration lead owns those paths and shared verification.
The synthetic all-facet test proves transport value preservation; it does not claim that its combined
facets represent a legal simultaneous world state. The production cargo case uses real admitted
projection values. Current view locality remains sight-complete, not general fog-of-war.

No Git operations, provider requests, subagents or uncoordinated heavy checks were used by this unit.

## Verification progress

The integration lead's first serialized solution build was terminal failed with four CS8602 nullable
warnings in the new test file; source stability was verified. Evidence:
`qualification/build-20261002T082917174345Z`. The four dereferences were corrected. No runtime
compile errors appeared in that result. A second coordinated check is pending; the first failure is
retained and is not rewritten into a pass.

The following hardening accompanied that repair: streaming duplicate-property detection, initialized
and exact container counters, existing 64-actor/16-region limits, regional-window/nonoverlap and wet
column vertical-envelope checks, and normalization of derived-getter overflow into `FormatException`.

The second coordinated solution build passed in 17.36 seconds on pinned current source, including the
integration lead's frame/replica/session tests. Evidence:
`qualification/build-20261002T084129542724Z`. Focused observation tests are running under the lead's
serialized checker; no test pass is claimed before its terminal result.

The first focused integrated observation check was terminal: 51 passed, 5 failed in 8.18 seconds,
retained in `qualification/observations-20261002T084200192068Z`. Two direct codec tests failed: real
production regional geometry exceeded 900 KiB as textual boolean arrays, and the paid-acceptance
assertion compared `SpotAcceptance` object identity. Two transport failures shared the same body-size
cause; a separate root actor-scope test fixture was incoherent before reaching its intended check.

Before the encoding adaptation the lead preserved
`codex/checkpoint-observations-before-packing-20261002T084343Z` at
`bf00a1422b4dd8233dd5215146b0b6200eb683fd`, with the real index unchanged and the failure retained in
`PACKING_CHECKPOINT.json`. The codec now packs occupancy bits with an explicit count, exact byte length
and zero padding, preserving complete geometry and the 900 KiB limit. Six additional hostile cases
cover length, padding, negative/oversized/missing count and noncanonical base64. Paid-acceptance parity
now compares exact canonical command bytes. The production test reports its encoded byte size.
A fresh coordinated build/test result is pending.

## Pre-compression terminal evidence

- Solution build passed in **16.02 seconds**, pinned current source:
  `qualification/build-20261002T084630695439Z/report.json` and `command.log`.
- All **62 focused integrated observation cases passed in 5.68 seconds**:
  `qualification/observations-20261002T084741455684Z/report.json`, `checks.trx` and `command.log`.
  This includes all 27 codec-unit cases and the lead's observation transport/replica/session cases.
- Actual production regional body: **97,122 bytes**, reported by the production projection roundtrip
  test. Terrain occupancy, region bounds, ground kinds, opening depths and water were retained; the
  body limit remains 921,600 bytes and default diagnostic JSON retains boolean occupancy arrays.
- These results closed the initial packed-occupancy unit before the later capacity follow-up below.
  They do not qualify compression changes. Original failures and both earlier repair stages remain
  recorded above; the lead owns integration and further acceptance disposition.

## Capacity regression and follow-up

The critic's size concern was reproduced on an actually admitted 4,096-lot saved-proposal world in
`qualification/observations-20261002T085205847886Z`. The 97,122-byte ordinary production regional
case therefore did not establish capacity coverage. The failure is retained. Before this encoding
adaptation the lead preserved `codex/checkpoint-observations-before-capacity-20261002T085257Z` at
`e3e74af28b4cbcd508d038c67ebd3bfe7d36fd60`.

The follow-up keeps canonical JSON unchanged for bodies at or below 900 KiB. Larger bodies use an
explicit versioned `OFVB` header and fixed-setting Brotli, still inside the same 900 KiB wire limit.
The new separate expanded JSON cap is 16 MiB. This matches the complete-world codec's byte budget
as a resource choice, not as proof that every composite maximum admitted world fits the observation.
The added expanded/canonical buffers and Brotli working memory are explicit new memory costs; no
unchanged-memory or maximum-performance claim is made.

The encoder now writes through a bounded `IBufferWriter<byte>` so serialization cannot first create
an unbounded intermediate array. Output is capped at 16 MiB, with a fixed 128 KiB buffer reservation
margin for writer span requests. The decoder validates the declared length before allocation,
requires exact compressed-byte consumption and expanded-byte count, then compares expanded JSON to
its typed canonical encoding. It does not require platform-dependent Brotli bytes to equal local
recompression. Small JSON inside a compressed wrapper is refused.

Twenty-two new compression cases cover lossless large-view/prepared-lot parity, alternate valid
Brotli encodings, small-wrapper refusal, malformed headers/streams/JSON, truncated/trailing/concatenated
streams, oversized and mismatched declared lengths, expansion bombs, incompressible wire overflow,
the serialization-time expanded limit and the exact bounded-writer limit. The lead separately owns
actual admitted maximum-lot and authenticated in-memory/socket delivery regressions. At that checkpoint fresh terminal
results were pending; the earlier 62/62 result does not qualify the compression changes.

## Compression follow-up terminal evidence

- `qualification/build-20261002T090103915714Z/report.json`: current compression source built in
  **18.18 seconds**.
- `qualification/network-20261002T090131964162Z/report.json` and `checks.trx`: **145 passed, 2 failed**.
  All **49 worker codec/compression cases passed**, including every added malformed-stream and
  bounded-writer case. The actual admitted 4,096-lot value regression passed with a **15,440-byte**
  encoded body, retaining the complete inventory and preparation data.
- Both remaining failures are in the lead's new authenticated-delivery fixture. Its fresh authored
  player admission ID differed from the retained empty resource-history anchor. The lead corrected
  the fixture by explicitly reanchoring its proven-empty fresh history, preserving stock/resources
  and rule checks. A fresh build/network check is pending; no delivery pass is inferred from the
  earlier failing fixture or from codec-only parity.
- The lead reports that the independent focused compression review found no new actionable defect.
  This does not replace unfinished integration qualification.
- Worker runtime/test sources remain frozen after these terminal results. Only this record was
  updated. No further runtime implementation is requested within this bounded follow-up.

The demonstrated 4,096-lot case closes the reproduced inventory/proposal capacity regression at the
codec-value boundary. The unchanged 900 KiB wire limit and new 16 MiB expanded JSON cap still require
separate coverage for other maximum composite combinations; their complete admission coverage and
maximum runtime/memory performance are not claimed here.

## Final lead network validation

After the explicit fresh resource-anchor correction, `qualification/build-20261002T090324704833Z/report.json`
passes in 11.71 seconds and `qualification/network-20261002T090357817887Z/report.json` passes all
147 cases in 8.77 seconds. This includes all 49 codec/compression cases and actual authenticated
local plus loopback WebSocket delivery of the admitted 4,096-lot world. The original failed fixture
checks remain above. The lead owns retained-regression, preservation and final milestone disposition.
