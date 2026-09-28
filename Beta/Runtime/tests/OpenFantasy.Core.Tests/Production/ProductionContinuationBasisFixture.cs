using System.Buffers.Binary;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Production.InstantCraftFixture;

namespace OpenFantasy.Core.Tests.Production;

/// <summary>Fresh finite admission followed only by actual crafting and material-consumption plans.</summary>
internal sealed record ProductionContinuationBasisFixture(CountedAuthorityState Inventory, ProductionSnapshot Production)
{
    internal static ProductionContinuationBasisFixture Fresh(bool splitSources = false, bool protectedStock = false)
    {
        var initial = Create(chained: true);
        var inventory = initial.Inventory;
        if (splitSources)
        {
            // Fresh authored claims leave [4,6) unissued and put [8,12) in the other actor's custody.
            // No earned history is inserted, and subsequent retirements require that actual owner.
            CountedSourceLot[] lots =
            [
                new(Id(70), Id(60), Berries, new(Id(71), 0, 4), 0),
                new(Id(74), Id(60), Berries, new(Id(71), 6, 8), 0),
                new(Id(75), Id(61), Berries, new(Id(71), 8, 12), 0),
                initial.Inventory.Custody.GetLot(Id(72))
            ];
            inventory = new(new(inventory.WorldId, inventory.BranchId, 0, inventory.Custody.Storages, lots),
                16, inventory.Actors, inventory.Controls, inventory.Policies,
                [new(Id(71), Berries, 12), new(Id(73), Wood, 4)]);
        }
        if (protectedStock)
        {
            // Protection and reservation are authored at genesis, never painted onto earned lots.
            var berry = inventory.Custody.GetLot(Id(70));
            var protectedBerry = new CountedSourceLot(berry.LotId, berry.CustodianId, berry.Definition, berry.Source,
                berry.Revision, LotProtection.Favorite | LotProtection.UserReserved, Id(90));
            inventory = new(new(inventory.WorldId, inventory.BranchId, 0, inventory.Custody.Storages,
                inventory.Custody.Lots.Select(value => value.LotId == berry.LotId ? protectedBerry : value)),
                inventory.AdmittedUnitCapacity, inventory.Actors, inventory.Controls, inventory.Policies,
                inventory.Sources, [Id(90)]);
        }
        return new(inventory, new(initial.Production.WorldId, initial.Production.BranchId, 0,
            initial.Production.Recipes, [], [], []));
    }

    internal ProductionContinuationBasisFixture CraftRations(ulong command = 6000)
    {
        var request = Request(Inventory, Production, recipe: Production.GetRecipe(Id(5202)));
        return From(InstantCraftFixture.Craft(Inventory, Production, command, request));
    }

    internal ProductionContinuationBasisFixture RepackRation(ulong command)
    {
        var ration = Assert.Single(Production.Lots, value => value.Definition == Rations);
        Assert.Equal(1UL, ration.Quantity);
        var request = Request(Inventory, Production,
            [new(IngredientLotKind.Transformed, ration.LotId, ration.Revision, 1)], Production.GetRecipe(Id(5204)));
        return From(InstantCraftFixture.Craft(Inventory, Production, command, request));
    }

    internal ProductionContinuationBasisFixture Consume(IngredientLotKind kind, EntityId lotId, ulong quantity,
        ulong command, EntityId? actor = null)
    {
        EntityId selectedActor = actor ?? Id(10);
        var control = Inventory.Controls.Single(value => value.ActorId == selectedActor);
        var raw = kind == IngredientLotKind.Source ? Inventory.Custody.GetLot(lotId) : null;
        var transformed = kind == IngredientLotKind.Transformed ? Production.Lots.Single(value => value.LotId == lotId) : null;
        EntityId custodian = raw?.CustodianId ?? transformed!.CustodianId;
        var request = new ConsumeItemRequest(custodian, Inventory.Custody.GetStorage(custodian).Revision,
            Production.Revision, Id(11), raw?.Definition ?? transformed!.Definition,
            new(kind, lotId, raw?.Revision ?? transformed!.Revision, quantity));
        var plan = new ItemConsumptionPlanner().Plan(Inventory, Production,
            new(control.PrincipalId, control.SessionId), selectedActor, Id(command), request);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Assert.NotNull(plan.Claim);
        plan.CandidateProduction.ValidateForAuthority(plan.CandidateInventory);
        return new(plan.CandidateInventory, plan.CandidateProduction);
    }

