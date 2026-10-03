using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Continues the real saved opening through the existing road checks, care, commerce and return.</summary>
internal sealed partial class RoadThreatPlayProbe
{
    private EntityId? _adventureCargoLot;
    private readonly Dictionary<EntityId, ulong> _adventureSupplies = [];
    private readonly List<FarmSubmission> _adventureSubmissions = [];
    private object? _adventureCare;
    private object? _adventureReturn;
    private bool _adventureHistoryVerified;

    internal RoadThreatPlayProbe(FarmGame game, string reportPath, string mode)
        : this(game, reportPath, string.Empty, string.Empty, mode)
    {
        if (!game.AdventureProfile) throw new ArgumentException("This continuation requires the ordinary adventure.", nameof(game));
    }

    private WorldSnapshot ReadGenesis() => _game.AdventureProfile
        ? AdventureWorldAdmission.CreateEmbedded(AdventureWorldAdmission.SeedText, [AdventureWorldAdmission.DefaultPlayerName])
        : WorldSnapshotCodec.Decode(File.ReadAllBytes(_genesisPath));

    private EntityId OpenAdventureContinuation()
    {
        WorldSnapshot genesis = ReadGenesis();
        Require(View.WorldId == genesis.WorldId && View.BranchId == genesis.BranchId,
            "The saved continuation is not the declared ordinary adventure.");
        Require(View.Construction.Any(project => project.IsCompleted)
            && View.Actors.Single(actor => actor.ActorId == _game.CompanionActorId).ControllerKind == ResidentControllerKind.Host,
            "The opening lost Wren's autonomous completed work.");
        if (_mode != "reopen")
        {
            foreach (var lot in StallOf(Marla).OwnLots) _adventureSupplies.Add(lot.LotId, lot.Quantity);
            Require(_adventureSupplies.Values.Aggregate(0UL, (sum, value) => sum + value) == 2,
                "The opening must retain its two crafted rations separately from the new cargo.");
            _game.ObserveSubmissions(submission =>
            {
                Require(_adventureSubmissions.Count < 128, "The continuation exceeded its bounded submission count.");
                _adventureSubmissions.Add(submission);
            });
        }
        return genesis.EncounterLifecycle!.Scenarios.Single(scenario =>
            genesis.GetResident(scenario.InitiatingActorId).ControllerKind == ResidentControllerKind.Host).InitiatingActorId;
    }

    private async Task ContinueFromMarla()
    {
        _stage = "retained-opening";
        Require(View.Progression!.CurrencyBalance == 200 && View.PaidQuotes.Count == 0 && EncounterCount == 0
            && StallOf(Marla).IsAvailableInPerson, "Continue the earned opening at Marla before its first purchase or encounter.");
        _reward = View.Progression.CurrencyBalance;
        await Capture("retained-opening");
        Pause();
        await _driver.Until(() => !_game.IsLocallyPaused, "The retained adventure did not resume.");
        await BuyAndTravelToChief(NativeCargoWorld.Corridor(ReadGenesis()));
    }

    private void RequireAdventureSupplies()
    {
        var remaining = StallOf(Marla).OwnLots.Where(lot => lot.LotId != _adventureCargoLot).ToArray();
        Require(remaining.Length == _adventureSupplies.Count && remaining.All(lot =>
            _adventureSupplies.TryGetValue(lot.LotId, out ulong units) && units == lot.Quantity),
            "The cargo transaction changed or replaced the originally crafted supplies.");
    }

    private async Task UseAdventureMedicine()
    {
        _stage = "personal-care";
        await _driver.Until(() => View.Restorative?.SelfUseOffers.Any(offer => offer.CanUse) == true
            && _game.CurrentOffers.GetValueOrDefault(FarmSemanticAction.UseRestorative)?.CanExecute == true,
            "The actual road injury did not permit the owned medicine.");
        LocalRestorativeUseView remedy = View.Restorative!.SelfUseOffers.Single(offer => offer.CanUse);
        int before = View.Progression!.Hp;
        ulong units = MedicineUnits(remedy.ItemDefinitionId);
        Require(remedy.ExpectedHealingHp > 0 && units > 0, "Care needs both a real injury and a finite owned dose.");
        _driver.Tap(Key.V);
        await _driver.Until(() => WheelOpen, "Care wheel did not open.");
        for (int pages = 0; Wheel.Model?.PageName != "Care"; pages++)
        {
            Require(pages < 5, "The action wheel has no Care page.");
            _driver.Tap(Key.Q);
            await _driver.Frame();
        }
        _driver.Tap(Key.Up);
        await _driver.Until(() => Wheel.Model?.Selected?.Action == FarmSemanticAction.UseRestorative
            && Wheel.Model.Selected.Offer.CanExecute, "The wheel did not capture a usable personal dose.");
        await Capture("care-inspection");
        Require(Wheel.Model!.Selected!.Offer.CanExecute && !Wheel.GetNode<Button>("%ConfirmChoice").Disabled,
            "The drawn care confirmation is no longer available.");
        int submissions = _adventureSubmissions.Count;
        _driver.Tap(Key.Enter);
        await _driver.Until(() => !_game.HasPendingAction && View.Progression!.Hp == before + remedy.ExpectedHealingHp,
            "Confirmed care did not restore its exact displayed amount.");
        Require(_adventureSubmissions.Count == submissions + 1
            && _adventureSubmissions[^1].Offer.Action == FarmSemanticAction.UseRestorative
            && MedicineUnits(remedy.ItemDefinitionId) == units - 1,
            "Care must consume one owned dose through one captured native action.");
        _adventureCare = new { BeforeHp = before, AfterHp = View.Progression!.Hp, remedy.ExpectedHealingHp,
            UnitsBefore = units, UnitsAfter = MedicineUnits(remedy.ItemDefinitionId),
            ItemDefinitionId = remedy.ItemDefinitionId.ToString() };
        RequireAdventureSupplies();
        await Capture("treated");
        _checks.Add("After the real road injury and retreat, the Care wheel consumed one owned Potion for its exact healing; crafted supplies stayed intact.");
    }

