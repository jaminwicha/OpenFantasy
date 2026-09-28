# Live material transaction evidence

This increment observes accepted work and material changes inside the actual profile26 transaction
and reconstructs those observations from a fully verified saved journal. It preserves the complete
world graph: gathering, wet excavation and water, construction/backing, quest delivery, transfers,
crafting, ordinary care and aftermath remedies. No world, production or storage format is changed.

## Runtime contract

`WorldStepProcessor.DecideWithMaterialEvidence` returns the same candidate, receipts and identities as
ordinary `Decide`, plus opaque immutable observations in actual batch-action order. Intermediate pins
are captured after complete action admission and before passive time/recovery/flow/movement. Partial
work counts; rejected or rolled-back actions do not. Distinct input/prepared root hashes preserve the
meaning of a resource-history cut. Temporary collectors retain owners only while preparing the step;
returned observations retain hashes, commands and identities, with no root or ancestor chain.

`SqliteWorldAuthority.ReadMaterialEvidenceForHost(sequence)` reconstructs one selected commit while
verifying the entire journal through the current head, including real archive retrieval, later rows,
checkpoints, receipts and identities. It returns detached before/after and source-head pins under the
writer lock. Ordinary publication and recovery keep the additional observation work disabled.

These are observations, not imported material rights or complete intermediate-world certificates.
Resource-history, pacing and lifecycle are not separate fields in the named material-owner pins.
A coordinated protocol must still prove their causal closure and exact material conservation.
The [timeless reference](../../../Runtime/docs/architecture/live-material-evidence.md) states the boundary.

## Evidence and review

The new 21-case integration suite passes against real complete worlds and actual SQLite publication.
It covers three earned wet-mining swings and subsequent water redistribution; quest funding and all
200 construction actions through installed backing; gathering/crafting by two actors in one batch;
true action-ordinal gaps; split-transfer source intervals; raw/crafted ordinary use and aftermath care;
late complete-world reward refusal; resource-cut retrieval; and missing/corrupt later journal evidence.
Closed-save reads preserve file bytes. The full Core suite passes 1,577 cases and full Hosting passes
850: **2,427 distinct managed cases**, zero failures/skips. The 21 new integration cases are a subset,
not an additional count. All nine retained care/world frames (638,645 bytes) match exactly. Full results
and preservation/compatibility checks are recorded in [verification](qualification/verification.json).
Build/test commands, source/execution pins, original failures and dispositions are retained.

[Independent Core review](CORE_REVIEW.md) and [independent Host review](HOST_REVIEW.md) found no
correctness defect in their bounded scopes. The spatial-component documentation was clarified and
later-archive corruption coverage was added. The public read-error recovery flag is source-reviewed;
direct corruption tests exercise the replay reader, rather than a public-method storage fault injection.
No new native UI/device, production latency/scale, physical input or human acceptance is claimed.

[Fable](delegation.json) delivered the scoped Core implementation and six test cases using
`fable[1m]`, Max effort: 795.295 seconds, reported list-price estimate $7.6656155, verified actual model
`claude-fable-5-1` and 1,000,000 context. No permission denials, context drift, substitutions or retries.
Astra integrated it, replaced the guessed identity envelope with bounds derived from supported domain
operations, corrected the worker's archive-test assumption, and added the real work/Host coverage.
[Original result](FABLE_RESULT.md) and [all failures](FAILURES.md) preserve the limits. No subscription
credit saving is proven. The private raw transcript remains outside Git.

## Preservation

Base: `0e05d8d50cde989a4ec8e93429b1ff2340711e9f` on `codex/runtime-foundation`.
The [initial checkpoint](preservation.json) records clean successor HEAD before edits. The separate
[pre-Host WIP snapshot](host-checkpoint.json) preserves the partial integration and explicitly claims
no passing checks. Unrelated predecessor staging, earlier milestones and frozen specs are preserved.
The original milestone inventory was empty; [reconciliation](milestone-reconciliation.json) verifies
all 70 refs in the prior committed inventory plus the care-frontier ref at original HEAD. The original
record and first verification output remain retained; this is explicitly an after-run correction.
All 71 prior refs, 23 frozen specs and predecessor staging pass verification. Both checkpoint refs
resolve exactly. Verified scope is preserved on `codex/milestone-live-material-evidence`; resolve it
for the exact commit. The current handoff records the remaining implementation boundary.

## Remaining complete-world dependency

World27, ProductionV9 and schema8 remain proposed and disabled. Next work must derive the coordinated
material/care/resource boundary from the admitted full graph, preserve actual wet excavation/effort,
construction/backing/quest mutation and all other live material writers, and authenticate archived
closing bytes. These observations can inspect that real transaction; their hashes cannot mint a grant.

Required acceptance still includes hundreds of real doses and surviving recraft ancestry through
multiple cuts, pending-obligation headroom, material-only cuts, historical-identity refusal, exact
rollback, actual process-kill recovery, native post-cut use/reopen and unchanged legacy meanings.
The entire production campaign, physical-expansion, platform, multiplayer, content, art and human
acceptance gates stay open. This packet does not close them or replace the full specification.
