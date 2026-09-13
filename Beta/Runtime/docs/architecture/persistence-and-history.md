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

The [profile-six codec](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV6.cs) embeds a genuine
profile-five basis and a pinned watershed archive. The construction wrapper reconstructs its
derived colliders from exact construction state; the nested spatial basis cannot independently
invent the same structures. Ordinary state changes retain the immutable watershed object and bytes.

Fresh watershed admission requires an unplayed construction genesis with matching world/branch/realm.
It pins the previous admission and watershed hash. It is not insertion of geography into a lived save.
All decode/admission paths enforce identity roles across components.

## Recovery and bounded storage

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
