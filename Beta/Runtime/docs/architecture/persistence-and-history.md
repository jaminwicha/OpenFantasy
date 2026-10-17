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
| `world_resource_segments` (schema7) | Exact closing roots, immutable scope/ordinal and atomic successor publication sequence |
| `world_material_care_segments` (schema8) | Exact joint material/care/resource closing roots and atomic successor publication sequence |
| `controller_recovery_activation` (schemas10–12) | Immutable recovery policy and original activation provenance |
| `controller_recovery_segment` (schemas10–12) | Fenced host generation and checksum of the complete operational allowance set |
| `controller_allowances` (schemas10–12) | Episode-bound, durably measured host milliseconds for each outstanding disconnect allowance |

[Schema three](../../src/OpenFantasy.Hosting/Worlds/WorldDatabaseSchemaThree.cs) extends accepted
commit/identity kinds for profile transitions. Schema version and snapshot profile version are
different dimensions; adding a profile does not imply a new writer or database. Recovery dispatches a
kind-3 commit by schema: schema three admits only its production, quest and construction migration
tags, and schema eight admits only the proposal transition. Every other schema refuses the kind.

The transaction writes the commit, the profile's optional archive, outcomes, occupied identities,
head and any checkpoint together. Schema seven and eight use distinct archive families.
Only a successful commit publishes the candidate in memory. An uncertain storage outcome requires
recovery before more writes. Restart checks exact persisted bytes and relationships, not just an
apparently valid head hash.

[Controller recovery](controller-recovery.md) explicitly selects schema ten for controller profiles
36–38. Its canonical world encodings and inherited archive definitions remain unchanged. Lifecycle
events and their operational row insertion/removal share the same transaction. Independent measured
progress checkpoints change no world revision or gameplay time. Copy-and-activate admission retains
original history and validates activation against its actual replayed source root; ordinary opening
does not upgrade a save or start a clock.

[Co-player permission](control-permissions.md) uses explicit profile39 and schema11. Its isolated
actor-command boundary stores ordinary receipts and permission state in the same journal transaction.
Schema11 retains measured allowance recovery, while admitting the new batch kind without changing
schemas1–10. Consent is not an active controller assignment or new gameplay authorization.

[Effective control](effective-control.md) explicitly selects fresh profile40/schema12. Assignment
events use their dedicated nonadvancing commit kind in the same transaction and recovery lineage.
Commands retain the real grantee principal/session and operated actor; exact prior receipt retrieval
remains distinct from new authority and private-history access. Replay authenticates the original
grant and acceptance even when permission is subsequently revoked. Earlier schema definitions remain
unchanged, and ordinary opening does not migrate a lived save.

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
| 12 | Complete dry excavation world with bounded mixed-height contact and ordinary stair movement/navigation |
| 13 | Complete stepped dry world plus separately revised physical construction members and material backing |
| 14 | Complete physical world plus immutable load policy, represented carried/project mass and stable action/movement admission |
| 15 | Immutable harmless-settlement calibration and atomic excavation with intact member/occupant descent |
| 16 | Reserved advancing interval, retained geometric consequence cursor and original deferred receipt |
| 17 | Explicit foreign-construction support, bounded coupled intact settlement policy and its reserved consequence cursor |
| 18 | Explicit event-ordered intact descent with a V3 cursor and actual scene-dependent work reservations |
| 19 | Complete fixed-reservoir world with immutable sealing rules and mandatory geometric containment |
| 20 | Terrain-contained reservoir world with exact detailed liquid placement and selected-boundary cellular flow |
| 21 | Terrain-derived liquid capacity and owned excavation with explicit raised-bank reach and finite provenance headroom |
| 22 | Permanent accepted exposure, coordinated Wait timing and fractional process carry over the complete terrain/liquid world |
| 23 | Source-pinned physical abilities, exact pending requests, damage randomness and real progression resources over the complete paced world |
| 24 | Permanent encounter scenarios, phases, cumulative exploration/tactical clocks and readiness debt across authenticated entry and outcome release |
| 25 | Aftermath care, consumption-enabled material owners and ordered causal resource history with explicit schema-seven archive publication |
| 26 | Ordinary restorative use, patient-owned assistance permission and revocation over the complete care/resource world, retaining schema-seven publication |
| 27 | The complete ordinary-care graph with coordinated material/care/resource closure, exact source issuance and schema-eight publication |
| 28 | Coordinated world with explicit simultaneous bilateral material transfer evidence and Production V11 continuation |
| 30 | Coordinated world with separately submitted saved material proposals, exact carried grants and permanent outcome identities |
| 32 | Saved-proposal world with paid quotes, currency-only causal fills, issuer revisions and joint archive carry |
| 33 | Paid world with immutable market bindings, rule-priced quotes, dated actor price knowledge and seller-authored selected-lot disclosures |
| 34 | Encounter retreat commitments with paid continuation and explicit escape boundaries |
| 35 | Encounter outcome acknowledgment and completion with persistent character consequences |
| 36 | Durable disconnect allowance, autonomous assignment and safe original control return |
| 37 | Assisted incapacity recovery independent of the latest encounter |
| 38 | Explicit patient cancellation and helper decline of pending care without fabricated treatment |
| 39 | Named co-player permission offers, exact acceptance and revocation, separate from effective control |
| 40 | Explicit effective co-player assignment, scoped gameplay authority and immutable command attribution over the complete permission world |
| 41 | Collective withdrawal Core: independent side commitments, paid movement, cancellation and whole-side departure over the effective-control world |

