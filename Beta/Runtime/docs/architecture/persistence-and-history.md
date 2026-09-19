# Persistence and history

## Runtime bindings: durable ownership

[SqliteWorldAuthority](../../src/OpenFantasy.Hosting/Worlds/SqliteWorldAuthority.cs) owns an exclusive
database lease and serializes operations under its writer gate. SQLite uses WAL, full synchronous
durability and explicit transactions. The active WAL and sidecars are part of database state;
copying only an active main file is not a consistent backup.

The [world schema](../../src/OpenFantasy.Hosting/Worlds/WorldDatabaseSchema.cs) stores:

| Record | Meaning |
| --- | --- |
| `world_genesis` | Canonical admitted starting state and hash |
| `world_head` | Published sequence, advancing-step count, complete state and hash |
| `world_commits` | Ordered accepted batch or typed transition, cause, outcomes and before/after hashes |
| `world_command_outcomes` | Principal/command identity, exact bytes and durable receipt |
| `world_occupied_identity` | Historical committed identity reservations and roles |
| `world_checkpoints` | Canonical state at declared recovery boundaries |

[Schema three](../../src/OpenFantasy.Hosting/Worlds/WorldDatabaseSchemaThree.cs) extends accepted
commit/identity kinds for profile transitions. Schema version and snapshot profile version are
different dimensions; adding a profile does not imply a new writer or database.

The transaction writes the commit, outcomes, occupied identities, head and any checkpoint together.
Only a successful commit publishes the candidate in memory. An uncertain storage outcome requires
recovery before more writes. Restart checks exact persisted bytes and relationships, not just an
apparently valid head hash.

## Runtime bindings: snapshot profiles

[WorldSnapshotCodec](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodec.cs) dispatches exact,
bounded interpreters. Profiles are saved compositions, not capability inferred from a client:

| Profile | Composed state |
| --- | --- |
| 1 | Exploration: custody, space, residents, calendar and dialogue |
| 2 | Exploration plus gathering definitions, sources and work |
| 3 | Gathering plus material production and provenance |
| 4 | Production plus progression, currency and delivery quests |
| 5 | Quest world plus construction, installed material and derived collision |
| 6 | Complete construction world plus one immutable prepared watershed |
| 7 | Complete construction world plus one immutable prepared watershed network |
| 8 | Complete construction world plus one exclusive physical reservoir/capacity owner and its network |
| 9 | Complete construction world plus one exclusive reservoir transport owner, gate catalog and due-stage journal |
| 10 | Complete dry construction world plus finite local excavation ownership and earned work |
| 11 | Complete dry excavation world with installed-member actor support and assisted vertical traversal |

