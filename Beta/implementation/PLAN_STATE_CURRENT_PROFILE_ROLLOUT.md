# Current40/41 ownership and preserved-source rollout

Lead preparation at `ca32680af2700f96726abbfb75174599153e502d`, for the existing PLAN-STATE
comparison and independent decision review. This is a proposed implementation contract, not a new
schema allocation, enabled migration or accepted partition design. It complements the
[current field/dependency map](PLAN_STATE_CURRENT_PROFILE_MAP.md),
[membership contract](PLAN_STATE_PROCESS_MEMBERSHIP.md) and [exit matrix](PLAN_STATE_EXIT_REVIEW.md).
All source profiles, P02 requirements and unrun review/qualification gates remain intact.

## Separate the two transitions

**Storage-only conversion** preserves the complete admitted current world bytes/hash, profile40 or41,
admission, world/branch and every command/time/identity fact at the selected head. It changes the
physical representation and its versioned metadata. It must not append a fake gameplay command,
earn time, create a new resource baseline, reset local revisions or reinterpret historical outcomes.
The format's source binding must authenticate the existing head and its retained history.

**Semantic migration** changes a gameplay interpreter or supported composition. The existing40→41
withdrawal transition is an example: it has a specific request, nonadvancing migration commit and
historical replay law. Reusing its SQL commit kind or publication helper does not authorize arbitrary
new profiles. Adding profile9 transport to a sealed current world is a semantic composition change,
not storage conversion. Keep the two proposals separate so their proofs cannot be confused.

The first current-profile partition candidate should preserve the40/41 interpreter. A later unification
of alternate-profile physical/process features needs its own no-loss admission and time-law design.
No schema number, byte tag or new command is reserved by this plan.

## Ownership coverage and byte accounting

The existing current-profile map enumerates every outer field and high-risk nested group. The target
representation must give every fact one canonical owner, with an explicit disposition for every encoded
field. Stable keys follow world/branch and semantic identity; crossing a region or changing controller
never invents a new person, custodian, source lot or historical producer. References may repeat; writable
canonical facts may not. Immutable historical snapshots are witnesses, not competing current owners.

| Source facts | Target responsibility / required reconstruction |
| --- | --- |
| Profile, admission, revision, advancing count, calendar, rule pins | One admitted head/timing/composition manifest with separate semantic and storage versions. Rebuild the exact original scalar fields; directory hashes are not admission identities. |
| Inventory actors, controls, policies, sources/capacities, reservations and custody | Stable authority/custodian owners with complete admission indexes. Counted and produced holdings share actual capacity/custody checks; control is not inferred from physical presence. Preserve each nested codec field and revision. |
| Spatial scope/regions, body definitions, actors and resident records | Stable terrain/actor owners and derived spatial membership. Resident identity, personal exposure and metadata remain complete. Installed colliders rebuild from construction, not a second editable spatial copy. |
| Gathering, production, progression and quests | Domain-owned processes/definitions, source issuance, transformations, progression/accounts and participation. Keep each genuinely coupled aggregate whole until a smaller boundary has its own qualified admission/decoder. |
| Construction definitions/projects, funding and installed backing | Stable project/material owners; references retain all builders, access actors, reservations and producer witnesses. Partial work cannot skip full final installation admission. |
| Reservoirs, seals, liquid cells and excavation | Coupled place/material owners and exact topology/context. Rebuild topology from reservoirs, terrain and seals. Current profiles cannot adopt standalone transport or other incompatible physical owners as optional extras. |
| Dialogues, pacing, lifecycle, retreat and outcome | Shared interaction/timing owners with exact participants, pending commands, random counters, decision debt and historical outcomes. Profile41 party membership/progress/resolutions remain distinct from profile40 individual retreat. |
| Coordinated market/care group and resource history | One matched archival context plus actual current consent, care, issuance, proposals, paid exchange, market/private knowledge and resource facts. Preserve its seven semantic fields and exact tag (eight framed fields total). |
| Controllers, recovery/disposition rule pins, control permissions and effective assignment | Separate connection, permission and acquired-authority owners with original witnesses. Preserve offers, acceptance, revocation, disconnected state and pending recovery; none grants the others implicitly. |

