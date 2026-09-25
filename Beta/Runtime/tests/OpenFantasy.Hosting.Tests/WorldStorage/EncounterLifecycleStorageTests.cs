using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class EncounterLifecycleStorageTests
{
    [Theory]
    [InlineData(22, 5)]
    [InlineData(23, 5)]
    [InlineData(24, 6)]
    public void Fresh_pacing_profiles_reopen_in_their_exact_schema(int profile, int schema)
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Genesis(profile);
        byte[] bytes = WorldSnapshotCodec.Encode(genesis);
        using (SqliteWorldAuthority.CreateNew(files.Path, genesis)) { }
        using (WorldDatabase database = WorldDatabase.Open(files.Path, create: false))
        {
            Assert.Equal(schema, database.SchemaVersion);
            Assert.Equal(bytes, database.ReadGenesis());
            Assert.Empty(database.ReadJournal());
        }
        byte[] closed = File.ReadAllBytes(files.Path);
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(bytes, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
        }
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(RecordedWorldReader.ReadForHost(files.Path, 0).State));
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
    }

    [Theory]
    [InlineData(22, 6)]
    [InlineData(23, 6)]
    [InlineData(24, 3)]
    [InlineData(24, 4)]
    [InlineData(24, 5)]
    public void Canonical_genesis_in_a_different_exact_schema_is_refused(int profile, int schema)
    {
        using var files = new StorageFiles();
        using (var database = WorldDatabase.Open(files.Path, create: true, schemaVersion: schema))
        {
            database.Initialize(WorldSnapshotCodec.Encode(Genesis(profile)));
        }
        byte[] closed = File.ReadAllBytes(files.Path);
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path));
        Assert.Throws<InvalidDataException>(() => RecordedWorldReader.ReadForHost(files.Path, 0));
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public void Real_entry_and_staging_recover_exact_history_receipt_and_reserved_encounter_identity()
    {
        using var files = new StorageFiles();
        var fixture = new EncounterJourneyWorldFixture();
        var history = new List<WorldSnapshot> { fixture.Genesis };
        WorldActionCommand enter;
        WorldActionReceipt entryReceipt;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            var explored = authority.Commit(fixture.Batch(fixture.Genesis, WorldBatchKind.AdvancingStep)).Candidate;
            history.Add(explored);
            enter = fixture.Enter(explored, fixture.FirstScenario);
            var accepted = authority.Commit(fixture.Batch(explored, WorldBatchKind.EncounterLifecycle, [enter]));
            entryReceipt = Assert.Single(accepted.Receipts);
            Assert.Equal(CommandReceiptStatus.Applied, entryReceipt.Status);
            Assert.Equal(EncounterLifecyclePhase.Confrontation, accepted.Candidate.EncounterLifecycle!.Phase);
            Assert.Equal(explored.Calendar, accepted.Candidate.Calendar);
            Assert.Equal(explored.AdvancingSteps, accepted.Candidate.AdvancingSteps);
            Assert.Equal(explored.Inventory.ContentHash, accepted.Candidate.Inventory.ContentHash);
            Assert.Equal(explored.LiquidCells!.ContentHash, accepted.Candidate.LiquidCells!.ContentHash);
            history.Add(accepted.Candidate);
        }
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(history[^1]), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            Assert.Equal(WorldReceiptCodecV1.Encode(entryReceipt), WorldReceiptCodecV1.Encode(
                Assert.IsType<WorldActionReceipt>(authority.FindReceipt(enter, fixture.Delivery))));
            foreach (var phase in new[] { EncounterLifecyclePhase.Staging, EncounterLifecyclePhase.Active })
            {
                var before = authority.ReadStateForHost();
                var staged = authority.Commit(fixture.Batch(before, WorldBatchKind.EncounterLifecycle));
                Assert.Empty(staged.Receipts);
                Assert.Equal(phase, staged.Candidate.EncounterLifecycle!.Phase);
                Assert.Equal(before.Calendar, staged.Candidate.Calendar);
                history.Add(staged.Candidate);
            }
            var active = authority.ReadStateForHost();
            var wait = fixture.Wait(active, fixture.Player);
            var queued = authority.Commit(fixture.Batch(active, WorldBatchKind.EncounterDecision, [wait]));
            Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(queued.Receipts).Status);
            history.Add(queued.Candidate);
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(fixture.Batch(
                queued.Candidate, WorldBatchKind.AdvancingStep)));
            Assert.Equal(history[^1].ContentHash, authority.ReadStateForHost().ContentHash);
        }
        using (var database = WorldDatabase.Open(files.Path, create: false))
        {
            Assert.Equal(6, database.SchemaVersion);
            Assert.Equal(new[] { WorldCommitKind.AdvancingStep, WorldCommitKind.EncounterLifecycle,
                WorldCommitKind.EncounterLifecycle, WorldCommitKind.EncounterLifecycle, WorldCommitKind.EncounterDecision },
                database.ReadJournal().Select(row => row.Kind).ToArray());
            var encounterId = Assert.IsType<EnterEncounterIntent>(enter.Intent).EncounterId;
            Assert.Contains(database.ReadIdentities(), row => row.Identity == encounterId
                && row.Kind == WorldIdentityKind.SpawnedEntity && row.Sequence == entryReceipt.CommitSequence);
        }
        byte[] closed = File.ReadAllBytes(files.Path);
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            foreach (var state in history)
            {
                Assert.Equal(WorldSnapshotCodec.Encode(state),
                    WorldSnapshotCodec.Encode(authority.ReadRecordedStateForHost(state.Revision).State));
            }
            Assert.Equal(WorldReceiptCodecV1.Encode(entryReceipt), WorldReceiptCodecV1.Encode(
                Assert.IsType<WorldActionReceipt>(authority.FindReceipt(enter, fixture.Delivery))));
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
    }

    [Theory]
    [InlineData("PRAGMA user_version=5")]
    [InlineData("UPDATE world_commits SET kind=7")]
    [InlineData("CREATE VIEW unexpected_lifecycle AS SELECT * FROM world_head")]
    public void Schema_or_lifecycle_kind_tampering_refuses_readiness_without_repair(string mutation)
    {
        using var files = new StorageFiles();
        var fixture = new EncounterJourneyWorldFixture();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            authority.Commit(fixture.Batch(fixture.Genesis, WorldBatchKind.EncounterLifecycle,
                [fixture.Enter(fixture.Genesis, fixture.FirstScenario)]));
        }
        Assert.Throws<SqliteException>(() => files.Execute("UPDATE world_commits SET kind=4"));
        files.Execute(mutation);
        byte[] corrupted = File.ReadAllBytes(files.Path);
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path));
        Assert.Equal(corrupted, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public void Prior_schema5_still_rejects_lifecycle_kind8()
    {
        using var files = new StorageFiles();
        var fixture = new EncounterCombatWorldFixture();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            authority.Commit(fixture.Batch(fixture.Genesis, WorldBatchKind.EncounterDecision,
                [fixture.Wait(fixture.Genesis, fixture.Player)]));
        }
        byte[] before = File.ReadAllBytes(files.Path);
        Assert.Throws<SqliteException>(() => files.Execute("UPDATE world_commits SET kind=8"));
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal((ushort)23, authority.ReadStateForHost().ProfileVersion);
            Assert.Equal(1UL, authority.ReadStateForHost().Revision);
        }
        Assert.Equal(before, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public void Restorable_zero_revision_debt_cannot_be_imported_as_new_database_genesis()
    {
        using var files = new StorageFiles();
        var fixture = new EncounterJourneyWorldFixture();
        var basis = fixture.Genesis;
        var original = basis.EncounterLifecycle!;
        var debt = new EncounterLifecycleSnapshot(original.WorldId, original.BranchId, original.Seed, 0, 0, 0, 0, 0,
            EncounterLifecyclePhase.Exploration, original.Abilities, original.Scenarios,
            original.Debts.Select(value => value.ActorId == fixture.Player
                ? new EncounterReadinessDebt(value.ActorId, 1) : value));
        var forged = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, basis.AdmissionVersion,
            basis.Calendar, 0, 0, gathering: basis.Gathering, production: basis.Production,
            progression: basis.Progression, quests: basis.Quests, construction: basis.Construction,
            reservoirs: basis.Reservoirs, excavation: basis.Excavation, reservoirSeals: basis.ReservoirSeals,
            liquidCells: basis.LiquidCells, pacing: basis.Pacing, encounterLifecycle: debt);
        byte[] bytes = WorldSnapshotCodec.Encode(forged);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
        Assert.Throws<ArgumentException>(() => SqliteWorldAuthority.CreateNew(files.Path, forged));
        Assert.False(File.Exists(files.Path));
        // A raw canonical row bypasses only creation; normal recovery must apply the same genesis guard.
        using (var database = WorldDatabase.Open(files.Path, create: true, schemaVersion: 6))
        {
            database.Initialize(bytes);
        }
        byte[] closed = File.ReadAllBytes(files.Path);
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path));
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
    }

    [Theory]
    [InlineData("player", 429_496_730, false)]
    [InlineData("wren", 429_496_730, false)]
    [InlineData("other-wolf", 429_496_730, false)]
    [InlineData("wren", 429_496_729, true)]
    public void Exploration_admission_checks_every_residents_normalized_speed_before_an_encounter(
        string actorRole, int speed, bool accepted)
    {
        using var files = new StorageFiles();
        var fixture = new EncounterJourneyWorldFixture();
        var basis = fixture.Genesis;
        EntityId actorId = actorRole switch
        {
            "player" => fixture.Player,
            "wren" => fixture.Wren,
            "other-wolf" => fixture.WolfSecond,
            _ => throw new ArgumentOutOfRangeException(nameof(actorRole))
        };
        // An explicitly altered definition is an admission counterexample, not source-backed
        // journey content. The inactive profile22 basis has no tactical normalization yet.
        var original = basis.Progression!;
        var actor = original.GetActor(actorId);
        var stats = actor.Stats;
        var profile = new ActorStatProfileDefinition(fixture.NextId(), "speed_admission_control", "Speed admission control",
            ActorStatProfileKind.Unassigned,
            new ActorStatValues(stats.MaxHp, stats.MaxMp, stats.Attack, stats.Magic, stats.Defense, speed),
            ActorStatModifiers.Zero, ActorStatModifiers.Zero);
        var altered = new ActorProgressionState(actor.ActorId, actor.Revision, profile, actor.Curve,
            actor.Level, actor.Experience, actor.CurrentHp, actor.CurrentMp);
        var progression = new ProgressionSnapshot(original.WorldId, original.BranchId, original.Revision,
            original.Profiles.Append(profile), original.Curves,
            original.Actors.Select(value => value.ActorId == actorId ? altered : value), original.Currency);
        var exploration = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, basis.AdmissionVersion,
            basis.Calendar, 0, 0, gathering: basis.Gathering, production: basis.Production,
            progression: progression, quests: basis.Quests, construction: basis.Construction,
            reservoirs: basis.Reservoirs, excavation: basis.Excavation, reservoirSeals: basis.ReservoirSeals,
            liquidCells: basis.LiquidCells, pacing: basis.Pacing);
        Assert.Equal((ushort)22, exploration.ProfileVersion);
        Assert.Null(exploration.Pacing!.Encounter);
        if (!accepted)
        {
            Assert.Throws<ArgumentException>(() => WorldEncounterLifecycleAdmission.CreateFresh(exploration,
                basis.EncounterLifecycle!));
            Assert.False(File.Exists(files.Path));
            return;
        }
        var admitted = WorldEncounterLifecycleAdmission.CreateFresh(exploration, basis.EncounterLifecycle!);
        Assert.Equal(4_294_967_290U, checked((uint)admitted.Progression!.GetActor(actorId).Stats.Speed * 10));
        using (SqliteWorldAuthority.CreateNew(files.Path, admitted)) { }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(admitted), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
    }

    [Fact]
    public void Caller_selected_episode_is_rejected_and_the_authority_pin_survives_reopen()
    {
        using var files = new StorageFiles();
        var fixture = new EncounterJourneyWorldFixture();
        var genesis = fixture.Genesis;
        EntityId expected = EncounterEpisodeIdentity.Derive(genesis.EncounterLifecycle!, fixture.FirstScenario);
        EntityId chosen = fixture.NextId();
        Assert.NotEqual(expected, chosen);
        var request = fixture.Enter(genesis, fixture.FirstScenario, chosen);
        WorldActionReceipt rejected;
        WorldSnapshot refused;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            var decision = authority.Commit(fixture.Batch(genesis, WorldBatchKind.EncounterLifecycle, [request]));
            rejected = Assert.Single(decision.Receipts);
            Assert.Equal(WorldActionRejection.Unavailable, rejected.Rejection);
            refused = decision.Candidate;
            Assert.Equal(EncounterLifecyclePhase.Exploration, refused.EncounterLifecycle!.Phase);
            Assert.Equal(0UL, refused.EncounterLifecycle.EncounterCount);
            Assert.Null(refused.EncounterLifecycle.Actions);
            Assert.Null(refused.Pacing!.Encounter);
            Assert.Equal(genesis.Calendar, refused.Calendar);
            Assert.Equal(genesis.Inventory.ContentHash, refused.Inventory.ContentHash);
            Assert.False(authority.IsIdentityOccupiedForHost(chosen));
            Assert.Equal(expected, EncounterEpisodeIdentity.Derive(refused.EncounterLifecycle, fixture.FirstScenario));
        }
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(refused), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            Assert.Equal(WorldReceiptCodecV1.Encode(rejected), WorldReceiptCodecV1.Encode(
                Assert.IsType<WorldActionReceipt>(authority.FindReceipt(request, fixture.Delivery))));
            var exact = fixture.Enter(authority.ReadStateForHost(), fixture.FirstScenario);
            Assert.Equal(expected, Assert.IsType<EnterEncounterIntent>(exact.Intent).EncounterId);
            var entered = authority.Commit(fixture.Batch(authority.ReadStateForHost(), WorldBatchKind.EncounterLifecycle, [exact]));
            Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(entered.Receipts).Status);
            Assert.Equal(expected, entered.Candidate.Pacing!.Encounter!.EncounterId);
            Assert.Equal(expected, entered.Candidate.EncounterLifecycle!.Actions!.Scope.EncounterId);
            Assert.True(authority.IsIdentityOccupiedForHost(expected));
            Assert.False(authority.IsIdentityOccupiedForHost(chosen));
        }
    }

    [Fact]
    public void Different_command_and_cause_ids_cannot_select_a_different_episode_or_random_stream()
    {
        using var firstFiles = new StorageFiles();
        using var secondFiles = new StorageFiles();
        var first = new EncounterJourneyWorldFixture();
        var second = new EncounterJourneyWorldFixture();
        for (int index = 0; index < 8; index++) _ = second.NextId();
        var firstCommand = first.Enter(first.Genesis, first.FirstScenario);
        var secondCommand = second.Enter(second.Genesis, second.FirstScenario);
        var firstBatch = first.Batch(first.Genesis, WorldBatchKind.EncounterLifecycle, [firstCommand]);
        var secondBatch = second.Batch(second.Genesis, WorldBatchKind.EncounterLifecycle, [secondCommand]);
        Assert.NotEqual(firstCommand.CommandId, secondCommand.CommandId);
        Assert.NotEqual(firstBatch.CauseId, secondBatch.CauseId);
        Assert.Equal(Assert.IsType<EnterEncounterIntent>(firstCommand.Intent).EncounterId,
            Assert.IsType<EnterEncounterIntent>(secondCommand.Intent).EncounterId);
        using var firstAuthority = SqliteWorldAuthority.CreateNew(firstFiles.Path, first.Genesis);
        using var secondAuthority = SqliteWorldAuthority.CreateNew(secondFiles.Path, second.Genesis);
        var left = firstAuthority.Commit(firstBatch);
        var right = secondAuthority.Commit(secondBatch);
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(left.Receipts).Status);
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(right.Receipts).Status);
        Assert.NotEqual(left.Receipts[0].ResultId, right.Receipts[0].ResultId);
        Assert.Equal(WorldSnapshotCodec.Encode(left.Candidate), WorldSnapshotCodec.Encode(right.Candidate));
        Assert.Equal(left.Candidate.EncounterLifecycle!.Actions!.CreateRandom().NextUInt64(201),
            right.Candidate.EncounterLifecycle!.Actions!.CreateRandom().NextUInt64(201));
    }

    [Theory]
    [InlineData("ordinary-command", false)]
    [InlineData("ordinary-command", true)]
    [InlineData("ordinary-cause", false)]
    [InlineData("ordinary-cause", true)]
    [InlineData("entry-command", false)]
    public void Generic_identities_cannot_poison_the_next_or_far_future_episode_family(string role, bool farFuture)
    {
        using var files = new StorageFiles();
        var fixture = new EncounterJourneyWorldFixture();
        var genesis = fixture.Genesis;
        EntityId episode = EncounterEpisodeIdentity.Derive(genesis.EncounterLifecycle!, fixture.FirstScenario);
        EntityId reserved = farFuture ? new EntityId(episode.High, episode.Low ^ 0x8000000000001234UL) : episode;
        Assert.True(genesis.IdentityIndex.Contains(reserved));
        Assert.False(genesis.IdentityIndex.ContainsEntity(reserved));
        WorldBatchKind kind = role == "entry-command" ? WorldBatchKind.EncounterLifecycle : WorldBatchKind.AdvancingStep;
        WorldActionCommand[] commands = [];
        if (role != "ordinary-cause")
        {
            var original = role == "entry-command" ? fixture.Enter(genesis, fixture.FirstScenario)
                : fixture.Command(genesis, fixture.Player, new SpeakToActorIntent(fixture.Wren,
                    genesis.Space.GetActor(fixture.Wren).Revision));
            commands = [new WorldActionCommand(reserved, original.PrincipalId, original.OriginSessionId,
                original.ActorId, original.WorldId, original.BranchId, original.ExpectedActorRevision, original.Intent)];
        }
        var poison = new WorldStepBatch(role == "ordinary-cause" ? reserved : fixture.NextId(),
            genesis.WorldId, genesis.BranchId, genesis.Revision, genesis.AdvancingSteps, kind, [], commands, genesis.ProfileVersion);
        using (SqliteWorldAuthority.CreateNew(files.Path, genesis)) { }
        byte[] closed = File.ReadAllBytes(files.Path);
        byte[] root = WorldSnapshotCodec.Encode(genesis);
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(poison));
            Assert.Equal(root, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            Assert.Equal(0UL, authority.ReadStateForHost().EncounterLifecycle!.EncounterCount);
            Assert.False(authority.ReadStateForHost().IdentityIndex.ContainsEntity(reserved));
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
        using (var database = WorldDatabase.Open(files.Path, create: false))
        {
            Assert.Empty(database.ReadJournal());
            Assert.Empty(database.ReadCommandOutcomes());
            Assert.Empty(database.ReadIdentities());
        }
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            var enter = fixture.Enter(authority.ReadStateForHost(), fixture.FirstScenario);
            var accepted = authority.Commit(fixture.Batch(authority.ReadStateForHost(), WorldBatchKind.EncounterLifecycle, [enter]));
            Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(accepted.Receipts).Status);
            Assert.Equal(1UL, accepted.Candidate.EncounterLifecycle!.EncounterCount);
            Assert.Equal(episode, accepted.Candidate.Pacing!.Encounter!.EncounterId);
            Assert.True(accepted.Candidate.IdentityIndex.ContainsEntity(episode));
            Assert.Equal(genesis.Calendar, accepted.Candidate.Calendar);
        }
        using var recorded = WorldDatabase.Open(files.Path, create: false);
        Assert.Single(recorded.ReadJournal());
        Assert.Single(recorded.ReadIdentities(), row => row.Identity == episode && row.Kind == WorldIdentityKind.SpawnedEntity);
        if (farFuture) Assert.DoesNotContain(recorded.ReadIdentities(), row => row.Identity == reserved);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unused_terrain_palette_identity_cannot_alias_a_present_or_future_episode(bool farFuture)
    {
        using var files = new StorageFiles();
        var fixture = new EncounterJourneyWorldFixture();
        var basis = fixture.Genesis;
        EntityId next = EncounterEpisodeIdentity.Derive(basis.EncounterLifecycle!, fixture.FirstScenario);
        EntityId collision = farFuture ? new EntityId(next.High, next.Low ^ 0x8000000000001234UL) : next;
        var originalRules = basis.Space.Regions[0].Rules;
        Assert.Equal(0, originalRules.Relief);
        var originalStone = originalRules.Materials.Stone;
        var unusedStone = new CountedItemDefinition(collision, originalStone.Version);
        var rules = new StratifiedTerrainRules(originalRules.BaseSurfaceZ, originalRules.Relief, originalRules.SoilDepth,
            new TerrainMaterialPalette(originalRules.Materials.Surface, originalRules.Materials.Soil, unusedStone));
        // This alternate fresh terrain changes an unused definition pin, not occupied material or
        // geometry. Regeneration is confined to fixture admission; no lived world is rewritten.
        var regions = basis.Space.Regions.Select(original =>
        {
            Assert.DoesNotContain(original.Cells, cell => cell.Stratum == TerrainStratum.Stone);
            var generated = new StratifiedTerrainGenerator().Generate(original.Scope, original.Seed, rules, original.Bounds);
            var holes = original.Cells.Where(cell => cell.IsExcavated).Select(cell => cell.Coordinate).ToArray();
            if (holes.Length != 0) generated = generated.Excavate(generated.Revision, holes);
            Assert.Equal(original.Revision, generated.Revision);
            Assert.Equal(original.Cells.Select(cell => cell.IsSolid), generated.Cells.Select(cell => cell.IsSolid));
            byte[] archive = TerrainRegionCodecV1.Encode(generated);
            Assert.Equal(archive, TerrainRegionCodecV1.Encode(TerrainRegionCodecV1.Decode(archive)));
            return generated;
        }).ToArray();
        var space = new LocalSpatialSnapshot(basis.Space.Scope, regions, basis.Space.Actors, basis.Space.Structures,
            basis.Space.GroundSupport, basis.Space.StructureSupport);
        // Keep the collision solely in the terrain identity layer: the removed rule names an
        // unused original material, and every actual liquid boundary keeps its original seal rule.
        var seals = new ReservoirSealPolicy(basis.ReservoirSeals!.Liquid,
            basis.ReservoirSeals.Rules.Where(rule => rule.Material != originalStone));
        Assert.DoesNotContain(seals.Rules, rule => rule.Material.DefinitionId == collision);
        var topology = TerrainLiquidTopology.Create(basis.Reservoirs!, regions, seals);
        var cells = basis.LiquidCells!.RebindTerrain(basis.Reservoirs!, topology);
        Assert.Equal(basis.LiquidCells.Portions, cells.Portions);
        Assert.Equal(basis.LiquidCells.DomainCells, cells.DomainCells);
        var excavation = new ExcavationSnapshot(basis.Excavation!.Sites.Select(site => new ExcavationSite(
            site.SiteId, site.Cell, regions.Single(region => region.Bounds.Contains(site.Cell)).GetCell(site.Cell).SourceId!.Value,
            site.CountedSourceId, site.OwnerActorId, site.Material, site.RequiredTool, site.RequiredWork,
            site.CompletedWork, site.Stance, site.ReachMode)));
        var oldProfile = new WorldSnapshot(basis.Inventory, space, basis.Residents, basis.AdmissionVersion,
            basis.Calendar, 0, 0, gathering: basis.Gathering, production: basis.Production,
            progression: basis.Progression, quests: basis.Quests, construction: basis.Construction,
            reservoirs: basis.Reservoirs, excavation: excavation, reservoirSeals: seals,
            liquidCells: cells, pacing: basis.Pacing);
        Assert.Equal((ushort)22, oldProfile.ProfileVersion);
        Assert.True(oldProfile.IdentityIndex.ContainsEntity(collision));
        Assert.Equal(basis.Inventory.ContentHash, oldProfile.Inventory.ContentHash);
        Assert.Equal(basis.Reservoirs!.ContentHash, oldProfile.Reservoirs!.ContentHash);
        byte[] bytes = WorldSnapshotCodec.Encode(oldProfile);
        using (SqliteWorldAuthority.CreateNew(files.Path, oldProfile)) { }
        using (var reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(bytes, WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        }
        Assert.Throws<ArgumentException>(() => WorldEncounterLifecycleAdmission.CreateFresh(oldProfile, basis.EncounterLifecycle!));
    }

    [Fact]
    public void Original_profile23_factory_preserves_the_qualified_genesis_bytes()
    {
        string path = RetainedCombatGenesis();
        Assert.Equal(43_614L, new FileInfo(path).Length);
        byte[] archived = File.ReadAllBytes(path);
        Assert.Equal("f7fe96a8000257784fa2ddc6a18a9735e8028b4524ae012947ca6a302a314c19",
            ContentHash.Compute(archived).Value);
        Assert.Equal(archived, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(archived)));
        Assert.Equal(archived, WorldSnapshotCodec.Encode(new EncounterCombatWorldFixture().Genesis));
    }

    private static string RetainedCombatGenesis()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "implementation", "p02", "encounter-actions",
                "qualification", "fixture", "genesis.bin");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("The qualified profile23 genesis fixture is missing from the checkout.");
    }

    private static WorldSnapshot Genesis(int profile) => profile switch
    {
        22 => new EncounterPacingWorldFixture().Genesis,
        23 => new EncounterCombatWorldFixture().Genesis,
        24 => new EncounterJourneyWorldFixture().Genesis,
        _ => throw new ArgumentOutOfRangeException(nameof(profile))
    };
}
