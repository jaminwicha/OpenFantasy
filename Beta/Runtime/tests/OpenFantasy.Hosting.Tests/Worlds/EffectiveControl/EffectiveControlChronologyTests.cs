using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.EffectiveControl;

public sealed class EffectiveControlChronologyTests
{
    [Fact]
    public void Acquisition_needs_its_own_world_boundary_but_revocation_can_share_the_release()
    {
        var fixture = new EffectiveControlFixture();
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        fixture.Grant(authority);
        var expired = fixture.LoseAndExpire(authority);
        Assert.Equal(4UL, expired.Revision);
        Assert.Equal(2UL, expired.ControlPermissions!.Revision);
        Assert.Equal(2UL, expired.Controllers!.Get(fixture.Owner).Revision);
        Assert.Empty(expired.EffectiveControl!.Slots);

        var acquired = fixture.Acquire(authority);
        Assert.Equal(5UL, acquired.Revision);
        var actual = acquired.EffectiveControl!.Find(fixture.Owner)!.Witness!;
        var premature = new WorldControlWitness(actual.OwnerActorId, actual.GranteeActorId,
            actual.GrantCommandId, actual.AcceptanceCommandId, actual.GrantRevision, actual.Capabilities,
            actual.TermsHash, actual.AssignmentId, actual.AssignmentRevision, expired.Revision,
            actual.DisconnectId, actual.OwnerControlRevision);
        var claimed = new WorldEffectiveControlState(1,
            [new(fixture.Owner, 1, expired.Revision, premature)]);
        // Each facet is independently canonical, and every referenced grant/episode exists at revision four.
        // Only the combined chronology exposes the missing fifth, isolated acquisition boundary.
        Assert.Equal(WorldEffectiveControlCodecV1.Encode(claimed),
            WorldEffectiveControlCodecV1.Encode(WorldEffectiveControlCodecV1.Decode(WorldEffectiveControlCodecV1.Encode(claimed))));
        var failure = Assert.Throws<ArgumentException>(() => CopyWithEffectiveControl(expired, claimed));
        Assert.Contains("more committed boundaries", failure.Message);
        Assert.Equal(WorldSnapshotCodec.Encode(acquired),
            WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(acquired))));

        var revoked = fixture.Revoke(authority);
        Assert.Equal(6UL, revoked.Revision);
        Assert.Equal(3UL, revoked.ControlPermissions!.Revision);
        Assert.Equal(2UL, revoked.Controllers!.Get(fixture.Owner).Revision);
        Assert.Equal(2UL, revoked.EffectiveControl!.Revision);
        Assert.Null(revoked.EffectiveControl.Find(fixture.Owner)!.Witness);
        Assert.Equal(WorldSnapshotCodec.Encode(revoked),
            WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(revoked))));
    }

    private static WorldSnapshot CopyWithEffectiveControl(WorldSnapshot basis, WorldEffectiveControlState effective) => new(
        basis.Inventory, basis.Space, basis.Residents, basis.AdmissionVersion, basis.Calendar,
        basis.Revision, basis.AdvancingSteps, basis.Dialogues, basis.Gathering, basis.Production,
        basis.Progression, basis.Quests, basis.Construction,
        reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
        excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
        care: basis.Care, resourceHistory: basis.ResourceHistory, coordination: basis.Coordination,
        retreat: basis.Retreat, encounterOutcome: basis.EncounterOutcome, controllers: basis.Controllers,
        recovery: basis.Recovery, controlPermissions: basis.ControlPermissions, effectiveControl: effective);
}