For every target owner, record source field/subfield, codec/discriminator/version, stable key, canonical
bytes/revision, membership rule, dependency cohort, public/private projection and history witness.
Where an existing codec exactly matches the chosen owner, reuse it. Where a split crosses its boundary,
implement an explicit lossless extractor/reconstructor with exact source-byte round trips before storage
adoption. Keeping an indivisible domain aggregate initially is permitted; calling it actor-level partial
loading is not. Do not create an intermediate throwaway public format merely to pass a migration test.

The complete V35-family decoder already re-encodes the admitted result and rejects noncanonical bytes.
Use that existing full admission as the oracle during development. A ledger of mapped property names
is necessary navigation, not proof that every union variant or nested pending state survives a split.

## Historical context is a dependency, not just a hash

The coordinated frame carries care, source baseline, optional joint-anchor binding, consented material
history, proposals, paid exchange and markets. A binding names an actual retained anchor; its bytes
alone cannot decode the continued production, geography, care and resource fields. The current decoder
requires exact matching context before reconstructing those owners. Retain original archives and their
publication sequence, predecessor/frontier and producer identities. Do not synthesize a fresh anchor
from the current balances or keep a hash while discarding its retrievable proof.

Preserve old genesis/journal/checkpoint/archive bytes and original interpreters. New representation
may derive a current manifest from authenticated history; it cannot rewrite the original past into the
new codec and call the result the same history. The selected head, its storage manifest and all modified
owner/directory references commit together through the existing writer. Original full replay remains
required until an independently qualified checkpoint/dependency scheme replaces its exact obligations.

## Copy, validate, publish and recover

Reuse the responsibilities of `WorldUpgradePublication`, `WorldBackupComparison`, `WorldJournalRecovery`
and the existing single-writer lease. Their current methods admit a closed set of known upgrade kinds;
this proposal does not make a new kind callable. A reviewed storage conversion needs its own explicit
preparation and storage-version dispatch, rather than impersonating `party-withdrawal`.

1. **Pin and lock the source.** Recover its actual world/profile/admission, head, history and operational
   recovery state under the established lease. Reject unsupported composition, corrupt ancestry,
   missing anchor, insufficient envelope or an already-used destination before changing source data.
2. **Make a private consistent backup.** Use the existing SQLite backup path in an unused staging
   destination. Compare original genesis/head, journal command/outcome bytes and hashes, receipts,
   identities, checkpoints and resource/material-care archives. These are `WorldBackupComparison`'s
   current responsibilities, not merely a final-state hash check.
3. **Preserve operational recovery separately.** `WorldPartyWithdrawalUpgrade.RequireRecovery` also
   checks controller recovery hash and activation bytes because these are not all contained in the
   world snapshot or generic backup comparison. The new conversion must retain activation provenance,
   generation/allowance integrity and restoration policy; copying world entities alone is insufficient.
4. **Prepare and verify the target.** Build complete profile-specific owner membership, exact payloads
   and dependency references from the admitted copy. Validate absence/forbidden combinations and all
   references. Reconstruct the exact current world bytes and all retained history; compare projections
   and private/controller access at actual boundaries. Missing required owners cannot become empty.
5. **Commit the representation atomically.** All target format metadata, current root references,
   owner payloads and required indexes become mutually consistent in the staging transaction. A pure
   storage conversion preserves gameplay sequence/time/receipts. Specify recovery dispatch and the
   retained source-binding record before assigning a durable format; do not use a gameplay cause ID
   as an unreviewed storage-generation token.
6. **Reopen before publication.** Recover through the target's real opening path and verify exact current
   bytes, original historical reads/receipts and operational recovery. Checkpoint for publication under
   existing rules; a nonempty WAL prevents publishing only the database file. Do not waive full replay
   merely because the staged manifest hashes agree with themselves.