    internal ProductionContinuationBasis Capture() => ProductionContinuationBasis.Capture(Inventory, Production);

    internal static ProductionContinuationBasisFixture DiagnosticOutputPartition()
    {
        var initial = Create();
        var owner = initial.Inventory;
        var inventory = new CountedAuthorityState(owner.Custody, owner.AdmittedUnitCapacity, owner.Actors,
            owner.Controls, owner.Policies, owner.Sources, [Id(90)]);
        var package = new PackageId("openfantasy.continuation-tests");
        var berries = new MaterialDefinition(package, Id(9300), "berries", new(20000), new(30000), new(1));
        var rations = new MaterialDefinition(package, Id(9301), "rations", new(20000), new(25000), new(2));
        var physical = new MaterialRecipeDefinition(package, Id(9302), "portion", new(1), null,
            new MaterialCatalog([berries, rations]),
            [new(RecipeMaterialRole.Input, berries.Pin(), 4), new(RecipeMaterialRole.Output, rations.Pin(), 4)]);
        var recipe = new CraftRecipeDefinition(Id(9303), physical, [new(Berries, berries), new(Rations, rations)],
            [new(Berries, 0, new(20000))]);
        var production = new ProductionSnapshot(owner.WorldId, owner.BranchId, 0, [recipe], [], [], []);
        var crafted = From(InstantCraftFixture.Craft(inventory, production, 9400,
            Request(inventory, production, [new(IngredientLotKind.Source, Id(70), 0, 4)], recipe)));
        var output = Assert.Single(crafted.Production.Lots).Output;
        // Only the producer is executed here. The supplied split/custody/consumption partition
        // is a structural diagnostic, deliberately not a claimed replayed gameplay boundary.
        TransformedLot[] partitions =
        [
            new(Id(9100), Id(60), Rations, new(output.BatchId, 0, 0, 1), 3),
            new(Id(9101), Id(60), Rations, new(output.BatchId, 0, 2, 3), 5,
                LotProtection.Favorite | LotProtection.UserReserved, Id(90)),
            new(Id(9102), Id(61), Rations, new(output.BatchId, 0, 3, 4), 8, LotProtection.Heirloom)
        ];
        var consumption = new ConsumedItemClaim(EntityId.DeriveSpawned(Id(9500), 0), Id(9500), Id(10), Id(11), Id(60),
            new(Id(9103), 1, Rations, new ProductionOutputInterval(output.BatchId, 0, 1, 2)));
        var diagnostic = new ProductionSnapshot(owner.WorldId, owner.BranchId, 2, [recipe], crafted.Production.Batches,
            partitions, [consumption]);
        diagnostic.ValidateForAuthority(crafted.Inventory);
        return new(crafted.Inventory, diagnostic);
    }

    internal ContentHash BatchHash(EntityId batchId)
    {
        // Extract the existing canonical batch frame from its actual complete owner. The oracle
        // does not reimplement the witness's hashing or invent a replacement producer record.
        byte[] bytes = Production.SupportsConsumption ? ProductionCodecV2.Encode(Production) : ProductionCodecV1.Encode(Production);
        byte[] batch = Assert.Single(Fields(Fields(bytes)[6]).Skip(1), value => EntityId.FromBytes(Fields(value)[2]) == batchId);
        return ContentHash.Compute(batch);
    }

    private static ProductionContinuationBasisFixture From(CraftPlan plan)
    {
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        plan.CandidateProduction.ValidateForAuthority(plan.CandidateInventory);
        return new(plan.CandidateInventory, plan.CandidateProduction);
    }

    private static byte[][] Fields(byte[] bytes)
    {
        Assert.True(bytes.AsSpan(0, 4).SequenceEqual("OFC\0"u8));
        Assert.Equal(1U, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(4, 4)));
        uint count = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4));
        var fields = new List<byte[]>();
        int offset = 12;
        for (uint index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4)));
            offset += 4;
            fields.Add(bytes.AsSpan(offset, length).ToArray());
            offset += length;
        }
        Assert.Equal(bytes.Length, offset);
        return fields.ToArray();
    }
}