Profile41 has explicit Core root/step codecs and complete-world transactions; SQLite recovery and
ordinary client admission do not select it. Its fresh factory cannot migrate a lived profile40 world.
The [party-withdrawal reference](party-withdrawal.md) separates those executable Core contracts from
the required preserved-source transition and connected/native adapter contracts.

[Profile 25](world-resource-history.md) includes the complete progression basis, ordered resource
changes and named attack/reward/treatment witnesses. Its structural and causal checks do not
authenticate the embedded basis. [WorldGenesisAdmission](../../src/OpenFantasy.Hosting/Worlds/WorldGenesisAdmission.cs)
admits only fresh care/resource/material owners with revision zero, no earned care or material
history, no archive and the exact initial progression basis. The
[continuation contract](resource-archive-continuation.md) produces sealed closing bytes with the
candidate and publishes them atomically through schema seven. Recovery verifies actual retrieved
archives against complete replay; a continued root requires its boundary context. Schema/profile
pairing is checked at both genesis and the replayed head. A snapshot interpreter alone is not
permission to select a storage schema. The durable authority accepts trusted selected care batches.
The [local care adapter](aftermath-care.md#local-session-and-public-observations) selects patient
acknowledgment and later helper commitment at distinct ready cutoffs. Its occupied-identity guard
survives reopening; recovery alone selects no treatment. The
[native care binding](native-encounters.md#care-interaction) submits the captured patient acknowledgment
through this adapter and reconstructs transient controls from the recovered public view.

[Profile 26](ordinary-care.md) selects Care V2 and action V14 explicitly. Self-use and living assistance
share the existing material-consumption and resource owners; retained uses identify the exact dose,
patient resource boundary, policy, permission and command position. Fresh admission pins the complete
ordinary policy and grants before play. Schema seven accepts profiles 25 and 26 explicitly at genesis
and replayed head; reopening never upgrades a lived profile-25 world. Resource continuation carries
ordinary uses and permission revocations across the same atomic archive transaction.

[Profile 27](coordinated-world-continuation.md) is an explicit fresh opt-in over the ordinary-care
graph. Its joint cut captures the actual complete world, rebases Production V9, Care V3 and resource
history together, and retains the same inventory, work, terrain/liquid, quest and actor facts. The
schema-eight table stores exact closing bytes; the world journal remains the sole ordered action log.
Decoding after a cut requires the actual joint anchor, and Host recovery verifies every archive
against complete replay. Neither opening a lived profile26 save nor supplying a component context
selects this interpretation. The embedded ordinary-care factory retains its own profile26 admission.

Profiles 28, 30, 32 and 33 use the same schema-eight physical layout with explicitly selected world
semantics. Profiles 29 and 31 remain unassigned and refused. The sole lived interpretation change is
the privileged [27-to-30 proposal transition](coordinated-world-continuation.md#explicit-proposal-transition).
[Paid profile 32](world-paid-exchange.md) has fresh admission; reopening another profile never enables
trading. Its single-command Host boundary checks independently supplied delivery before receipt
disclosure, and publishes goods, balances, quotes, journal, receipts, identities and any cut in one
transaction. Exact retries return the retained receipt. A matching root hash alone does not establish
payment history; recovery reconstructs it from the admitted genesis and complete journal.

[Market profile33](world-markets.md) also requires fresh admission. Its snapshot frame adds the
market owner and its action codec records quote issuance, observations and selected-lot disclosure
commands. Goods and payments still use the paid transaction. The market owner and its continuation
context preserve dated knowledge and disclosure causes without silently refreshing either on load.

[Profile ten](excavation.md#persistence-and-boundaries) binds exact cell/source ownership and work to
retained terrain edits and the existing custody/Production ledger. Its fresh admission and action eight
are explicit; it shares the writer and limits while excluding independent watershed/reservoir ownership.

[Profile eleven](constructed-footing.md#complete-persistence-composition) stores one complete direct
root so installed floors exist before actor admission. It never strips supporting structures to encode
or decode an intermediate legacy basis. Support surfaces derive from retained completed construction;
spatial version two selects their contact rules. Fresh admission and climb action nine are explicit.

[Profile twelve](stair-navigation.md#hosted-actors-and-persistence) uses that complete root with spatial
version three and the stepped contact interpreter. XY-only ordinary movement replays against that
explicit profile. Opening a profile-eleven save does not change its movement or contact semantics.

The [physical construction root](construction-physical.md#canonical-persistence-and-extension-boundary)
retains a direct eighteen-field frame and decodes fabrication and member state before actor admission.
Its complete root uses the same schema-three writer and identity journal; older saves keep their existing
interpretations. Completion-derived member identities and physical state publish in the same transaction
as the earned work, installed output and receipt.

The [structural policy root](structural-policy-and-motion.md#durable-stable-world-authority) appends the
complete load-policy asset as a nineteenth field. Fresh admission pins its interpretation; ordinary
successors retain it unchanged. Stable-load validation covers every resident, actual carried project stock
and installed member allocation before publication. Derived graphs and their world-dependent pins are
not encoded in the root. This keeps hashing nonrecursive while exact history recovers both the owned
policy and its material/occupancy inputs. A failure refusal is not a saved partial collapse.

[Profile fifteen](structural-settlement.md) appends settlement calibration to a direct twenty-field root.
Its ordinary advancing transaction publishes bounded intact descent, occupants and source issuance
together. It retains schema three and terminal outcomes; it has no saved partial operation or continuation.

[Profile seventeen](coupled-settlement.md) retains a direct twenty-three-field root. The
[codec](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV17.cs) keeps profile sixteen's component order,
uses spatial version four and `PendingExcavationCodecV2`, then appends the complete
[CoupledSettlementPolicy](../../src/OpenFantasy.Core/Structures/CoupledSettlementPolicy.cs) archive.
Spatial version four implies stepped actor footing and `StructureSupportMode.TerrainAndConstruction`;
older spatial interpreters require terrain-only structure support and cannot silently drop the new law.
Construction and physical members decode before their supporting spatial scene. The optional pending
cursor is restored only against the exact stable root, including its coupled policy and complete
retained load interpretation. Canonical reconstruction and the enclosing 16 MiB bound still apply.

[Fresh coupled admission](../../src/OpenFantasy.Core/Worlds/WorldCoupledSettlementAdmission.cs) accepts only
an unplayed, unheld profile-sixteen genesis with no pending work. Its admission frame pins the prior
admission, complete retained load-policy hash and coupled policy hash. It does not migrate a lived save.
Profile seventeen shares schema four's sole writer and continuation journal with profile sixteen;
its explicit step codec selects the new interpretation without changing prior step bytes. Reservation
headroom uses the admitted assembly-dependent work bound, not profile sixteen's single-assembly bound.

[Profile eighteen](event-descent.md#explicit-live-interpretation) appends the selected descent mode to a
direct twenty-four-field root and uses the V3 pending/resolution codecs. Fresh admission accepts only
an unplayed, unheld profile-seventeen genesis without pending work. Complete policies remain pinned;
ordinary successors retain the mode. The actual event scene determines the work bound used by saved
continuations and capacity reservations. Schema four and the exclusive writer remain shared; prior
profiles keep their recorded interpretation.

[Profile nineteen](reservoir-containment.md) stores a direct eighteen-field root with raw spatial
version one, fixed construction, the complete reservoir archive and the complete sealing-policy archive.
It uses schema three's atomic terminal transaction, not schema four's consequence cursor. Its fresh
admission accepts an unplayed, unheld profile-eight basis and pins the prior admission plus the complete
policy hash. Ordinary actions retain that policy. All geometric and material-pin checks run during
decode and complete candidate admission; reading an older save never installs this interpretation.

[Profile twenty](liquid-cell-flow.md) adds exact source/produced-lot cell portions as the nineteenth
field of that direct root. It requires terrain-supplied enclosure and excludes pending runoff and its
controls. Fresh admission binds an unplayed profile nineteen basis to its complete initial placement.
The liquid boundary and revision match calendar and advancing steps. Schema three commits the whole
selected flow stage atomically; fragment exhaustion throws before publication and has no resumable
cursor in this interpretation. Older profile bytes and their action semantics remain unchanged.

[Profile twenty-one](terrain-liquid-breach.md) has a direct twenty-field root, V2 liquid and extraction
components, rederived exact terrain topology and reserved liquid serialization headroom. It uses the
same schema-three writer, authenticated actions and exact history without migrating older worlds.

[Profile twenty-two](world-pacing.md) appends permanent pacing to that direct root, reserving both liquid
and pacing serialization headroom. Exclusive schema five admits explicit tactical/decision/resolution
commit kinds. The writer and exact replay remain shared; a fresh genesis cannot import elapsed time or
queued choices. The world calendar, resident exposure and last complete liquid/gathering process boundary
are distinct but exactly reconciled. Its applied Wait receipt confirms queuing, not action resolution.

[Profile twenty-three](encounter-actions.md) adds the action owner to a direct 22-field root and reserves
its complete 128 KiB allowance alongside liquid and pacing. Schema five explicitly accepts both paced
profiles. Actual HP/MP, saved damage draw position, cancellation, timing debt and ordinary queue receipts
replay in the same authority. Incapacity preserves identities/custody and outcome holds the encounter;
neither implies death rewards, migration or safe release.

[Profile twenty-four](encounter-lifecycle.md) retains a direct 22-field root with pacing at index twenty
and the complete lifecycle archive at index twenty-one. Its current/last action archive is nested in
that owner; released history cannot route ordinary work into an inactive encounter. The enclosing
16 MiB allowance reserves the maximum 512 KiB lifecycle archive alongside liquid and pacing. Exclusive
[schema six](../../src/OpenFantasy.Hosting/Worlds/WorldDatabaseSchemaSix.cs) admits lifecycle commit kind
eight while preserving schema five's definition and older meanings. Entry and release use action
protocol twelve and receive ordinary exact receipts; actionless staging has no actor receipt. Accepted
entry reserves the authority-derived episode identity in the same historical ledger. Its derivation
pins world, branch, seed, scenario/version and encounter ordinal, independent of command/cause IDs.
Each scenario's complete future identity family is reserved against generic roles; only an accepted
entry may claim the exact next member after checking actual entities and retained history.
Recovery replays these boundaries and complete clock/debt/carry relationships through the existing
writer contract. Fresh admission refuses preloaded episode history, elapsed intervals or recovery debt;
there is no implicit migration from a lived profile-twenty-two or twenty-three save.

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
geography into a lived save. Profiles six through thirteen use the same schema-three writer and full-journal
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

Action version and world profile version are distinct: runoff controls execute in profiles eight, nine
and nineteen.
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

### Canonical candidate ownership

[WorldCanonicalFrame](../../src/OpenFantasy.Core/Worlds/WorldCanonicalFrame.cs) owns canonical bytes
captured through `WorldSnapshotCodec.Encode` for one exact immutable `WorldSnapshot`. Its constructor
is private, exports copy the backing array, and its lazy digest hashes only those owned bytes. Binding
uses object identity; a different root with equal bytes cannot borrow the frame. A frame retains no
predecessor or global cache and adds no persisted flag, schema or interpreter version.

[WorldStepDecision](../../src/OpenFantasy.Core/Worlds/WorldStepDecision.cs) carries an optional frame
when preparation already encoded its final candidate. Capture stays at the existing envelope checks,
including inside per-action rejection boundaries. Action preflight frames are discarded. Movement
retains a frame only with its exact admitted final root; later refused moves preserve that pair. Care
rollback clears both candidate and frame before preparing its fallback. Archive wrappers forward the
continued candidate's frame, separately from the archive's closing root.

[WorldCommitPreparation](../../src/OpenFantasy.Hosting/Worlds/WorldCommitPreparation.cs) checks this
binding and obtains a defensive byte copy for durable rows. Candidates without a frame use the
ordinary encoder at the Host boundary. A frame and that copy can coexist while the prepared decision
is retained; consumers should release completed decisions rather than accumulate them. Serialization
reuse does not cache admission, authenticate history or authorize publication. Full world validation,
actual retrieved archive decoding and complete journal replay remain required.

### Recovery envelopes

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
Resource archives and joint material/care archives each have a separate 1,024-row/256 MiB envelope
under their exclusive schema/profile family. They cannot be combined to expand one world's budget.

### Exact starting-state selection

`SqliteWorldAuthority.OpenExisting(path, expectedGenesis)` copies and decodes the expected bytes,
requires admitted fresh genesis and canonical re-encoding, then opens an existing database and performs
complete journal/archive recovery once. Both the recovered genesis hash and stored genesis bytes must
match. A missing save or mismatch fails; a failure after opening disposes the database. The expectation
selects a world, not a checkpoint, migration, permission grant or substitute for historical admission.
World/branch/profile/admission equality alone does not prove the same original resident and world facts.

The explicit coordinated fixture route in
[FarmClientSession](../../client/scripts/FarmClientSession.cs) additionally requires profile27 at commit
and advancing step zero. It attaches only after exact-origin and ordinary selection checks. Its helper
identity comes from the player's unique ordinary-care permission, including a revoked permission;
reciprocal quest givers do not implicitly select one companion. This fixture binding is distinct from
the normal farm and ordinary-care startup routes and has no default save destination.

## Migration and publication

### Interpretation and admission identity

`WorldSnapshot.ProfileVersion`, component rules pins and `AdmissionVersion` have distinct roles.
The profile selects a complete saved interpreter; component pins select its exact laws. Admission
identifies the world's admitted lineage. It is not a freely replaceable current-version label:
[WorldResourceAdmission](../../src/OpenFantasy.Core/Worlds/WorldResourceAdmission.cs) requires the
resource history's anchor admission to match it, and
[WorldMaterialCareArchiveAnchor](../../src/OpenFantasy.Core/Worlds/WorldMaterialCareArchiveAnchor.cs)
requires successors to retain it. The resource-only
[WorldResourceArchiveAnchor](../../src/OpenFantasy.Core/Worlds/WorldResourceArchiveAnchor.cs) also
requires its profile to remain exact. These archive families have different continuation contracts.

The explicit proposal transition preserves original admission while changing the selected profile.
Its request, journal entry and replay authenticate that change; a new resource baseline is not a
substitute for lived history. Fresh-admission helpers require unplayed worlds and do not supply a
migration path. Normal `WorldSnapshot.WithState` retains the retreat rules pin rather than silently
selecting another retreat law.

[PlayerWorldCompatibility](../../src/OpenFantasy.Hosting/Networking/PlayerWorldCompatibility.cs)
matches world/branch, profile and admission against the world. Its public package pin describes the
resolved content manifests; it is not an arbitrary extension-policy channel. A changed nested law
must have an explicit supported compatibility boundary before either side accepts gameplay. Matching
public pins does not authenticate a player or a historical command.

Extending a saved interpretation requires agreement among root/component and command codecs,
complete-world admission, archive context, storage/replay dispatch, host selection and client
compatibility. SQL admitting a commit kind alone is insufficient: journal recovery refuses profile
migrations outside its named schema-three and schema-eight routes. Profiles 35–40 use the shared
[complete codec](../../src/OpenFantasy.Core/Worlds/WorldSnapshotCodecV35.cs) with a 4 KiB retreat field
and reserved component maxima under the 16 MiB root budget. A larger standalone component must fit
the complete reservation calculation; its own successful encoding does not prove that capacity.
Profile41 explicitly substitutes the collective retreat codec and512KiB reservation in that shared
complete frame; legacy versions retain their original codec and bound. This changes no database
schema, host compatibility selection or existing save's rule on opening.

### Design contract: partitioned persistence and version ownership

The concrete `WorldSnapshot` codecs encode a complete bounded root. A terrain region, watershed
network or material-source partition does not by itself provide independently resident world state.
Partitioned persistence is an extension contract; it is not an alternate implemented save format.

A partition is an ownership and storage boundary, not a second simulation authority. Stable
world/branch/entity identities and a canonical ownership directory or deterministic mapping locate
each fact independently of its detailed representation. Actor movement across a boundary preserves the person, possessions,
commitments, knowledge, control and chronology. Reading a neighbour's cached projection cannot confer
rights to mutate that neighbour or transfer its cargo.

A transaction determines its complete read/write dependency set before publication. Coupled effects
across partitions either commit under one authenticated commit identity or remain unpublished. Exact
source revisions and deterministic ordering must detect stale or conflicting proposals. Missing
residency requests bounded preparation or reports a reason to defer; it cannot omit an affected actor,
consume twice, skip a held encounter or publish half a transfer. Global uniqueness, custody, spatial
contact and causal-history invariants require cross-partition validation in addition to each facet's
local validation. Partitioning does not authorize IPC or a distributed commit service.

Recovery restores the ownership mapping, affected roots, command receipt and retained work as the same
committed transaction. Replay resolves the exact partition identities and interpreters at a historical
revision, including original admission and archive ancestry. Unloading, reloading or branching cannot
regenerate possessions, discard obligations, change due-work order or make a previously valid historical
address inaccessible. Spatial neighbourhood, knowledge visibility, simulation cadence and residency are
distinct boundaries; the [frontier contract](world-and-geography.md#design-contract-finite-preparation-and-frontier-streaming)
defines their gameplay obligations.

A per-facet manifest is a candidate mechanism for selecting exact component formats and laws; it is
not a selected runtime format. Any such manifest needs canonical ordering, content/rule identities,
required/optional presence rules, supported combination admission, bounded encoded sizes and explicit
unsupported-version refusal. Typed facet validators retain their own invariants; the composition owner
validates shared identities and coupled effects. A generic manifest cannot make arbitrary combinations
legal or remove command semantics. Root envelopes, step/action interpreters and compatibility contracts
still need versions whenever their bytes or meaning change.

Legacy read/replay interpreters remain available for every retained save and historical segment that
references them. An explicit live-head upgrade does not retire interpreters needed for earlier history
or time travel. Compatibility support may share implementation where semantics are identical, but cannot
silently reinterpret old bytes or delete lived history to bound maintenance cost. Selecting a new format
requires preserved-source publication and evidence for composed mutation, restoration and replay under
meaningful state, not only empty-root throughput.

### Preserved-source publication

[WorldProductionUpgrade](../../src/OpenFantasy.Hosting/Worlds/WorldProductionUpgrade.cs),
[WorldQuestUpgrade](../../src/OpenFantasy.Hosting/Worlds/WorldQuestUpgrade.cs) and
[WorldConstructionUpgrade](../../src/OpenFantasy.Hosting/Worlds/WorldConstructionUpgrade.cs)
validate a source, operate on a preserved copy and select a verified destination through
[WorldUpgradePublication](../../src/OpenFantasy.Hosting/Worlds/WorldUpgradePublication.cs).
Old saves and their histories remain intact. Existing selected destinations take precedence;
incompatible sources, orphaned sidecars or failed upgrades stop opening instead of creating an
apparently fresh replacement. New components cannot reset pre-existing bodies, terrain, rewards
or material custody.

[WorldProposalTransitionUpgrade](../../src/OpenFantasy.Hosting/Worlds/WorldProposalTransitionUpgrade.cs)
applies the same discipline to schema eight. It holds the exclusive source lease, recovers the source
completely, prepares one typed transition against the exact recovered head, copies a consistent backup
to a private staging directory beside an unused destination, and compares the copy's genesis, head,
journal, receipts, identities, checkpoints and both archive families with the source. It then appends
one kind-3 commit, the joint archive closing the stored prior head, the cause identity, the new head and
any scheduled checkpoint in one transaction, replays the whole copy again, and publishes it by a
non-overwriting move only when replay reproduces the prepared head bytes and journal usage. The source
file is never written. A fault before publication leaves the destination absent; a failed private copy
stays in its staging directory and is never selected. Opening any save, including this one, never
transitions it.

Recovery of the published save authenticates the stored genesis, every lived archive against replay,
the transition commit from its canonical request, the transition archive through
`DecodeTransition` with that request, and each later archive against its preceding anchor. A damaged
or missing archive, a forged request, a changed outcome, an unscheduled checkpoint or a head that
disagrees with replay refuses the whole save, including historical reads before the damage. The
[coordinated reference](coordinated-world-continuation.md#explicit-proposal-transition) defines the
request, interpretation boundary and rejection rules.

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

For a closed save, [RecordedWorldReader.ReadForHost](../../src/OpenFantasy.Hosting/Worlds/RecordedWorldReader.cs)
acquires the same exclusive database lease and selects the requested state during one complete replay.
Selection never stops verification early: later commits, checkpoints, outcomes, identity rows and the
final head must all agree before the result is returned. The lease is disposed on success or failure;
the immutable result retains the verified source-head pin without retaining a database connection.
Closed-save history, descent and reservoir-boundary operators use this path. An active authority keeps
its serialized instance operation and live-head comparison. Neither path uses a historical-state cache,
trusts a checkpoint in place of replay, creates a missing save or advances pending work.

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

## Reserved physical consequences

The [durable consequence contract](consequence-continuation.md) retains eligible profile-sixteen and profile-seventeen excavation
work across explicit continuation commits, each under its own geometric interpreter and finite work bound. The authoritative scene and game clocks remain unchanged until
terminal publication charges the reserved advancing interval once, including on refusal. Active sessions
resume before client readiness; read-only opening and recorded history do not advance pending work.

## External controller decisions

[Durable character control](controller-lifecycle.md) uses explicit world profile36 and storage schema9.
Its external events enter the same journal and transaction as gameplay. They retain the world/lifecycle
boundary revision relationship while earning no simulation time, spending no resources and preserving
accepted choices. Recovery consumes recorded evidence without a clock; ordinary save restoration
changes no control assignment. Previous profiles and schema checks remain explicit and unchanged.
