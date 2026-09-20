using System.Buffers.Binary;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class StructuralPolicyAdmissionTests
{
    [Fact]
    public void Fresh_policy_ownership_preserves_legacy_archives_and_survives_an_ordinary_step()
    {
        var fixture = new StructuralPolicyWorldFixture();
        WorldSnapshot thirteen = fixture.Stairs.Genesis;
        WorldSnapshot twelve = new(thirteen.Inventory, thirteen.Space, thirteen.Residents, thirteen.AdmissionVersion,
            thirteen.Calendar, 0, 0, gathering: thirteen.Gathering, production: thirteen.Production,
            progression: thirteen.Progression, quests: thirteen.Quests, construction: thirteen.Construction, excavation: thirteen.Excavation);
        WorldSnapshot[] legacy = [fixture.Stairs.LegacyGenesis, twelve, thirteen];
        byte[][] retained = legacy.Select(WorldSnapshotCodec.Encode).ToArray();
        Assert.Equal(new ushort[] { 11, 12, 13 }, legacy.Select(world => world.ProfileVersion));

        ConstructionLoadPolicy policy = StructuralPolicyWorldFixture.PolicyFor(thirteen);
        WorldSnapshot admitted = WorldStructuralPolicyAdmission.CreateGenesis(thirteen, policy);
        Assert.Equal((ushort)14, admitted.ProfileVersion);
        Assert.Equal(policy.ContentHash, admitted.StructuralPolicy!.ContentHash);
        Assert.NotEqual(thirteen.AdmissionVersion, admitted.AdmissionVersion);
        Assert.Equal(thirteen.Inventory.ContentHash, admitted.Inventory.ContentHash);
        Assert.Equal(thirteen.Construction!.ContentHash, admitted.Construction!.ContentHash);
        Assert.Equal(thirteen.ConstructionPhysical!.ContentHash, admitted.ConstructionPhysical!.ContentHash);
        Assert.Equal(0UL, admitted.Revision);
        Assert.Equal(0UL, admitted.AdvancingSteps);
        Assert.Equal(fixture.Genesis.ContentHash, admitted.ContentHash);
        byte[] bytes = WorldSnapshotCodec.Encode(admitted);
        Assert.Equal(19, Fields(bytes).Length);
        Assert.Equal(ConstructionLoadPolicyCodecV1.Encode(policy), Fields(bytes)[18]);
        WorldSnapshot restored = WorldSnapshotCodec.Decode(bytes);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(restored));
        Assert.Equal(admitted.ContentHash, restored.ContentHash);
        Assert.Equal(policy.ContentHash, restored.StructuralPolicy!.ContentHash);

        WorldSnapshot advanced = new WorldStepProcessor().Decide(restored,
            fixture.Stairs.Actions.Batch(restored, [], [])).Candidate;
        Assert.Equal(restored.Revision + 1, advanced.Revision);
        Assert.Equal(ConstructionLoadPolicyCodecV1.Encode(policy), ConstructionLoadPolicyCodecV1.Encode(advanced.StructuralPolicy!));
        for (int index = 0; index < legacy.Length; index++)
        {
            Assert.Null(legacy[index].StructuralPolicy);
            Assert.Equal(retained[index], WorldSnapshotCodec.Encode(legacy[index]));
            Assert.Equal(retained[index], WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(retained[index])));
        }
        Assert.Throws<ArgumentException>(() => WorldSnapshotCodecV13.Encode(admitted));
        Assert.Throws<FormatException>(() => WorldSnapshotCodecV13.Decode(bytes));
    }

    [Fact]
    public void Admission_refuses_old_already_owned_played_and_held_worlds()
    {
        var fixture = new StructuralPolicyWorldFixture();
        WorldSnapshot basis = fixture.Stairs.Genesis;
        ConstructionLoadPolicy policy = StructuralPolicyWorldFixture.PolicyFor(basis);
        Assert.Throws<ArgumentException>(() => WorldStructuralPolicyAdmission.CreateGenesis(fixture.Stairs.LegacyGenesis, policy));
        Assert.Throws<ArgumentException>(() => WorldStructuralPolicyAdmission.CreateGenesis(fixture.Genesis, policy));
        WorldSnapshot played = new WorldStepProcessor().Decide(basis, fixture.Stairs.Actions.Batch(basis, [], [])).Candidate;
        Assert.Throws<ArgumentException>(() => WorldStructuralPolicyAdmission.CreateGenesis(played, policy));
        WorldResident speaker = basis.Residents.Single(resident => resident.OpeningUtterance is not null);
        var dialogue = new DialogueSession(Id(1), fixture.Stairs.Player, speaker.ActorId,
            speaker.OpeningUtterance!.DefinitionId, Id(2), basis.Calendar);
        LocalSpatialSnapshot heldSpace = basis.Space.WithActors(basis.Space.Actors.Select(actor => actor.ActorId == fixture.Stairs.Player
            ? new ActorPresence(actor.Binding, actor.Condition, actor.Scope,
                basis.Space.GetActor(speaker.ActorId).Feet.Offset(-256, 0, 0), actor.Revision, actor.HomePlaceId) : actor));
        Assert.Equal(SpatialInteraction.Reachable, heldSpace.QueryInteraction(fixture.Stairs.Player, speaker.ActorId));
        WorldSnapshot held = Copy(basis, dialogues: [dialogue], space: heldSpace);
        Assert.True(held.HasReadingHold);
        Assert.Equal(0UL, held.Revision);
        Assert.Throws<ArgumentException>(() => WorldStructuralPolicyAdmission.CreateGenesis(held, policy));
    }

    [Fact]
    public void Policy_scope_definition_ancestry_and_calibration_must_match_the_complete_world()
    {
        var fixture = new StructuralPolicyWorldFixture();
        WorldSnapshot basis = fixture.Stairs.Genesis;
        ConstructionLoadPolicy policy = StructuralPolicyWorldFixture.PolicyFor(basis);
        foreach (var scope in new[] { policy.Scope with { WorldId = Id(10) }, policy.Scope with { BranchId = Id(11) },
                     policy.Scope with { RealmId = Id(12) } })
        {
            Reject(new(scope, policy.Templates, policy.Foundations, policy.Masses));
        }
        ConstructionBearingTemplate template = Assert.Single(policy.Templates);
        Reject(new(policy.Scope, [new(template.DefinitionId, ContentHash.Compute("stale-construction"u8),
            template.Members, template.Joints)], policy.Foundations, policy.Masses));
        Reject(new(policy.Scope, [new(Id(13), template.DefinitionVersion, template.Members, template.Joints)],
            policy.Foundations, policy.Masses));
        StableTerrainBearing foundation = policy.Foundations[0];
        Reject(new(policy.Scope, policy.Templates, policy.Foundations.Select(value => value == foundation
            ? new(value.ElementId, value.Cell, Id(14), value.Material, value.Capacity) : value), policy.Masses));
        Reject(new(policy.Scope, policy.Templates, policy.Foundations, new(policy.Masses.Bodies.Select(body =>
            new StructuralBodyMass(body.DefinitionId, ContentHash.Compute("stale-body"u8), body.Mass)), policy.Masses.SupplementalItems)));
        Reject(new(policy.Scope, policy.Templates, policy.Foundations, new([], policy.Masses.SupplementalItems)));
        var productionMaterial = basis.Production!.Recipes.SelectMany(recipe => recipe.Bindings).First();
        Reject(new(policy.Scope, policy.Templates, policy.Foundations, new(policy.Masses.Bodies,
            policy.Masses.SupplementalItems.Where(item => item.Item.DefinitionId != productionMaterial.Item.DefinitionId)
                .Append(new(productionMaterial.Item, new(checked(productionMaterial.Physical.MassPerUnit.Value + 1)))))));

        void Reject(ConstructionLoadPolicy invalid) =>
            Assert.Throws<ArgumentException>(() => WorldStructuralPolicyAdmission.CreateGenesis(basis, invalid));
    }

    [Fact]
    public void Current_actor_load_must_be_stable_at_genesis_even_when_no_structure_has_been_built()
    {
        var fixture = new StructuralPolicyWorldFixture();
        WorldSnapshot basis = fixture.Stairs.Genesis;
        Assert.All(basis.Construction!.Projects, project => Assert.Null(project.Installed));
        ConstructionLoadPolicy tooWeak = StructuralPolicyWorldFixture.PolicyFor(basis, foundationCapacity: 1);
        Assert.Throws<ArgumentException>(() => WorldStructuralPolicyAdmission.CreateGenesis(basis, tooWeak));
        Assert.Throws<ArgumentException>(() => Copy(basis, tooWeak));
        // A different, still sufficient calibration changes both owned policy and root admission pins.
        ConstructionLoadPolicy heavier = StructuralPolicyWorldFixture.PolicyFor(basis, bodyMass: 71_000_000);
        WorldSnapshot changed = WorldStructuralPolicyAdmission.CreateGenesis(basis, heavier);
        Assert.NotEqual(fixture.Genesis.StructuralPolicy!.ContentHash, changed.StructuralPolicy!.ContentHash);
        Assert.NotEqual(fixture.Genesis.AdmissionVersion, changed.AdmissionVersion);
        Assert.NotEqual(fixture.Genesis.ContentHash, changed.ContentHash);
    }

    [Fact]
    public void Foundation_identities_cannot_alias_existing_resident_custodian_or_body_instances()
    {
        var fixture = new StructuralPolicyWorldFixture();
        WorldSnapshot basis = fixture.Stairs.Genesis;
        ConstructionLoadPolicy policy = StructuralPolicyWorldFixture.PolicyFor(basis);
        StableTerrainBearing first = policy.Foundations[0];
        EntityId[] occupied = [fixture.Stairs.Player, basis.GetResident(fixture.Stairs.Player).InventoryCustodianId,
            basis.Space.GetActor(fixture.Stairs.Player).Binding.FormInstanceId,
            basis.Space.GetActor(fixture.Stairs.Player).Binding.PhysicalBodyId!.Value];
        foreach (EntityId identity in occupied)
        {
            // The standalone policy cannot know these world-only roles; its own catalogs are valid.
            var alias = new ConstructionLoadPolicy(policy.Scope, policy.Templates,
                policy.Foundations.Select(value => value == first
                    ? new(identity, value.Cell, value.SourceId, value.Material, value.Capacity) : value), policy.Masses);
            Assert.Equal(alias.ContentHash, ConstructionLoadPolicyCodecV1.Decode(
                ConstructionLoadPolicyCodecV1.Encode(alias), alias.ContentHash, TestContext.Current.CancellationToken).ContentHash);
            Assert.Throws<ArgumentException>(() => WorldStructuralPolicyAdmission.CreateGenesis(basis, alias));
            Assert.Throws<ArgumentException>(() => Copy(basis, alias));
        }
    }

    [Fact]
    public void Unused_body_and_item_calibrations_still_reserve_exact_world_identity_roles()
    {
        var fixture = new StructuralPolicyWorldFixture();
        WorldSnapshot basis = fixture.Stairs.Genesis;
        ConstructionLoadPolicy policy = StructuralPolicyWorldFixture.PolicyFor(basis);
        ContentHash unusedPin = ContentHash.Compute("unused-exact-policy-pin"u8);
        var body = new StructuralBodyMass(Id(30), unusedPin, new(1));
        var item = new StructuralItemMass(new(Id(31), unusedPin), new(1));
        var extended = new ConstructionLoadPolicy(policy.Scope, policy.Templates, policy.Foundations,
            new(policy.Masses.Bodies.Append(body), policy.Masses.SupplementalItems.Append(item)));
        WorldSnapshot admitted = WorldStructuralPolicyAdmission.CreateGenesis(basis, extended);
        Assert.True(admitted.IdentityIndex.Contains(body.DefinitionId));
        Assert.True(admitted.IdentityIndex.Contains(item.Item.DefinitionId));
        Assert.NotEqual(policy.ContentHash, admitted.StructuralPolicy!.ContentHash);
        foreach (EntityId existing in new[] { fixture.Stairs.Player, basis.GetResident(fixture.Stairs.Player).InventoryCustodianId })
        {
            var bodyAlias = new ConstructionLoadPolicy(policy.Scope, policy.Templates, policy.Foundations,
                new(policy.Masses.Bodies.Append(new(existing, unusedPin, new(1))), policy.Masses.SupplementalItems));
            var itemAlias = new ConstructionLoadPolicy(policy.Scope, policy.Templates, policy.Foundations,
                new(policy.Masses.Bodies, policy.Masses.SupplementalItems.Append(new(new(existing, unusedPin), new(1)))));
            Assert.Throws<ArgumentException>(() => WorldStructuralPolicyAdmission.CreateGenesis(basis, bodyAlias));
            Assert.Throws<ArgumentException>(() => WorldStructuralPolicyAdmission.CreateGenesis(basis, itemAlias));
        }
    }

    [Fact]
    public void Canonical_valid_policy_bytes_cannot_bypass_root_semantics_when_rehashed_and_substituted()
    {
        var fixture = new StructuralPolicyWorldFixture();
        WorldSnapshot root = fixture.Genesis;
        ConstructionLoadPolicy policy = root.StructuralPolicy!;
        byte[] original = WorldSnapshotCodec.Encode(root);
        var reversed = new ConstructionLoadPolicy(policy.Scope, policy.Templates.Reverse(), policy.Foundations.Reverse(),
            new(policy.Masses.Bodies.Reverse(), policy.Masses.SupplementalItems.Reverse()));
        Assert.Equal(original, WorldSnapshotCodec.Encode(WorldStructuralPolicyAdmission.CreateGenesis(fixture.Stairs.Genesis, reversed)));
        StableTerrainBearing first = policy.Foundations[0];
        ConstructionLoadPolicy[] substitutions =
        [
            new(policy.Scope with { RealmId = Id(40) }, policy.Templates, policy.Foundations, policy.Masses),
            new(policy.Scope, policy.Templates, policy.Foundations.Select(value => value == first
                ? new(value.ElementId, value.Cell, Id(41), value.Material, value.Capacity) : value), policy.Masses),
            StructuralPolicyWorldFixture.PolicyFor(fixture.Stairs.Genesis, foundationCapacity: 1)
        ];
        foreach (ConstructionLoadPolicy substituted in substitutions)
        {
            byte[] policyBytes = ConstructionLoadPolicyCodecV1.Encode(substituted);
            // These are canonical, internally valid policy archives with their actual matching hash.
            Assert.Equal(substituted.ContentHash, ConstructionLoadPolicyCodecV1.Decode(
                policyBytes, ContentHash.Compute(policyBytes), TestContext.Current.CancellationToken).ContentHash);
            byte[][] fields = Fields(original);
            fields[18] = policyBytes;
            byte[] rehashedRoot = Frame(fields);
            Assert.NotEqual(root.ContentHash, ContentHash.Compute(rehashedRoot));
            Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(rehashedRoot));
        }
        Assert.Equal(original, WorldSnapshotCodec.Encode(root));
        Assert.Equal(original, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(original)));
    }

    private static WorldSnapshot Copy(WorldSnapshot basis, ConstructionLoadPolicy? policy = null,
        IEnumerable<DialogueSession>? dialogues = null, LocalSpatialSnapshot? space = null) => new(basis.Inventory, space ?? basis.Space, basis.Residents,
        basis.AdmissionVersion, basis.Calendar, basis.Revision, basis.AdvancingSteps, dialogues ?? basis.Dialogues,
        basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
        excavation: basis.Excavation, constructionPhysical: basis.ConstructionPhysical, structuralPolicy: policy);

    private static EntityId Id(ulong value) => new(0x7374727563706f6c, value);

    private static byte[][] Fields(byte[] bytes)
    {
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8)));
        byte[][] fields = new byte[count][];
        int offset = 12;
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
            offset += 4;
            fields[index] = bytes.AsSpan(offset, length).ToArray();
            offset += length;
        }
        Assert.Equal(bytes.Length, offset);
        return fields;
    }

    private static byte[] Frame(byte[][] fields)
    {
        byte[] bytes = new byte[checked(12 + fields.Sum(value => checked(4 + value.Length)))];
        "OFC\0"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), checked((uint)fields.Length));
        int offset = 12;
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset), checked((uint)field.Length));
            offset += 4;
            field.CopyTo(bytes, offset);
            offset += field.Length;
        }
        return bytes;
    }
}
