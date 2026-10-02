# Observation boundary review

Independent native critic: `/root/shared_sessions_critic`, read-only, bounded to 20 minutes.
The review covered typed projection/privacy, captured action terms, framing and replica recovery,
publication/backpressure/lifetime, and the new tests. No external provider request was made.
The critic inspected source and retained fixtures; the lead executes the reproductions and checks.

## P1: admitted inventory capacity

Core and retained `ConsentedWorldLotBoundTests` admit exactly 4,096 raw lots. An observation duplicates
eligible lots in its inventory and saved-proposal preparation. Even minimal JSON spellings of 3,000
such pairs exceed the 900 KiB body bound. A subscribed session therefore closes on a valid world.
The terrain bit-packing repair alone cannot solve this inventory defect.

The additive `PlayerObservationCapacityTests` reproduces this with the existing finite-source fixture,
fresh saved-proposal admission and successful complete-world codec roundtrip. The original test fails
at `PlayerViewCodec.Encode`; its 4,096 inventory count and nonempty preparation assertions pass first.
Report: `qualification/observations-20261002T085205847886Z/report.json` (62 passed, 1 failed).
Checkpoint before repair: `CAPACITY_CHECKPOINT.json` / `e3e74af28b4cbcd508d038c67ebd3bfe7d36fd60`.

Implemented resolution: preserve the 900 KiB encoded wire bound while adding a bounded lossless large-body
form and a separate 16 MiB expanded-JSON budget. This is an explicit new decoding/memory budget, not a
claim that the earlier JSON limit was sufficient. Validate exact decompression length/consumption,
malformed/truncated/oversized input, all retained fields and actual authenticated local/socket delivery
at the admitted inventory bound. The expanded budget matches the existing complete-world codec's byte
budget as a resource choice; it does not prove every combination of maximum world facets fits.

## Cadence clarification

The 100 ms bound applies to generation/enqueue. A delayed transport can deliver consecutive queued
observations less than 100 ms apart after backpressure clears. Documentation now states this precisely.
One pending observation and the existing send deadline still bound work; equal spacing of TCP delivery
is not an acceptance claim. This was a documentation ambiguity, not a required pacing-rule change.

## Other findings and limits

The critic found no additional concrete privacy, action-preparation, decoding or lifetime defect.
This is not proof of production scale, cross-platform Brotli behavior, native Godot clients or human
acceptance. The capacity adaptation receives a focused follow-up review because it introduces a new
decompression boundary. Final disposition belongs in `RESULT.md` with the actual terminal evidence.

## Focused compression follow-up

The same independent critic reviewed only the new compression/writer/canonical-decoding boundary,
bounded to eight minutes. It found no new actionable defect: size checks precede allocation, exact
consumption and output checks reject truncated/trailing/oversized expansion, and canonical validation
uses expanded JSON. Its pre-build missing-import finding was fixed before compilation. It ran no tests.
Lead network run `qualification/network-20261002T090357817887Z/report.json` passes all 147 cases,
including successful actual local/socket delivery at the reproduced 4,096-lot bound. The P1 regression
is closed for that demonstrated case. Other maximum composite worlds and production resource budgets
remain unqualified; no blanket capacity claim follows from this repair.