    private ulong MedicineUnits(EntityId definition) => View.PlayerInventory.Lots.Where(lot => lot.DefinitionId == definition)
        .Aggregate(0UL, (sum, lot) => checked(sum + lot.Quantity));

    private async Task ReturnToWrensWork(CargoCorridor corridor)
    {
        _stage = "return-home";
        CellCoordinate here = Cell(Feet);
        CellCoordinate north = new(346, 303, here.Z);
        CellCoordinate south = new(346, 318, here.Z);
        await Walk(CargoCorridor.Lane([here, north, new(343, north.Y, here.Z), new(343, south.Y, here.Z),
            south, corridor.Corner, corridor.OriginApproach]).Select(CargoCorridor.FeetAt).ToArray());
        Require(EncounterCount == 1 && !View.IsInEncounter, "The return detour caused another encounter.");
        SubunitPosition origin = ReadGenesis().Space.GetActor(View.PlayerActorId).Feet;
        CellCoordinate home = Cell(origin);
        await Walk(CargoCorridor.Lane([Cell(Feet), new(home.X, corridor.OriginApproach.Y, home.Z), home])
            .Select(CargoCorridor.FeetAt).ToArray());
        RequireAdventureSupplies();
        Require(View.Construction.Any(project => project.IsCompleted) && CarriedRations == 0,
            "The return lost Wren's completed project or retained sold cargo.");
        await Capture("returned-work");
        _adventureReturn = new { Feet.X, Feet.Y, Feet.Z, CompletedProjects = View.Construction.Count(project => project.IsCompleted),
            Balance = View.Progression!.CurrencyBalance, Hp = View.Progression.Hp,
            CraftedLots = _adventureSupplies.Select(pair => new { LotId = pair.Key.ToString(), Quantity = pair.Value }).ToArray() };
        _checks.Add("Held-key travel returned around the chief to the original farm, with Wren's completed work and both crafted rations retained.");
    }

    private async Task VerifyReturnedAdventure()
    {
        using var saved = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(_reportPath)!, "keyboard.json")));
        var prior = saved.RootElement;
        Require(prior.GetProperty("Status").GetString() == "passed"
            && prior.GetProperty("Adventure").GetProperty("PublicHash").GetString() == MarketProbeEvidence.PublicHash(View),
            "Separate paused reopening did not restore the exact returned public state.");
        LocalHistoryVerification history = await _game.VerifyRecordedHistory().WaitAsync(TimeSpan.FromSeconds(30));
        Require(history.ReproducesHead, "The complete adventure no longer reproduces its recorded head from the original genesis.");
        _adventureHistoryVerified = true;
        _checks.Add("Separate paused reopening restores the exact returned view and replays the original adventure through all commerce, injury, retreat and care.");
    }

    private object? AdventureEvidence() => !_game.AdventureProfile ? null : new
    {
        CargoLot = _adventureCargoLot?.ToString(), Care = _adventureCare, Returned = _adventureReturn,
        PublicHash = _game.CurrentView is { } view ? MarketProbeEvidence.PublicHash(view) : null,
        HistoryVerified = _adventureHistoryVerified,
        Submissions = _adventureSubmissions.Select(value => new
        {
            Action = value.Offer.Action.ToString(), CommandId = value.CommandId.ToString(),
            Command = Convert.ToBase64String(value.CanonicalCommandBytes()), value.IsRetry,
            Receipt = value.Receipt.IsCompletedSuccessfully ? new
            {
                Bytes = Convert.ToBase64String(WorldReceiptCodecV1.Encode(value.Receipt.Result)),
                Rejection = value.Receipt.Result.Rejection.ToString(), value.Receipt.Result.CommitSequence
            } : null
        }).ToArray()
    };

    private async Task AwaitAdventureDraw() => await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
}
