# Movement-derived terrain preparation

`MovementTerrainFootprint` derives a conservative read volume from an actual immutable actor, cardinal
displacement and ground-support interpretation. The existing file-backed terrain preparation test now
uses it instead of a hand-authored rectangle. Ordinary stair movement and preparation share the same
step-height calculation; movement rules, codecs, world profiles and default launch are unchanged.

The footprint includes body clearance across the entire sweep, the highest stair candidate, and support
below a descending endpoint. That endpoint can be one step below the origin and its support band a
second step lower. Existing signed cell floor/ceiling conventions and half-open body extents are retained.
It is intentionally conservative: some covered cells belong to candidate heights the query may never
need. It is not a minimum-read or performance claim, and missing required terrain still refuses preparation.

## Verification

[Final Core run](check-02/core.json): 157 cases pass. [Final Hosting run](check-02/hosting.json): three pass.
The 160 distinct cases include ten new footprint cases and the complete selected spatial family plus
terrain-read plans/loading. Invocations take 6.361 and 15.088 seconds under unchanged 180-second bounds;
builds have no warnings/errors. The initial check failed to compile the new test file because of a
missing spatial namespace import. It is retained in `check-01/core.log`; no runtime test ran in that
attempt. The import was fixed and both named selections completed; no gameplay rule or test gate changed.

New cases compare actual `LocalSpatialSnapshot.PrepareMove` on a complete terrain region with the same
query on an exactly cropped, encoded, pinned and decoded footprint region. The crop preserves generated
source IDs, remaining solids and excavation state for every included cell. Cases cover all four cardinal
directions with odd body extents; genuine stair ascent/descent; the legacy constructed-support refusal;
negative elevation; and maximum admitted 2048-subunit body axes with 256-subunit motion. The maximum
stepped case requires a 9×8×10-cell request. Invalid motion/body envelopes and numeric overflow refuse.

The real file-backed loader comparison still loads exactly two required archives and omits the third.
Its actual actor's derived rectangle is the same 12-cell request as before; all supplied actors remain
present in the comparison. That proves terrain preparation and query agreement, not partial admission
of a live world, a directory transaction, offscreen actors or a full partition transfer.

## Scope and next work

The footprint retains its actual source actor, but does not authenticate freshness, controls, locomotion
permission, neighbours, structures, cargo or time. Combine those with terrain pins in the next complete
action dependency boundary; do not omit actors from `WorldSnapshot` admission. World ownership transfer,
absent-detail funded work, atomic publication/crash/retry and original-history replay remain required.
The local geometry size bounds do not become shipping limits for vehicles, cities or the frontier.

[Checkpoint](CHECKPOINT.json) preserves `8bf21d887f3f1b956aa8971badbf71792459a739` before the change.
The accepted marker is `codex/milestone-movement-terrain-footprint-20261017`. Unrelated predecessor
staging, raw prior evidence, the October5 advisory and frozen specifications remain untouched.

## Documentation review

- **Plan/spec:** Roadmap, handoff and partition comparison record actual movement-derived terrain
  requests while preserving the full action/authority and integrated prototype obligations. No format,
  migration, new locomotion law or phase exit is selected.
- **Reference:** World/geography explains the shared step rule, conservative stair support envelope,
  signed-cell behavior and separation from actor/structure/time/permission completeness. Present-tense
  contracts stay separate from measurement and implementation history.
- **Accuracy/completeness:** Lead traced `PrepareWalkFrom`, candidate heights, both swept segments and
  `FootingQuery` endpoint/corridor support bands. Comparisons use those actual queries and retained
  body/terrain identities; the largest admitted fixture and legacy interpretation are explicit. No
  inference of whole-world streaming or a fresh authority token follows from the stored actor reference.
- **Checks:** Final 160 passing outcomes and the initial compile failure are retained. Documentation,
  authored whitespace, source/binary identity and protected-input checks are in `VERIFICATION.json`.
  No native or independent acceptance is claimed; orchestration remains paused and full gates remain open.