[Profile ten](excavation.md#persistence-and-boundaries) binds exact cell/source ownership and work to
retained terrain edits and the existing custody/Production ledger. Its fresh admission and action eight
are explicit; it shares the writer and limits while excluding independent watershed/reservoir ownership.

[Profile eleven](constructed-footing.md#complete-persistence-composition) stores one complete direct
root so installed floors exist before actor admission. It never strips supporting structures to encode
or decode an intermediate legacy basis. Support surfaces derive from retained completed construction;
spatial version two selects their contact rules. Fresh admission and climb action nine are explicit.

The [profile-six codec](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV6.cs) embeds a genuine
profile-five basis and a pinned watershed archive. The
[profile-seven codec](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV7.cs) embeds a genuine
profile-five basis and a pinned network archive. These are sibling compositions; seven does not nest
six or duplicate a member as an independently owned single basin. The construction wrapper reconstructs its
derived colliders from exact construction state; the nested spatial basis cannot independently
invent the same structures. Ordinary state changes retain the immutable macro object and bytes.

Fresh macro admission requires an unplayed construction genesis with matching world/branch/realm.
It pins the previous admission and the single-basin or network hash under distinct admission domains.
The network pin is SHA-256 of an OFC frame tagged `world.watershed-network-admission.v1`, followed by
the prior admission bytes and exact network content-hash bytes. That pin is retained in the nested
basis. The outer snapshot has tag `world.snapshot`, unsigned-64 profile `7`, exact profile-five basis
and exact network archive. Its step interpreter retains the existing action and receipt semantics.

All decode/admission paths enforce identity roles across components. Admission is not insertion of
geography into a lived save. Profiles six through eleven use the same schema-three writer and full-journal
recovery; they add no migration commit, separate writer or clock.

[Profile eight](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV8.cs) frames unsigned-64 profile `8`,
a genuine profile-five basis and the unchanged complete `LiquidReservoirCodecV1` archive. The latter
owns the sole network; an independent basin/network argument rejects. Inventory and Production occur
in both nested formats, but exact content equality is mandatory. They are duplicate serialized evidence
of one authority, never independently writable roots. Both nested and complete-world bounds apply;
the outer limit remains 16 MiB even though a standalone reservoir archive permits more. This explicit
format reuses existing interpreters and does not silently normalize or discard prior archives.

[Fresh reservoir admission](../../src/OpenFantasy.Core/Worlds/WorldReservoirAdmission.cs) requires an
unplayed profile-five construction world and the exact inventory/Production of the supplied owner.
The new admission pin is SHA-256 of an OFC frame tagged `world.reservoir-admission.v1`, prior admission
hash bytes and complete reservoir hash bytes. It retains that pin in the nested basis. Ordinary profile-eight
batches use the existing action interpreter, schema-three transaction, historical identity reservations
and exact replay. `--create-reservoir-world` requires a new destination; `--inspect-recorded-world`
reports the exact physical owner at the selected commit. These privileged operations confer no network
authentication and introduce no migration of a lived save.

[WorldActionCodecV6](../../src/OpenFantasy.Core/Worlds/WorldActionCodecV6.cs) encodes runoff commitment
controls in the existing ten-field `world.actor-action` envelope with unsigned-64 action version `6`.
Its nested frame is tagged `world.action.runoff-commitment`, with version `1`, kind (`1` admit or `2`
cancel), exact 32-byte reservoir hash, capacity revision and reservation ID. Admission adds start/end
instants (signed-64 days plus signed-32 milliseconds) and canonical basin input rows. Each existing
72-byte input row names basin, depth, source ID/range and custodian; a dry row has zero funding bytes.
The command is bounded to 2,048 bytes and sixteen input rows. Decode rejects partial/duplicate/reordered
rows, hidden dry funding, unknown kinds/versions and trailing fields by validating and re-encoding exact bytes.

Action version and world profile version are distinct: runoff controls execute in profiles eight and nine.
Both constructor and decoder paths reject their insertion into older world batches; the local session
checks the profile before queuing. Existing snapshot/step encodings, global command/batch bounds and
schema-three tables remain unchanged. Accepted capacity-only operations and rejected controls receive
the same atomic journal/outcome/identity treatment as other actions; exact retries retrieve the recorded
receipt. Restart and historical reads replay controls rather than reconstructing obligations from wall time.

[Profile nine](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV9.cs) frames unsigned-64 profile `9`,
a genuine profile-five basis and the complete
[transport archive](../../src/OpenFantasy.Core/Geography/ReservoirTransportCodecV1.cs). Its admission
pin frames `world.reservoir-transport-admission.v1`, prior admission hash and transport hash. The transport
archive owns the unchanged physical wrapper and therefore the sole network. The outer world still fits
16 MiB; no prior profile, physical archive or database table changes meaning. Fresh construction admission
is explicit; opening a lived profile-eight save does not start executing previously inert budgets.

The transport archive's six fields are tag `geography.reservoir-transport.v1`, revision, exact processed
calendar boundary, physical archive, canonical 96-byte gate rows and a nested stage journal. Each gate row
contains three identities, five signed-64 coordinates and unsigned-64 per-step throughput. Stage frames
retain stage/reservation IDs, due instant, funding hash, units, 32-byte basin/gate route rows, typed status
and optional resolved instant. Connections/stages sort by identity; routes sort by basin. Decode bounds
collections before reconstructing all cross-component contracts and requires exact re-encoding. The
standalone archive permits 29,000,000 bytes; the composed-world bound remains independently mandatory.

[Action version seven](../../src/OpenFantasy.Core/Worlds/WorldActionCodecV7.cs) uses the ordinary ten-field
actor envelope with a `world.action.schedule-runoff` nested frame: version `1`, complete transport hash,
transport revision, reservation ID, explicit due instant and at most sixteen route rows. Its 2,048-byte
bound does not expand the global command limit. Only profile nine accepts this intent. Existing version-six
admit/cancel commands remain byte-identical and use the transport journal when cancelling a scheduled budget.

`--create-reservoir-transport-world` takes a new database path, profile-five basis and fresh transport
archive. Live and historical privileged reports expose physical/transport hashes, boundary, stage outcomes
and exact vessel quantities. All material stages replay through ordinary world commits; no scheduler side
file or independent writer can advance them. Root admission rejects a prepopulated journal at world revision
zero, including direct construction paths. The [transport contract](reservoir-transport.md) defines step
ordering, source/capacity atomicity and retained blocked outcomes.

The [dry terrain candidate archive](../../src/OpenFantasy.Core/Geography/WatershedTerrainCodecV1.cs)
is a separately versioned preparation artifact, not another composed-world profile or a replacement
for `TerrainRegionCodecV1`. Retaining it proves exact candidate reconstruction, without admitting it
into a lived world or creating historical terrain changes. World snapshot and journal bounds remain
independent of this archive's preparation envelope.

## Recovery and bounded storage

Each complete canonical snapshot fits the existing 16 MiB bound. The network archive has its own
four-million-byte bound inside that envelope. Individually valid components do not guarantee a valid
combined size: an oversized composition rejects before new-save publication. Limits are not expanded
or satisfied by dropping retained state.

[WorldJournalRecovery](../../src/OpenFantasy.Hosting/Worlds/WorldJournalRecovery.cs) replays from
genesis, interprets typed transitions, recomputes outcomes and verifies the hash chain, receipt table,
occupied identities, checkpoints and head. A self-consistent substituted checkpoint or head is
insufficient if it disagrees with replayed history.

The full-journal profile has an explicit recovery envelope in
[WorldJournalPolicy](../../src/OpenFantasy.Hosting/Worlds/WorldJournalPolicy.cs): 250,000 commits,
65,536 command outcomes and 128 MiB of accepted batch/outcome journal bytes. Checkpoints are triggered
by 6,000 advancing steps or 10,000 commits. These limits describe this storage algorithm, not a
promised world lifespan or production throughput. Reaching the envelope visibly stops the host;
it cannot discard history, relax the bound or turn the failure into a rejected player action.

## Migration and publication

[WorldProductionUpgrade](../../src/OpenFantasy.Hosting/Worlds/WorldProductionUpgrade.cs),
[WorldQuestUpgrade](../../src/OpenFantasy.Hosting/Worlds/WorldQuestUpgrade.cs) and
[WorldConstructionUpgrade](../../src/OpenFantasy.Hosting/Worlds/WorldConstructionUpgrade.cs)
validate a source, operate on a preserved copy and select a verified destination through
[WorldUpgradePublication](../../src/OpenFantasy.Hosting/Worlds/WorldUpgradePublication.cs).
Old saves and their histories remain intact. Existing selected destinations take precedence;
incompatible sources, orphaned sidecars or failed upgrades stop opening instead of creating an
apparently fresh replacement. New components cannot reset pre-existing bodies, terrain, rewards
or material custody.

## Domain contracts and runtime binding: historical reads

[HistoryAddress](../../src/OpenFantasy.Core/History/HistoryAddress.cs),
[HistoryRequest](../../src/OpenFantasy.Core/History/HistoryRequest.cs) and
[HistoryCommitSelection](../../src/OpenFantasy.Core/History/HistoryCommitSelection.cs) encode
historical coordinates, boundaries and selection over an ancestry-resolved supplied stream.
Those types do not themselves reconstruct a world or move an actor through time.

`ReadRecordedStateForHost(revision)` reconstructs an **exact recorded commit**, returning an immutable
[RecordedWorldState](../../src/OpenFantasy.Hosting/Worlds/RecordedWorldState.cs) pinned to the
source head's revision and hash. It uses synchronous full-journal recovery under the writer gate.
It is a privileged operator read, with no player knowledge filtering. It provides neither arbitrary
interior-time reconstruction nor generated prehistory, branch travel or a player-facing atlas.

## Design contracts: chronology and travel

Historical destinations combine world, branch, realm, place and time. Reconstruction preserves
geography, resources, living processes, institutions and causal changes together. Generated
prehistory and lived history require retained rules, process boundaries and reproducible intermediate
states; a newer seed generator cannot replace old facts. A historical query and a travel command
are separate operations, with distinct permissions and costs.

Branches retain ancestry and divergence explicitly. Transit reserves and transfers permitted actors,
cargo and source rights atomically. Shared ancestral source intervals cannot multiply export rights
through forked harvesting, crafting, waste or repeated return trips. Knowledge transfer follows its
own rules; material and quest cargo require explicit eligibility. Player observation of a past world
still passes through perception and knowledge rules.

History indexing and archive compaction preserve every promised destination losslessly. Capacity
pressure is visible backpressure, not permission to erase quiet eras or worlds without active quests.
These broader obligations come from the [technical design](../../../spec-v1_1/TECHNICAL_DESIGN.md)
and [geological history contract](../../../spec-v1_1/GEOLOGICAL_WORLD_DESIGN.md).
