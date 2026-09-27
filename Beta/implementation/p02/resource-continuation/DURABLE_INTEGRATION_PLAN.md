# Durable archive integration dependency

This is the next host change, not completed storage support. The Core continuation returns exact
sealed bytes with the next candidate; `WorldGenesisAdmission` still rejects care-enabled roots.

## Storage and transaction

Introduce an explicit schema7 while preserving schemas2–6 byte-for-byte. Care kind9 belongs only
to the new schema. Update supported schema lists, exact table/index validation, creation dispatch
and both genesis/final replay profile/schema checks together. Do not opportunistically migrate an
older save or use the existing fallback for profile25.

Store append-only `world_resource_segments` rows with exact16-byte world/branch IDs, unsigned
big-endian8-byte ordinal, boundary sequence and publication sequence,32-byte boundary hash, and
bounded full canonical closing bytes. Unique `(world, branch, ordinal)` prevents competing versions.
The publication sequence references the commit that publishes the successor. It differs from the
closing boundary sequence: Core seals the prior root at revisionN, then publishes the archive and
successor with commitN+1. Require the reference hash to equal that commit's actual before-hash.

`WorldCommitPreparation.Prepare` carries `Decision.SealedResourceSegment` in the prepared write.
`SqliteWorldAuthority.Commit` inserts it in the same immediate transaction as accepted batch,
outcomes, identities, head and scheduled checkpoint. Preview must not write it. Memory publishes
after transaction commit. Preserve the current pre/post-commit fault seams and uncertain-outcome
reopen rule. Use INSERT, never replacement/upsert. Add explicit archive-byte/count accounting to
the development envelope without redefining the existing accepted-journal byte counter.

## Recovery and retrieval

Extend `WorldJournalRecovery.ExpectedJournalEntry` with the expected seal. At its publication
commit, read the actual stored row, check exact scope/ordinal/sequences/hash, decode its retrieved
closing bytes with the preceding anchor and compare the bytes with the deterministic replay seal.
Compare the restored closing root to the actual prior replay state. Never regenerate a missing row
and treat recovery as valid. Reject extra/orphan rows after replay, as command/identity tables do.

Recovery starts from admitted genesis and executes all accepted commands, so its live anchor is
reconstructed from the actual prior world. Stored head/checkpoint bytes are compared against replay;
continued head bytes must not be independently decoded with an invented anchor. Recorded-state
reads share the same verified replay and keep the source-head revision/hash pin. A privileged archive
read API must retrieve verified immutable evidence under the owning authority's lock; no resolver
may retain a disposed database connection. Player knowledge projection remains separate.

## Care dispatch and admission

Add Care to `WorldCommitKind`/mapping and its exact one-command dispatch rule. Exempt only the
actual care boundary from tactical-kind equality; do not grant arbitrary normal actions during a
paused outcome. Fresh admission requires care/resource revisions zero, no archive anchor or earned
care/history, fixed scope and the exact fresh basis. Pending care recovery retains an acknowledged
obligation with its existing terminal acknowledgment receipt. The helper still needs its own
controller authorization and fresh eligible action; recovery does not fabricate a dose or receipt.

## Required executable qualification

- Real source-backed raw and crafted treatment: attack, acknowledgment, treatment, later attack/reward;
  reopen at each meaningful boundary with exact bytes/resources/materials/debt and original damage.
- Run the actual520-resource-command journey across two real SQLite archive publications. Retrieve
  both stored archives and replay from genesis. Include earlier funded XP and continued RNG/MP costs.
- Separate-process kills immediately before and after publication; exact duplicate retry cannot insert
  another archive or consume another dose. Preserve transaction failure evidence and original thresholds.
- Missing/changed/reordered/wrong-scope/orphan archives, edited anchor references, forged rewards,
  reused historical identities, stale offers, mismatched schema/profile and care pending state fail.
- Complete-batch reservation near a boundary:255 resource batches followed by two actual quest rewards
  at their deterministic command positions, plus later rejection/rollback. This critic-suggested case
  remains unqualified by the current single-attack stress journey.
- Legacy schema/profile fixtures and full affected managed regression; independent source/byte review.
  Native ordinary/aftermath treatment and physical/human acceptance remain their own subsequent gates.

The Core cut does not solve the256-care/material-claim bounds, global journal lifetime, production
scale, roster/rule migrations or full game persistence. Those retained obligations remain in the
complete implementation plan; no development envelope becomes a permanent gameplay rule.
