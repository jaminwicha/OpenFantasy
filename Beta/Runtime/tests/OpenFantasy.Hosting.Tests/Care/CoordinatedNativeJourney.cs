using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>One accepted player movement sample and the exact positions it joined.</summary>
internal sealed record WalkStep(ulong Sequence, long DeltaX, long DeltaY, SubunitPosition From, SubunitPosition To);

/// <summary>
/// Prepares the native withdrawal fixture from a copy of the retained 520-dose ledger. Preparation is
/// earned readiness and accepted player movement samples only; the retained source is only read.
/// </summary>
public sealed class CoordinatedNativeJourney : IDisposable
{
    internal const string FixtureName = "coordinated-withdrawal";
    internal const string SourceVariable = "OPENFANTASY_COORDINATED_NATIVE_SOURCE";
    internal const string EvidenceVariable = "OPENFANTASY_COORDINATED_NATIVE_EVIDENCE";
    internal const int WalkingStepLimit = 64;
    private const string ExpectedAction = "PatientRevokeCarePermission";
    private const int ReadinessStepLimit = 64;
    private const ulong RetainedPotions = 80;
    private const ulong RetainedUses = 520;
    private const long Stride = WorldMovementInput.DisplacementPerStep;
    private static readonly (long X, long Y)[] Cardinals = [(Stride, 0), (-Stride, 0), (0, Stride), (0, -Stride)];
    private static readonly string[] SourceFiles =
        ["final.sqlite3", "final.ofc", "before.sqlite3", "before.ofc", "after.sqlite3", "after.ofc", "batch.ofc", "metadata.json"];
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "openfantasy-native-journey-" + Guid.NewGuid().ToString("N"));
    private ulong _identity;

    public CoordinatedNativeJourney()
    {
        if (Environment.GetEnvironmentVariable(SourceVariable) is not { Length: > 0 } source) return;
        SourceDirectory = source;
        Directory.CreateDirectory(FixtureDirectory);
        SourceSha256 = HashSource(source);
        foreach (string suffix in new[] { "-wal", "-shm", "-journal" })
        {
            Assert.False(File.Exists(Path.Combine(source, "final.sqlite3") + suffix), "The retained source must be a closed backup.");
        }
        string working = Path.Combine(_directory, "working.sqlite3");
        File.Copy(Path.Combine(source, "final.sqlite3"), working);
        using (WorldDatabase stored = WorldDatabase.Open(working, create: false))
        {
            GenesisBytes = stored.ReadGenesis();
            SourceLedgerSha256 = LedgerDigest(stored, stored.ReadHead().Sequence);
        }
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(working, GenesisBytes))
        {
            Prepare(authority);
            authority.BackupForHost(BeforeDatabasePath);
        }
        using (WorldDatabase backup = WorldDatabase.Open(BeforeDatabasePath, create: false))
        {
            BeforeLedgerSha256 = LedgerDigest(backup, Before.Revision);
        }
        WriteFixture();
        IsPrepared = true;
        PublishEvidence();
    }

    internal bool IsPrepared { get; }
    internal string SourceDirectory { get; } = "";
    internal IReadOnlyDictionary<string, string> SourceSha256 { get; } = new Dictionary<string, string>();
    internal string FixtureDirectory => Path.Combine(_directory, FixtureName);
    internal string BeforeDatabasePath => Path.Combine(FixtureDirectory, "before.sqlite3");
    internal string GenesisPath => Path.Combine(FixtureDirectory, "genesis.ofc");
    internal byte[] GenesisBytes { get; } = [];
    internal string BeforeLedgerSha256 { get; } = "";
    internal string SourceLedgerSha256 { get; } = "";
    internal int InReachAutomaticProposalCount { get; private set; }
    internal EntityId Player { get; private set; }
    internal EntityId Helper { get; private set; }
    internal WorldSnapshot Retained { get; private set; } = null!;
    internal ulong RetainedArchives { get; private set; }
    internal WorldSnapshot InReach { get; private set; } = null!;
    internal WorldSnapshot Before { get; private set; } = null!;
    internal ulong BeforeArchives { get; private set; }
    internal int ReadinessSteps { get; private set; }
    internal List<WalkStep> Walk { get; } = [];
    internal EntityId HelperStockLot { get; private set; }
    internal long RemedyRangeSubunits { get; private set; }
    internal RevokeCarePermissionIntent ExpectedRevoke { get; private set; } = null!;
    internal WorldSnapshot ExpectedImmediate { get; private set; } = null!;
    internal bool ExpectedImmediateSealsArchive { get; private set; }

    internal IReadOnlyDictionary<string, string> HashSource() => HashSource(SourceDirectory);

    private static Dictionary<string, string> HashSource(string directory) => SourceFiles.ToDictionary(name => name,
        name => Sha256(Path.Combine(directory, name)), StringComparer.Ordinal);

    /// <summary>The helper's exact aid choice from its retained stock at this boundary's own revisions.</summary>
    internal static UseRestorativeItemIntent CaptureHelperAid(WorldSnapshot state, EntityId helper, EntityId stockLot)
    {
        CarePermission permission = Assert.Single(state.Care!.Ordinary!.Permissions);
        Assert.Equal(helper, permission.HelperActorId);
        var definition = state.Care.Ordinary.Definitions.Single(value => value.DefinitionId == permission.UseDefinitionId);
        var stock = state.Inventory.Custody.Lots.Single(value => value.LotId == stockLot);
        EntityId storage = state.GetResident(helper).InventoryCustodianId;
        EntityId patient = permission.PatientActorId;
        return new(patient, definition.DefinitionId, definition.Version, permission.PermissionId, permission.GrantVersion,
            permission.Revision, state.Space.GetActor(patient).Revision, state.Progression!.GetActor(patient).Revision,
            state.Inventory.Custody.GetStorage(storage).Revision, state.Production!.Revision,
            new IngredientRequest(IngredientLotKind.Source, stock.LotId, stock.Revision, 1));
    }

    private void Prepare(SqliteWorldAuthority authority)
    {
        WorldSnapshot state = authority.ReadStateForHost();
        Retained = state;
        Assert.Equal(RetainedUses, state.Care!.TotalOrdinaryUses);
        Assert.Equal(SourceSha256["final.ofc"], state.ContentHash.Value);
        RetainedArchives = authority.InspectForHost().Journal.MaterialCareArchiveCount;
        CarePermission permission = Assert.Single(state.Care!.Ordinary!.Permissions);
        Assert.True(permission.IsActive);
        Player = permission.PatientActorId;
        Helper = permission.HelperActorId;
        var remedy = Assert.Single(state.Care.Remedies);
        RemedyRangeSubunits = checked((long)remedy.RangeSubunits);
        EntityId helperStorage = state.GetResident(Helper).InventoryCustodianId;
        var stock = state.Inventory.Custody.Lots.Single(lot => lot.CustodianId == helperStorage && lot.Definition == remedy.Item);
        Assert.Equal(RetainedPotions, stock.Quantity);
        HelperStockLot = stock.LotId;

        // Ready and within reach the helper's exact aid is admissible; the walk then changes position alone.
        state = EarnReadiness(authority, state, Player, Helper);
        InReach = state;
        Assert.Equal(WorldActionRejection.None,
            WorldRestorativeActions.Evaluate(state, Helper, CaptureHelperAid(state, Helper, HelperStockLot)));
        var automatic = new LocalRestorativeController().SelectActions(state, authority.IsIdentityOccupiedForHost,
            new HashSet<EntityId>());
        InReachAutomaticProposalCount = automatic.Count;
        Assert.All(automatic, action => Assert.Equal(Helper, action.ActorId));
        state = WalkOutOfReach(authority, state);
        state = EarnReadiness(authority, state, Player, Helper);
        Before = state;
        BeforeArchives = authority.InspectForHost().Journal.MaterialCareArchiveCount;

        Assert.Equal(WorldActionRejection.OutOfReach,
            WorldRestorativeActions.Evaluate(state, Helper, CaptureHelperAid(state, Helper, HelperStockLot)));
        Assert.Empty(new LocalRestorativeController().SelectActions(state, authority.IsIdentityOccupiedForHost,
            new HashSet<EntityId>()));
        Assert.Empty(new LocalNpcController().SelectMovement(state));

        // The withdrawal is only previewed here; the native wheel commits the authoritative one.
        CarePermission kept = Assert.Single(state.Care!.Ordinary!.Permissions);
        Assert.True(kept.IsActive);
        Assert.Equal(permission.Version, kept.Version);
        ExpectedRevoke = new(kept.PermissionId, kept.Revision, kept.GrantVersion);
        WorldStepDecision preview = authority.Preview(Batch(authority, state, [Command(authority, state, Player, ExpectedRevoke)]));
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(preview.Receipts).Status);
        ExpectedImmediate = preview.Candidate;
        ExpectedImmediateSealsArchive = preview.SealedMaterialCareSegment is not null;
        Assert.Equal(state.ContentHash, authority.ReadStateForHost().ContentHash);
    }

    private WorldSnapshot WalkOutOfReach(SqliteWorldAuthority authority, WorldSnapshot state)
    {
        while (state.Space.QueryInteraction(Helper, Player, RemedyRangeSubunits) == SpatialInteraction.Reachable)
        {
            Assert.True(Walk.Count < WalkingStepLimit, "The player must leave remedy reach within the finite movement bound.");
            state = EarnReadiness(authority, state, Player);
            WorldSnapshot basis = state;
            SubunitPosition feet = basis.Space.GetActor(Player).Feet;
            SubunitPosition helper = basis.Space.GetActor(Helper).Feet;

            // The actual route query admits cardinal samples; the farthest admitted one separates the pair.
            var admitted = Cardinals.Select(delta => (Delta: delta, Destination: feet.Offset(delta.X, delta.Y, 0)))
                .Where(value => basis.Space.QueryGroundRoute(Player, value.Destination).Status == GroundRouteStatus.Reachable)
                .OrderByDescending(value => SquaredDistance(value.Destination, helper)).ToArray();
            Assert.True(admitted.Length > 0, "No cardinal movement sample has a reachable ground route.");
            var choice = admitted[0];
            Assert.True(SquaredDistance(choice.Destination, helper) > SquaredDistance(feet, helper),
                "Every reachable cardinal sample approaches the helper.");

            state = CommitMovement(authority, basis, choice.Delta.X, choice.Delta.Y);
            Assert.Equal(choice.Destination, state.Space.GetActor(Player).Feet);
            Assert.Equal(Others(basis, Player), Others(state, Player));
            Walk.Add(new(state.Revision, choice.Delta.X, choice.Delta.Y, feet, choice.Destination));
        }
        return state;
    }

    private WorldSnapshot CommitMovement(SqliteWorldAuthority authority, WorldSnapshot state, long deltaX, long deltaY)
    {
        ActorControlBinding control = state.Inventory.Controls.Single(value => value.ActorId == Player);
        var sample = new WorldMovementInput(new(control.PrincipalId, control.SessionId), Player, deltaX, deltaY);
        WorldStepDecision step = authority.Commit(new(NextIdentity(authority), state.WorldId, state.BranchId, state.Revision,
            state.AdvancingSteps, WorldBatchKind.AdvancingStep, [sample], [], state.ProfileVersion));
        Assert.Empty(step.Receipts);
        Assert.Equal(state.Revision + 1, step.Candidate.Revision);
        Assert.Equal(state.AdvancingSteps + 1, step.Candidate.AdvancingSteps);
        return step.Candidate;
    }

    private static long SquaredDistance(SubunitPosition from, SubunitPosition to) =>
        checked((from.X - to.X) * (from.X - to.X) + (from.Y - to.Y) * (from.Y - to.Y));

    private static (EntityId, SubunitPosition, ulong)[] Others(WorldSnapshot world, EntityId moving) => world.Space.Actors
        .Where(value => value.ActorId != moving).Select(value => (value.ActorId, value.Feet, value.Revision)).ToArray();

    private WorldSnapshot EarnReadiness(SqliteWorldAuthority authority, WorldSnapshot state, params EntityId[] actors)
    {
        while (actors.Any(actor => !WorldActorAvailability.CanAct(state, actor)))
        {
            Assert.True(ReadinessSteps++ < ReadinessStepLimit, "Readiness must be earned within the finite fixture step bound.");
            WorldStepDecision step = authority.Commit(Batch(authority, state, []));
            Assert.Empty(step.Receipts);
            Assert.Equal(state.AdvancingSteps + 1, step.Candidate.AdvancingSteps);
            state = step.Candidate;
        }
        return state;
    }

    private WorldActionCommand Command(SqliteWorldAuthority authority, WorldSnapshot state, EntityId actor, WorldActionIntent intent)
    {
        ActorControlBinding control = state.Inventory.Controls.Single(value => value.ActorId == actor);
        return new(NextIdentity(authority), control.PrincipalId, control.SessionId, actor, state.WorldId, state.BranchId,
            state.Space.GetActor(actor).Revision, intent);
    }

    private WorldStepBatch Batch(SqliteWorldAuthority authority, WorldSnapshot state, IEnumerable<WorldActionCommand> actions) =>
        new(NextIdentity(authority), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
            WorldBatchKind.AdvancingStep, [], actions, state.ProfileVersion);

    /// <summary>Fresh preparation identities; the retained ledger already occupies the original fixture's sequence.</summary>
    private EntityId NextIdentity(SqliteWorldAuthority authority)
    {
        var identity = new EntityId(0xc027_fe00_0000_0000, ++_identity);
        Assert.False(authority.IsIdentityOccupiedForHost(identity), "A preparation identity aliases the retained ledger.");
        return identity;
    }

    private static object Position(SubunitPosition value) => new { x = value.X, y = value.Y, z = value.Z };

    private void WriteFixture()
    {
        File.WriteAllBytes(GenesisPath, GenesisBytes);
        File.WriteAllBytes(Path.Combine(FixtureDirectory, "before.ofc"), WorldSnapshotCodec.Encode(Before));
        File.WriteAllBytes(Path.Combine(FixtureDirectory, "expected-immediate-withdrawal.ofc"), WorldSnapshotCodec.Encode(ExpectedImmediate));
        var player = Before.Progression!.GetActor(Player);
        var expected = ExpectedImmediate.Progression!.GetActor(Player);
        CarePermission permission = Before.Care!.Ordinary!.Permissions.Single();
        File.WriteAllText(Path.Combine(FixtureDirectory, "metadata.json"), JsonSerializer.Serialize(new
        {
            fixture = FixtureName, sourceStat = false,
            setupContract = "Scripted Host preparation on the copied 520-dose ledger uses accepted player movement and readiness; "
                + "it does not schedule autonomous helper policy. No action command, permission change, stock change or restoration occurs.",
            preparationController = "Explicit Host batches, not the live LocalWorldSession scheduler",
            inReachAutomaticProposalCount = InReachAutomaticProposalCount,
            reachEvidence = "Diagnostic Evaluate and Assess on the actual prepared state, not a rejected command receipt",
            fixtureBoundary = "Both actors are ready and the care permission is active; the helper keeps all 80 raw Potions but its "
                + "aid fails for reach. Native continuation begins here and commits the patient's withdrawal.",
            statistics = "Explicit fresh stress statistics from the retained ledger; all injury, restoration and stock changes are accepted actions.",
            sourceDirectory = SourceDirectory, sourceSha256 = SourceSha256,
            sourceLedgerSha256 = SourceLedgerSha256,
            worldId = Before.WorldId.ToString(), branchId = Before.BranchId.ToString(),
            admissionHash = Before.AdmissionVersion.Value, profileVersion = Before.ProfileVersion,
            genesisHash = ContentHash.Compute(GenesisBytes).Value,
            playerActorId = Player.ToString(), helperActorId = Helper.ToString(),
            retainedRevision = Retained.Revision, retainedHash = Retained.ContentHash.Value, retainedArchives = RetainedArchives,
            retainedHp = Retained.Progression!.GetActor(Player).CurrentHp,
            inReachRevision = InReach.Revision, inReachHash = InReach.ContentHash.Value,
            beforeRevision = Before.Revision, beforeHash = Before.ContentHash.Value, beforeLedgerSha256 = BeforeLedgerSha256,
            beforeCalendar = new { days = Before.Calendar.Days, millisecondsWithinDay = Before.Calendar.MillisecondsWithinDay },
            beforeAdvancingSteps = Before.AdvancingSteps, beforeArchives = BeforeArchives, readinessSteps = ReadinessSteps,
            walkingSteps = Walk.Count, walkingStepLimit = WalkingStepLimit,
            walk = Walk.Select(step => new
            {
                sequence = step.Sequence, deltaX = step.DeltaX, deltaY = step.DeltaY,
                from = Position(step.From), to = Position(step.To)
            }).ToArray(),
            playerFeet = Position(Before.Space.GetActor(Player).Feet), helperFeet = Position(Before.Space.GetActor(Helper).Feet),
            remedyRangeSubunits = RemedyRangeSubunits,
            helperInteraction = Before.Space.QueryInteraction(Helper, Player, RemedyRangeSubunits).ToString(),
            helperAidRejection = WorldActionRejection.OutOfReach.ToString(),
            helperWanderRadiusCells = Before.GetResident(Helper).WanderRadiusCells,
            beforeHp = player.CurrentHp, beforeMp = player.CurrentMp, maximumHp = player.Stats.MaxHp,
            beforeProgressionRevision = player.Revision, beforeTotalOrdinaryUses = Before.Care.TotalOrdinaryUses,
            helperStockLotId = HelperStockLot.ToString(), helperStockQuantity = RetainedPotions,
            permissionId = permission.PermissionId.ToString(), permissionActive = permission.IsActive,
            permissionRevision = permission.Revision, permissionVersion = permission.Version.Value,
            permissionGrantVersion = permission.GrantVersion.Value,
            expectedAction = ExpectedAction, expectedActorId = Player.ToString(),
            expectedHp = expected.CurrentHp, expectedMp = expected.CurrentMp,
            expectedTotalOrdinaryUses = ExpectedImmediate.Care!.TotalOrdinaryUses, expectedPermissionActive = false,
            expectedImmediateSealsArchive = ExpectedImmediateSealsArchive
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void PublishEvidence()
    {
        if (Environment.GetEnvironmentVariable(EvidenceVariable) is not { Length: > 0 } root) return;
        string target = Path.Combine(root, FixtureName);
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(FixtureDirectory))
        {
            // Published evidence is retained: a repeated label fails here instead of replacing it.
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: false);
        }
    }

    /// <summary>
    /// Authenticates a closed ledger that continued the exported fixture by one patient withdrawal. The
    /// continued frames are read from the reopened world; none is decoded without its archives.
    /// </summary>
    internal static void Authenticate(string databasePath, string fixtureDirectory)
    {
        using var metadataDocument = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixtureDirectory, "metadata.json")));
        JsonElement metadata = metadataDocument.RootElement;
        Assert.Equal(FixtureName, Text(metadata, "fixture"));
        Assert.Equal(ExpectedAction, Text(metadata, "expectedAction"));
        byte[] genesis = File.ReadAllBytes(Path.Combine(fixtureDirectory, "genesis.ofc"));
        byte[] preparedBytes = File.ReadAllBytes(Path.Combine(fixtureDirectory, "before.ofc"));
        Assert.Equal(Text(metadata, "genesisHash"), ContentHash.Compute(genesis).Value);
        Assert.Equal(Text(metadata, "beforeHash"), ContentHash.Compute(preparedBytes).Value);
        ulong retainedRevision = metadata.GetProperty("retainedRevision").GetUInt64();
        ulong preparedRevision = metadata.GetProperty("beforeRevision").GetUInt64();
        EntityId player = EntityId.Parse(Text(metadata, "playerActorId"));
        EntityId helper = EntityId.Parse(Text(metadata, "helperActorId"));
        byte[] closedBytes = File.ReadAllBytes(databasePath);
        string originalLedger = VerifyOriginalSource(metadata, genesis);

        StoredWorldCommit[] preparation;
        StoredWorldCommit[] commits;
        StoredWorldCommandOutcome[] outcomes;
        StoredWorldMaterialCareArchive[] archives;
        using (WorldDatabase stored = WorldDatabase.Open(databasePath, create: false))
        {
            Assert.Equal(genesis, stored.ReadGenesis());
            Assert.Equal(originalLedger, LedgerDigest(stored, retainedRevision));
            Assert.Equal(Text(metadata, "beforeLedgerSha256"), LedgerDigest(stored, preparedRevision));
            preparation = stored.ReadJournal(retainedRevision).TakeWhile(value => value.Sequence <= preparedRevision).ToArray();
            Assert.Empty(stored.ReadCommandOutcomes(retainedRevision).TakeWhile(value => value.Sequence <= preparedRevision));
            commits = stored.ReadJournal(preparedRevision).ToArray();
            outcomes = stored.ReadCommandOutcomes(preparedRevision).ToArray();
            archives = stored.ReadMaterialCareArchives().ToArray();
        }
        VerifyArchivePrefix(metadata, fixtureDirectory, preparedRevision, archives);

        // One command follows the prepared boundary: the withdrawal. Any helper dose, personal use
        // or other accepted or rejected action would appear here as another retained outcome.
        Assert.NotEmpty(commits);
        StoredWorldCommandOutcome row = Assert.Single(outcomes);
        WorldActionCommand command = WorldActionCodec.Decode(row.Command);
        WorldActionReceipt receipt = WorldReceiptCodecV1.Decode(row.Receipt);
        var intent = Assert.IsType<RevokeCarePermissionIntent>(command.Intent);
        Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        Assert.Equal(row.Sequence, receipt.CommitSequence);
        Assert.Equal(command.CommandId, receipt.CommandId);
        Assert.Equal(command.ContentHash, receipt.CommandHash);
        StoredWorldCommit committed = commits.Single(value => value.Sequence == row.Sequence);
        WorldStepBatch batch = WorldStepCodec.Decode(committed.AcceptedBatch);
        Assert.Equal(WorldBatchKind.AdvancingStep, batch.Kind);
        Assert.Empty(batch.Movement);
        Assert.Equal(row.Command, WorldActionCodec.Encode(Assert.Single(batch.Actions)));
        foreach (StoredWorldCommit other in commits.Where(value => value.Sequence != row.Sequence))
        {
            WorldStepBatch idle = WorldStepCodec.Decode(other.AcceptedBatch);
            Assert.Empty(idle.Actions);
            Assert.Empty(idle.Movement);
        }

        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(databasePath, genesis))
        {
            WorldSnapshot final = authority.ReadStateForHost();
            WorldJournalUsage usage = authority.InspectForHost().Journal;
            Assert.Equal((ushort)27, final.ProfileVersion);
            Assert.Equal((ulong)archives.Length, usage.MaterialCareArchiveCount);
            Assert.Equal(commits[^1].Sequence, final.Revision);
            Assert.Equal(commits[^1].AfterHash, final.ContentHash);

            RecordedWorldState retained = authority.ReadRecordedStateForHost(retainedRevision);
            RecordedWorldState prepared = authority.ReadRecordedStateForHost(preparedRevision);
            RecordedWorldState before = authority.ReadRecordedStateForHost(row.Sequence - 1);
            RecordedWorldState after = authority.ReadRecordedStateForHost(row.Sequence);
            Assert.Equal(Text(metadata, "retainedHash"), retained.StateHash.Value);
            Assert.Equal(RetainedUses, retained.State.Care!.TotalOrdinaryUses);
            Assert.Equal(preparedBytes, WorldSnapshotCodec.Encode(prepared.State));
            Assert.Equal(final.Revision, after.SourceHeadRevision);
            Assert.Equal(final.ContentHash, after.SourceHeadHash);
            Assert.Equal(committed.BeforeHash, before.StateHash);
            Assert.Equal(committed.AfterHash, after.StateHash);
            Assert.Equal(batch.ExpectedRevision, before.State.Revision);
            VerifyPreparation(metadata, preparation, retained.State, prepared.State, player);
            VerifyWithdrawal(metadata, prepared.State, before.State, after.State, command, intent, player, helper);
            VerifyHead(metadata, prepared.State, after.State, final, helper);

            var delivery = new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId);
            Assert.Equal(row.Receipt, WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(authority.FindReceipt(command, delivery))));
            Assert.True(authority.IsIdentityOccupiedForHost(command.CommandId));
            RecordedWorldMaterialCareArchive first = authority.ReadMaterialCareArchiveForHost(0);
            Assert.Equal(Describe(archives[0].Reference), Describe(first.Segment.Reference));
            Assert.Equal(archives[0].State, first.Segment.Encode());
            Assert.Equal(final.ContentHash, first.SourceHeadHash);
        }
        Assert.Equal(closedBytes, File.ReadAllBytes(databasePath));
        RefuseDamagedPriorArchive(databasePath, genesis);
        Assert.Equal(closedBytes, File.ReadAllBytes(databasePath));
    }

    private static string VerifyOriginalSource(JsonElement metadata, byte[] genesis)
    {
        string? source = Environment.GetEnvironmentVariable(SourceVariable);
        Assert.False(string.IsNullOrWhiteSpace(source), "Original retained source is required for native authentication.");
        JsonElement pins = metadata.GetProperty("sourceSha256");
        Assert.Equal(SourceFiles.Order(), pins.EnumerateObject().Select(value => value.Name).Order());
        foreach (string name in SourceFiles)
        {
            Assert.Equal(Text(pins, name), Sha256(Path.Combine(source!, name)));
        }
        Assert.Equal(Text(pins, "final.ofc"), Text(metadata, "retainedHash"));
        Assert.Equal(RetainedUses, metadata.GetProperty("beforeTotalOrdinaryUses").GetUInt64());
        using var copy = new StorageFiles();
        File.Copy(Path.Combine(source!, "final.sqlite3"), copy.Path);
        using WorldDatabase original = WorldDatabase.Open(copy.Path, create: false);
        Assert.Equal(genesis, original.ReadGenesis());
        var head = original.ReadHead();
        Assert.Equal(metadata.GetProperty("retainedRevision").GetUInt64(), head.Sequence);
        Assert.Equal(File.ReadAllBytes(Path.Combine(source!, "final.ofc")), head.State);
        string ledger = LedgerDigest(original, head.Sequence);
        Assert.Equal(Text(metadata, "sourceLedgerSha256"), ledger);
        return ledger;
    }

    private static void VerifyArchivePrefix(JsonElement metadata, string fixtureDirectory, ulong preparedRevision,
        StoredWorldMaterialCareArchive[] archives)
    {
        using var prepared = new StorageFiles();
        File.Copy(Path.Combine(fixtureDirectory, "before.sqlite3"), prepared.Path);
        using WorldDatabase fixture = WorldDatabase.Open(prepared.Path, create: false);
        Assert.Equal(Text(metadata, "beforeLedgerSha256"), LedgerDigest(fixture, preparedRevision));
        StoredWorldMaterialCareArchive[] retained = fixture.ReadMaterialCareArchives().ToArray();
        Assert.Equal(metadata.GetProperty("beforeArchives").GetUInt64(), (ulong)retained.Length);
        Assert.True(retained.Length >= 3 && archives.Length >= retained.Length);
        for (int index = 0; index < retained.Length; index++)
        {
            Assert.Equal(Describe(retained[index].Reference), Describe(archives[index].Reference));
            Assert.Equal(retained[index].PublicationSequence, archives[index].PublicationSequence);
            Assert.Equal(retained[index].State, archives[index].State);
        }
    }

    /// <summary>Preparation holds no command: linked idle steps and the recorded player samples alone.</summary>
    private static void VerifyPreparation(JsonElement metadata, StoredWorldCommit[] preparation, WorldSnapshot retained,
        WorldSnapshot prepared, EntityId player)
    {
        JsonElement[] walk = metadata.GetProperty("walk").EnumerateArray().ToArray();
        Assert.InRange(walk.Length, 1, WalkingStepLimit);
        Assert.Equal(prepared.Revision - retained.Revision, (ulong)preparation.Length);
        ActorControlBinding control = retained.Inventory.Controls.Single(value => value.ActorId == player);
        SubunitPosition feet = retained.Space.GetActor(player).Feet;
        ContentHash linked = retained.ContentHash;
        int walked = 0;
        foreach (StoredWorldCommit commit in preparation)
        {
            Assert.Equal(linked, commit.BeforeHash);
            linked = commit.AfterHash;
            WorldStepBatch batch = WorldStepCodec.Decode(commit.AcceptedBatch);
            Assert.Equal(WorldBatchKind.AdvancingStep, batch.Kind);
            Assert.Empty(batch.Actions);
            if (batch.Movement.Count == 0) continue;

            WorldMovementInput sample = Assert.Single(batch.Movement);
            Assert.True(walked < walk.Length, "The ledger holds an unrecorded preparation movement.");
            JsonElement step = walk[walked++];
            Assert.Equal(player, sample.ActorId);
            Assert.Equal(new TrustedCommandDelivery(control.PrincipalId, control.SessionId), sample.Delivery);
            Assert.Equal(step.GetProperty("sequence").GetUInt64(), commit.Sequence);
            Assert.Equal(step.GetProperty("deltaX").GetInt64(), sample.DeltaX);
            Assert.Equal(step.GetProperty("deltaY").GetInt64(), sample.DeltaY);
            Assert.Equal(feet, Read(step.GetProperty("from")));
            feet = feet.Offset(sample.DeltaX, sample.DeltaY, 0);
            Assert.Equal(feet, Read(step.GetProperty("to")));
        }
        Assert.Equal(walk.Length, walked);
        Assert.Equal(prepared.ContentHash, linked);
        Assert.Equal(feet, prepared.Space.GetActor(player).Feet);
        Assert.Equal(Others(retained, player), Others(prepared, player));
        Assert.Equal(retained.Care!.ContentHash, prepared.Care!.ContentHash);
        Assert.Equal(retained.Production!.ContentHash, prepared.Production!.ContentHash);
        Assert.Equal(retained.Inventory.ContentHash, prepared.Inventory.ContentHash);
        Assert.Equal(retained.Progression!.ContentHash, prepared.Progression!.ContentHash);
    }

    private static SubunitPosition Read(JsonElement position) => new(position.GetProperty("x").GetInt64(),
        position.GetProperty("y").GetInt64(), position.GetProperty("z").GetInt64());

    private static void VerifyWithdrawal(JsonElement metadata, WorldSnapshot prepared, WorldSnapshot before, WorldSnapshot after,
        WorldActionCommand command, RevokeCarePermissionIntent intent, EntityId player, EntityId helper)
    {
        Assert.Equal(before.Calendar.AddMilliseconds(1_800), after.Calendar);
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        Assert.Equal(before.Space.Actors.Select(value => (value.ActorId, value.Feet, value.Revision)),
            after.Space.Actors.Select(value => (value.ActorId, value.Feet, value.Revision)));
        Assert.Equal(prepared.Space.Actors.Select(value => (value.ActorId, value.Feet, value.Revision)),
            after.Space.Actors.Select(value => (value.ActorId, value.Feet, value.Revision)));
        Assert.Equal(prepared.Residents.Select(value => (value.ActorId, value.InventoryCustodianId, value.ControllerKind)),
            after.Residents.Select(value => (value.ActorId, value.InventoryCustodianId, value.ControllerKind)));

        // The patient's own admitted control commits the withdrawal; no helper or Host credential does.
        Assert.Equal(player, command.ActorId);
        Assert.Equal(Text(metadata, "expectedActorId"), command.ActorId.ToString());
        Assert.Equal(prepared.WorldId, command.WorldId);
        Assert.Equal(prepared.BranchId, command.BranchId);
        ActorControlBinding control = before.Inventory.Controls.Single(value => value.ActorId == player);
        Assert.Equal(control.PrincipalId, command.PrincipalId);
        Assert.Equal(control.SessionId, command.OriginSessionId);
        Assert.Equal(ResidentControllerKind.Player, before.GetResident(player).ControllerKind);
        Assert.Equal(before.Space.GetActor(player).Revision, command.ExpectedActorRevision);

        // Until this command the original grant is active, and the ready helper fails for reach alone.
        foreach (WorldSnapshot world in new[] { prepared, before })
        {
            CarePermission active = VerifyRetainedOwners(metadata, world, player, helper, active: true);
            Assert.Equal(Text(metadata, "permissionVersion"), active.Version.Value);
            Assert.True(WorldActorAvailability.CanAct(world, player) && WorldActorAvailability.CanAct(world, helper));
            Assert.Equal(WorldActionRejection.OutOfReach, WorldRestorativeActions.Evaluate(world, helper,
                CaptureHelperAid(world, helper, EntityId.Parse(Text(metadata, "helperStockLotId")))));
        }
        CarePermission prior = Assert.Single(before.Care!.Ordinary!.Permissions);
        Assert.Equal(prior.PermissionId, intent.PermissionId);
        Assert.Equal(prior.Revision, intent.ExpectedPermissionRevision);
        Assert.Equal(prior.GrantVersion, intent.ExpectedPermissionGrantVersion);

        CarePermission withdrawn = VerifyRetainedOwners(metadata, after, player, helper, active: false);
        Assert.Equal(prior.UseDefinitionId, withdrawn.UseDefinitionId);
        Assert.Equal(prior.UseDefinitionVersion, withdrawn.UseDefinitionVersion);
        Assert.Equal(prior.Revision + 1, withdrawn.Revision);
        Assert.NotEqual(prior.Version, withdrawn.Version);
        CarePermissionRevocation revocation = Assert.IsType<CarePermissionRevocation>(withdrawn.Revocation);
        Assert.Equal(EntityId.DeriveSpawned(command.CommandId, 0), revocation.RevocationId);
        Assert.Equal(command.CommandId, revocation.CommandId);
        Assert.Equal(prior.PermissionId, revocation.PermissionId);
        Assert.Equal(player, revocation.PatientActorId);
        Assert.Equal(after.Revision, revocation.WorldSequence);
        Assert.Equal(0U, revocation.CommandOrdinal);
        Assert.Equal(WorldActionRejection.NotAuthorized, WorldRestorativeActions.Evaluate(after, helper,
            CaptureHelperAid(after, helper, EntityId.Parse(Text(metadata, "helperStockLotId")))));

        VerifyUnchangedOwners(before, after);
        Assert.Equal(before.ResourceHistory!.ContentHash, after.ResourceHistory!.ContentHash);
        var patient = after.Progression!.GetActor(player);
        Assert.Equal(metadata.GetProperty("beforeHp").GetInt32(), patient.CurrentHp);
        Assert.Equal(metadata.GetProperty("expectedHp").GetInt32(), patient.CurrentHp);
        Assert.Equal(metadata.GetProperty("expectedMp").GetInt32(), patient.CurrentMp);
    }

    /// <summary>Every care record other than the permission, and all stock, material and resources.</summary>
    private static void VerifyUnchangedOwners(WorldSnapshot earlier, WorldSnapshot later)
    {
        CareSnapshot before = earlier.Care!, after = later.Care!;
        Assert.Equal(before.TotalOrdinaryUses, after.TotalOrdinaryUses);
        Assert.Equal(before.TotalAcknowledgments, after.TotalAcknowledgments);
        Assert.Equal(before.TotalTreatments, after.TotalTreatments);
        Assert.Equal(before.Acknowledgments.Count, after.Acknowledgments.Count);
        Assert.Equal(before.Treatments.Count, after.Treatments.Count);
        Assert.Equivalent(before.Agreements, after.Agreements, strict: true);
        Assert.Equivalent(before.Acknowledgments, after.Acknowledgments, strict: true);
        Assert.Equivalent(before.Treatments, after.Treatments, strict: true);
        Assert.Equal(before.Continuation?.ContentHash, after.Continuation?.ContentHash);
        Assert.Null(after.Pending);
        Assert.Equal(before.Remedies.Select(value => value.Version), after.Remedies.Select(value => value.Version));
        Assert.Equal(before.Ordinary!.Definitions.Select(value => (value.DefinitionId, value.Version)),
            after.Ordinary!.Definitions.Select(value => (value.DefinitionId, value.Version)));
        Assert.Equal(before.Ordinary.Uses.Select(value => (value.CommandId, value.ConsumptionId, value.WorldSequence)),
            after.Ordinary.Uses.Select(value => (value.CommandId, value.ConsumptionId, value.WorldSequence)));
        Assert.Equivalent(before.Ordinary.Uses, after.Ordinary.Uses, strict: true);
        Assert.Equal(earlier.Production!.ContentHash, later.Production!.ContentHash);
        Assert.Equal(earlier.Inventory.ContentHash, later.Inventory.ContentHash);
        Assert.Equal(earlier.Progression!.ContentHash, later.Progression!.ContentHash);
        Assert.Equivalent(earlier.Progression.Currency, later.Progression.Currency, strict: true);
    }

    private static void VerifyHead(JsonElement metadata, WorldSnapshot prepared, WorldSnapshot after, WorldSnapshot final,
        EntityId helper)
    {
        // Later ordinary steps may pass before the pause; they cannot move, spend, restore or re-grant.
        CarePermission permission = Assert.Single(final.Care!.Ordinary!.Permissions);
        VerifyRetainedOwners(metadata, final, permission.PatientActorId, helper, active: false);
        Assert.Equal(metadata.GetProperty("expectedTotalOrdinaryUses").GetUInt64(), final.Care.TotalOrdinaryUses);
        Assert.Equal(after.Care!.ContentHash, final.Care.ContentHash);
        VerifyUnchangedOwners(prepared, final);
        VerifyUnchangedOwners(after, final);
        Assert.Equal(prepared.Space.Actors.Select(value => (value.ActorId, value.Feet, value.Revision)),
            final.Space.Actors.Select(value => (value.ActorId, value.Feet, value.Revision)));
        Assert.Equal(prepared.Residents.Select(value => (value.ActorId, value.InventoryCustodianId, value.ControllerKind)),
            final.Residents.Select(value => (value.ActorId, value.InventoryCustodianId, value.ControllerKind)));
    }

    private static CarePermission VerifyRetainedOwners(JsonElement metadata, WorldSnapshot world, EntityId player,
        EntityId helper, bool active)
    {
        CarePermission permission = Assert.Single(world.Care!.Ordinary!.Permissions);
        Assert.Equal(active, permission.IsActive);
        Assert.Equal(Text(metadata, "permissionId"), permission.PermissionId.ToString());
        Assert.Equal(Text(metadata, "permissionGrantVersion"), permission.GrantVersion.Value);
        Assert.Equal(player, permission.PatientActorId);
        Assert.Equal(helper, permission.HelperActorId);
        var stock = world.Inventory.Custody.Lots.Single(value => value.LotId.ToString() == Text(metadata, "helperStockLotId"));
        Assert.Equal(world.GetResident(helper).InventoryCustodianId, stock.CustodianId);
        Assert.Equal(metadata.GetProperty("helperStockQuantity").GetUInt64(), stock.Quantity);
        Assert.Equal(RetainedPotions, stock.Quantity);
        return permission;
    }

    private static (EntityId World, EntityId Branch, ulong Ordinal, ulong Boundary, string Hash) Describe(
        WorldMaterialCareArchiveReference reference) => (reference.WorldId, reference.BranchId, reference.Ordinal,
            reference.BoundarySequence, reference.BoundaryHash.Value);

    private static void RefuseDamagedPriorArchive(string databasePath, byte[] genesis)
    {
        using var damaged = new StorageFiles();
        File.Copy(databasePath, damaged.Path);
        damaged.Execute("UPDATE world_material_care_segments SET state=zeroblob(length(state)) "
            + "WHERE ordinal=(SELECT MIN(ordinal) FROM world_material_care_segments)");
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(damaged.Path, genesis));
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(damaged.Path));
    }

    /// <summary>Every retained row through one commit: genesis, commits, receipts, archives, identities and checkpoints.</summary>
    internal static string LedgerDigest(WorldDatabase database, ulong through)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, database.ReadGenesis());
        foreach (StoredWorldCommit commit in database.ReadJournal().TakeWhile(value => value.Sequence <= through))
        {
            Append(hash, commit.Sequence, (ulong)commit.Kind, commit.AdvancingSteps);
            Append(hash, commit.CauseId.ToByteArray(), commit.AcceptedBatch, commit.Outcomes,
                commit.BeforeHash.ToByteArray(), commit.AfterHash.ToByteArray());
        }
        foreach (StoredWorldCommandOutcome outcome in database.ReadCommandOutcomes().TakeWhile(value => value.Sequence <= through))
        {
            Append(hash, outcome.Sequence);
            Append(hash, outcome.PrincipalId.ToByteArray(), outcome.CommandId.ToByteArray(), outcome.Command, outcome.Receipt);
        }
        foreach (StoredWorldMaterialCareArchive archive in database.ReadMaterialCareArchives()
            .TakeWhile(value => value.PublicationSequence <= through))
        {
            Append(hash, archive.Reference.Ordinal, archive.Reference.BoundarySequence, archive.PublicationSequence);
            Append(hash, archive.Reference.BoundaryHash.ToByteArray(), archive.State);
        }
        foreach (StoredWorldIdentity identity in database.ReadIdentities().Where(value => value.Sequence <= through))
        {
            Append(hash, identity.Sequence, (ulong)identity.Kind);
            Append(hash, identity.Identity.ToByteArray());
        }
        foreach (StoredWorldCheckpoint checkpoint in database.ReadCheckpoints().TakeWhile(value => value.Sequence <= through))
        {
            Append(hash, checkpoint.Sequence, checkpoint.AdvancingSteps);
            Append(hash, checkpoint.State);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void Append(IncrementalHash hash, params ulong[] numbers)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        foreach (ulong number in numbers)
        {
            BinaryPrimitives.WriteUInt64BigEndian(bytes, number);
            hash.AppendData(bytes);
        }
    }

    private static void Append(IncrementalHash hash, params byte[][] fields)
    {
        foreach (byte[] field in fields)
        {
            // Length framing keeps adjacent variable fields from aliasing one another.
            Append(hash, (ulong)field.Length);
            hash.AppendData(field);
        }
    }

    internal static string Sha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    internal static string Text(JsonElement value, string property) => value.GetProperty(property).GetString()
        ?? throw new InvalidDataException($"Coordinated native verification requires {property}.");

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
