# Native gift fixture and authority authentication

Assigned scope: new `Client/NativeGiftFixtureTests.cs`, `Client/NativeGiftResultTests.cs`, and this
record only. Root owns native UI/probe/runner, integration, checkpoints and verification. No worker
build, test, Git operation or external agent request was executed. Checks below describe assertions
implemented in source, not successful runs.

## Fixture protocol

`NativeGiftFixtureTests` always constructs a real SQLite world using
`WorldMaterialProposalAdmission.CreateFresh(new OrdinaryCareWorldFixture().Genesis)`. The existing
`OrdinaryCareWorldFixture` / `CareRewardJourney` executes actual source-stat defeat, acknowledged
finite care, deliberate Wait/bites, victory, readiness, and two helper crafts consuming three source
ingredients each. The patient executes `RevokeCarePermissionIntent` before the native boundary.
No state/HP/stock patch, invented lot or lived-save migration is used.

When `OPENFANTASY_NATIVE_GIFT_FIXTURES` names a fresh or empty directory, export writes:

- `save.sqlite`: actual authoritative backup before any gift exists.
- `genesis.ofc`: exact fresh canonical genesis.
- `before.ofc`: exact canonical snapshot before native launch.
- `metadata.json`: identities, custody/source intervals/counts, craft provenance and expected routes.
- `manifest.json`: absolute paths for all four inputs.

Existing directory contents are rejected. The export pins `playerId`, `helperId`, `recipientId`
(living West wolf, second encounter scenario), `rawLotId`, `incomingLotId`, `incomingCommandId` and
`incomingProposalId`. Incoming IDs are computed from the controller's read-only selection; no offer
exists in the exported world. The native session creates it using explicit
`LocalMaterialGiftPolicy(helper, player, selfUseDefinition, version, offerLifetimeSteps: 1200)` with
both giving and answering enabled, reserve one and desired stock three.

Two helper doses permit one accepted gift while retaining one. A declined preferred gift does not
produce a replacement: its stable semantic decision is occupied. The West wolf has an actual owned
inventory and no gift policy. Creating then withdrawing the player's actual one-unit raw offer is
legal; this does not claim that the wolf can accept or manipulate the item.

The fixture's managed continuation independently exercises actual scheduled creation, acceptance,
outgoing creation and withdrawal, checks unchanged health/care and raw stock, and runs even without
the export variable. Native runs are independent copies of the exported pre-offer save.

## Results protocol and assertions

`OPENFANTASY_NATIVE_GIFT_RESULTS` names a runner manifest with
`results: [{mode, save, genesis, before, metadata, report, reopenReport}]`, all paths absolute.
Exactly three distinct saves are required: keyboard and controller accept; pointer declines.
Every route creates and withdraws an outgoing raw offer to the pinned living West wolf.

Reports use `Kind: native-gifts-v1`, `Status: passed`, the actual `Mode`, `IncomingProposalId`,
`OutgoingProposalId` and `ReplyStatus`. Separate reopen reports retain and independently verify
those IDs/outcomes. `InitialHash`/`FinalHash` are uppercase SHA256 of the default JSON serialization
of `LocalWorldView`, not canonical world hashes. The authenticator reconstructs the public view
from the verified authoritative final state and checks both reports against it. It retains the
150-second bound, paused result, zero focus requests and non-headless renderer checks.

Authentication opens the save with exact expected genesis, verifies the complete journal,
receipts, identity ledger, checkpoints and any archives through the existing authority, then checks
the retained fixture boundary and each native action boundary. Exactly four new applied commands
are required, with exact order, actors, controls, delivery, identities, receipt hashes, action bytes,
advancing-step/calendar effects and immutable receipt retrieval. Incoming policy identity is
recomputed and preferred crafted lot checked. Acceptance checks the exact produced output interval,
settlement owner hashes and one-lot custody/revision change; decline checks no stock movement.
Withdrawal preserves the exact raw source interval and custody. Raw lots/sources, production
batches/consumption, health, care, actor positions and helper reserve remain checked. Closing and
reopening authority must leave the closed SQLite bytes unchanged.

The result test explicitly skips without a results manifest. No native/device, accepted-dose
healing, original post-cut 520-dose journey, archive-cut, human or production acceptance is claimed
by adding these tests. The player's earlier raw dose remains present. This is test/evidence plumbing
over existing ownership and interfaces; it introduces no architectural contract change.
