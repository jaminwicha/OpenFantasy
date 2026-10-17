# Prepared spatial movement reads

`SpatialMovementDependencies` captures terrain references and relevant actor/structure colliders for
one actual cardinal movement from a caller-established complete `LocalSpatialSnapshot`. It retains
selected immutable state, rather than the complete source scene. Evaluation loads the exact prepared
terrain plan and uses the same movement/clearance implementation as ordinary movement, stair walking,
climbing and route search. It returns geometry, not an authorized world mutation.

Current-state validation re-enumerates the movement envelope, detecting arriving and departing
colliders as well as changed positions, revisions, body bindings/conditions, structures, bearing
surfaces, terrain and support interpretation. Equivalent reconstructed dependencies match; unrelated
distant actor movement does not invalidate the read. The publication owner must prevent intervening
changes and supply the remaining custody, control, time and action dependencies.

## Verification

The [focused Core run](check-01/core.json) passes 164 cases and the
[Hosting run](check-01/hosting.json) passes three, including seven new dependency cases.
Because the extracted kernel also serves ordinary runtime queries, the
[full Core run](core-full/process.json) then passes all 1,938 cases without failures or skips in
124.166 seconds under its unchanged 180-second bound. There are **1,941 distinct passing cases**
across full Core and selected Hosting; the focused Core subset is not counted again. Focused
invocations take 14.264 and 23.908 seconds, each under 180 seconds. Builds report no warnings/errors.

New tests exercise real blocking actors, arrivals/departures, position/form changes without revision
bumps, structure/surface changes, equivalent reconstructed state, terrain edits, support interpretation,
actual stair ascent and refusal of a read set bound to another terrain plan. The file-backed Hosting
case supplies both real world actors in its complete source scene, selects only the relevant mover,
loads two required regions, and leaves a third archive unread. Prepared and complete movement agree.

The [extraction check](EXTRACTION_CHECK.json) compares moved method bodies with the prior committed
implementations after declared field-name, visibility and constant-qualification substitutions.
Lead inspection also checks constructor wiring and dependency selection. This is source/behavioral
evidence, not an independent architectural review or a new native qualification.

## Scope and next work

Capturing the dependencies still starts from a complete supplied local scene. Selected-state retention
and omitted archive reads do not prove whole-process memory savings, a global residency directory or
world-scale streaming. No codec, schema, interpretation, default launch or gameplay law changes here.
The ordinary world continues complete admission. PLAN-STATE still needs integrated ownership and
actor/cargo transfer, complete action dependencies, absent-detail funded work, atomic publication,
crash/retry and original-history replay with meaningful measurements. No limit is raised or gate waived.

The owner's requested recent review is the preserved
[October17 Opus report](../../../roadmap-review-20261017/REVIEW.md). Its existing lead disposition
covers every R1–R13 finding. This checkpoint reconciles those roadmap actions with the now-qualified
selected expedition and the still-incomplete partition prototype. Cognition, story, ecology, art/audio,
catalog targets and structured human playtesting remain scheduled or unresolved as stated there;
the historical review is not rewritten to suggest it inspected later work. No new delegation occurred.

[Checkpoint](CHECKPOINT.json) preserves `8fc4586b397ce59f4d8265f27caa49dd9feca86b` before the change.
The accepted marker is `codex/milestone-prepared-spatial-reads-20261017`. Unrelated predecessor staging,
the October5 advisory, frozen specifications and prior evidence remain preserved.

## Documentation review

- **Plan/spec:** Roadmap, handoff and partition comparison distinguish spatial preparation from the
  integrated PLAN-STATE acceptance case. The recent review's findings retain explicit dispositions
  and phase placements. No full-scope requirement, phase exit, format or content target changes.
- **Reference:** World/geography describes selected dependencies, exact plan binding, shared query
  ownership, current-state revalidation and the caller's completeness/publication obligations. The
  encyclopedic reference uses present-tense contracts without turning preparation into authority.
- **Accuracy/completeness:** Lead traced capture, selection, actor/structure equality, shared kernel
  wiring and ordinary movement callers against the source and tests. Source-scene completeness,
  global directory absence, nonspatial dependencies and unmeasured memory/scale remain explicit.
  All 13 external-review dispositions were checked against the current execution sequence.
- **Checks:** Passing focused and full-Core outcomes, source/binary identities, whitespace,
  documentation links/fields and protected inputs are recorded in `VERIFICATION.json`. Link checks
  supplement factual inspection. Independent, human, platform and broader phase gates remain open.
