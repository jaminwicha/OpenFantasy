# Withdrawal continuity: source review

Recorded 2026-10-03. Scope: save-interpretation selection for the P02 shared expedition,
RQ-029/RQ-079/RQ-099. This is direct lead design review. It adds no runtime code, new profile,
schema, gameplay permission or accepted migration. Independent architectural critique stays unrun
under the owner's no-orchestration instruction; full P02 and the implementation goal remain open.

## Finding and selected direction

Use an explicit new complete-world interpretation with a versioned retreat facet under one owner.
Keep original admission and earned resource/archive lineage. The earlier nested-only candidate was
insufficient as a complete compatibility design: the root encodes the old rule and field bound, normal
state updates preserve that rule, and the connection compares profile/admission without a separate
withdrawal-law field. This is a source-based design decision, not a benchmark or runtime proof.
The [decision](../../../PARTY_WITHDRAWAL_DECISION.md) carries the detailed implementation obligations.

The important existing boundaries are:

- `WorldResourceAdmission.Validate` compares retained anchor admission with world admission.
  `WorldMaterialCareArchiveAnchor.ValidateSuccessor` preserves scope/admission and concrete
  capability continuity. Resource-only anchors additionally require the exact profile. A new
  admission hash and fresh resource history over lived progression would break the lineage.
- `WorldProposalTransitionProcessor` supplies a real precedent for changing interpretation while
  retaining original admission, owned facts and clocks. Its request, cut and schema-eight path
  apply specifically to 27→30; withdrawal cannot reuse those semantics by changing a number.
- `WorldJournalRecovery.PrepareEntry` accepts migrations only through schema-three or schema-eight
  dispatch. Its final lineage check recognizes the single explicit proposal transition. A profile40
  adventure in schema12 cannot be upgraded merely by adding a root decoder or appending kind3.
  Schema12's inherited SQL kind list permits3, but replay still rejects it.
- `WorldSnapshotCodecV35` shares the profiles35–40 frame with exact legacy retreat codec, a4KiB
  read bound and reserved component capacity. The complete state maximum remains16MiB. No new
  complete-root maximum fixture or capacity measurement is claimed.
- `WorldActorCommandAuthority.AuthorizeAccepted` identifies retained pending abilities or the
  legacy active retreat. `WorldControllerBoundaries.CanRestoreHuman` uses the retreat hold. Both must gain
  the new owner's exact command semantics together; arrival is not permission to keep delegated
  control indefinitely or to erase the commitment.
- `PlayerWorldCompatibility.Matches` checks world/branch/profile/admission, while package identity
  comes from resolved manifests. Public compatibility does not authenticate an actor or historical
  consent. Root versioning, host selection and invitation/client refusal must agree.

## Delivery boundary

The next runtime increment must bind authenticated choices, due-order movement, Full Wait,
arrival/cancellation/incapacity and controller handoff to the complete world, with explicit lived-save
continuity. The initial upgrade may require quiet exploration, but cannot cancel accepted work or
reset the world to obtain it. Preserve pending care/material work and operational controller evidence,
or prove an explicit temporary refusal and lawful route to eligibility.

Use the maintained preserved-source publication discipline and original-genesis replay. Determine
whether a joint cut is necessary from actual owner changes rather than copying the proposal path
mechanically. Keep numeric profile/schema selection with its implementation. Required proof includes
old bytes/replay, failed and successful transition/reopening, ordinary subsequent cuts, exact original
rows/identities, post-transition played actions, incompatible client refusal and the earned expedition.
No fresh-world-only result closes this requirement.

## Documentation review

- **Plan/spec:** refined the versioning decision and the current roadmap's next dependency without
  changing the frozen181 requirements, Full Wait, no-lost-content obligation or acceptance gates.
- **Reference:** expanded the timeless persistence reference with admission/interpretation identity,
  archive-family distinctions, replay authorization, compatibility and capacity; connected the
  standalone withdrawal reference. Design obligations remain separate from executable bindings.
- **Accuracy/completeness:** traced the actual owners, codecs, transition processor, journal and
  controller/connection consumers pinned in [source-pins.json](source-pins.json). The review finds
  concrete integration prerequisites, not implemented migration or independent acceptance. Existing
  proposal-transition behavior is a precedent with a different source/target and archive contract.
- **Checks:** [documentation.json](documentation.json) records scoped document checks; source hashes,
  whitespace and preservation checks are recorded in [verification.json](verification.json).
  Link/field checks do not validate semantics; the source review above does. Runtime tests were not
  rerun because no runtime, fixture or toolchain changed, and no new runtime claim is made.

## Preservation and limits

[checkpoint.json](checkpoint.json) records the verified clean successor source and pre-change ref.
The19 unrelated staged predecessor files stay byte-identical. The prior component evidence and all
frozen specifications remain untouched. The documentation-priority doctrine already exists locally
and in ecosystem commit70401b8; this change applies it without adding duplicate process rules.

The first documentation patch failed to match its context and changed no file; the corrected patch
applied to the inspected section. The [initial document check](documentation-initial.json) reported
its own not-yet-created output link; its failed result is retained. Rechecking after that output
exists validates the real linked artifact, without changing the checker. No runtime test failure,
independent review, native validation,
new provider request or source-save migration occurred in this increment. The documentation marker
`codex/milestone-withdrawal-continuity-design-20261003` records only this reviewed design scope.