7. **Publish without overwrite.** Reuse the unused-destination check and atomic move. A before-publish
   failure leaves the source selectable and private copy diagnostic-only. An after-publish failure may
   leave a valid destination; inspect it once under the normal recovery path before any deliberate
   operator action. Never blindly retry or silently choose an incomplete private copy.

The source remains preserved. Before any target gameplay, rollback means selecting the intact source
and retaining/quarantining the target. After new accepted gameplay, selecting the older source would
lose progress and is not a valid rollback. Require an explicit lossless reverse export or keep the
new save under its supported interpreter; never promise downgrade solely because a backup exists.

## Rollout and acceptance sequence

A. **Architecture review:** evaluate this owner map, legal compositions, atomic source binding,
   historical and operational recovery, budgets and measured alternatives. Resolve findings before
   permanent NPC persistence or promotion. The owner's orchestration pause leaves this gate unrun.
B. **Lossless current-frame candidate:** reuse actual40/41 saved lineages and component decoders; prove
   every chosen owner split reconstructs original bytes/rights/history, including populated nested and
   continued cases. A fresh empty snapshot is insufficient. Keep default creation/opening unchanged.
C. **Explicit copied-source storage candidate:** implement only the reviewed map/version and existing
   writer integration. Prove normal and faulted conversion and continue real gameplay on the copied
   save; its originals remain independently readable. No in-place adoption or automatic fallback.
D. **Partial execution and measurement:** consume the actual owner/dependency membership for selected
   operations and mandatory processes; compare against original interpretation. Include changing
   membership, held/blocked work, all-resident exposure, continued archives and full final admission.
   Measure resident detail, boundaries, due-service units and load/commit/replay costs on named hardware.
E. **Promotion:** satisfy the unchanged relevant gates and compatibility/recovery decision, then choose
   explicit host/client admission for the new representation. No PLAN-STATE prototype or this design
   closes full P01/P02/P03, production scale, platform, human or end-to-end campaign acceptance.

These are ordered dependencies within the existing roadmap, not new phases or a reassignment of its
open gates. The independent review must judge which concrete preparation/implementation proofs meet
the original bounded PLAN-STATE exit, without either deleting obligations or demanding an entire
shipping engine to review a migration design. Any proposed gate clarification requires explicit
recorded disposition. All pending requirements remain pending meanwhile.

## Required refusal and fault matrix

| Boundary | Required contrast |
| --- | --- |
| Source/admission | Wrong profile/branch/head, unsupported combination, missing anchor, altered history, duplicate owner, incomplete coverage; unchanged original remains recoverable. |
| Capacity/identity | Existing maximums and historical retired identities; no raised caps, trimmed actors, dropped private facts or minted source intervals. |
| Atomic conversion | Fault after backup, during target transaction, before/after commit and before/after publication; only complete old or complete new representation is selectable. |
| Active nested state | Pending care/disposition, open proposals/quotes, private market observations, permissions/effective controls, reconnect allowance, paid encounter/withdrawal state; no replayed payment or implicit grant. |
| History | Original genesis, checkpoints and archive boundaries including coordinated continuation, exact command/receipt identities and historical before/after reads. |
| Live continuation | Actual movement/custody transfer, due processes, work and final installation on the converted lineage; full admission fallback and same-source invalidation remain honest. |
| Operator recovery | Destination already exists, failed private copy, nonempty WAL, restart after publication, downgrade after new play; no automatic overwrite/retry or silent loss of progress. |

## Documentation review

- **Plan/spec:** provides current-profile ownership/disposition and concrete copied-source rollout,
  preserving existing phase order, all fields/history/operational recovery and unchanged exit gates.
- **Reference:** timeless persistence reference gains the storage-versus-semantic conversion distinction,
  generic backup versus operational recovery boundary, and rollback limits; no new format is claimed.
- **Accuracy/completeness:** reviewed actual complete40/41 codecs, joint-anchor decoding, constructor
  composition exclusions, backup comparison and40→41 publication/recovery responsibilities. This is not
  an exhaustive implemented nested-field migration proof or independent architectural acceptance.
- **Checks:** [packet source/documentation evidence](p02/partition-state/current-profile-rollout/RESULT.md).
