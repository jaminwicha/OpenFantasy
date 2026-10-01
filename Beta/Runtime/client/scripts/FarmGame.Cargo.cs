using Godot;
using OpenFantasy.Client.Cargo;
using OpenFantasy.Client.Presentation;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;

namespace OpenFantasy.Client;

/// <summary>
/// The cargo world's place in the game. One projection from the player's bounded terrain observation places the
/// ground, every actor and object, the target cue, the camera limits and pointer picks, so they always agree. The
/// retained merchants are drawn from their own source appearances. Authority positions and identities never depend
/// on the viewport; an unsupported observation stops presentation instead of drawing a guess.
/// </summary>
public partial class FarmGame
{
    private CargoMerchantAppearances? _merchantAppearances;
    private CargoCameraFraming? _cargoCameraFraming;

    internal Rect2 CargoFieldViewportBounds => _hud.FieldViewportBounds;

    /// <summary>Explicit fresh cargo-world selection with its own save; never an upgrade of another world.</summary>
    internal bool CargoProfile { get; init; }

    /// <summary>Expected fresh genesis of an exported cargo fixture; requires its explicit existing save.</summary>
    internal string? CargoFixtureGenesisPath { get; init; }

    /// <summary>The open cargo session's read-only travel advice, for disposable probes only.</summary>
    internal LocalRouteAdvice? CargoRouteAdvice => _session?.RouteAdvice;

    /// <summary>The open world's quest companion (Wren), for disposable probes that walk to her.</summary>
    internal EntityId? CompanionActorId => _session?.WrenActorId;

    /// <summary>Starts one explicit saved-history check in the session's background history slot; never in a frame.</summary>
    internal Task<LocalHistoryVerification> VerifyRecordedHistory() =>
        _session?.World.VerifyRecordedHistoryAsync() ?? throw new InvalidOperationException("No open world to verify.");

    /// <summary>The stalls of the open cargo world, labels and appearances only.</summary>
    internal IReadOnlyList<NativeCargoStall> CargoStalls => _session?.CargoStalls ?? [];

    /// <summary>The field canvas, so a probe can compare drawn positions with the projection.</summary>
    internal FarmCanvas Canvas => _canvas;

    /// <summary>The camera's actual framed centre and limits this frame.</summary>
    internal (Vector2 Center, Rect2 Limits) CameraFrame => (_camera.GetScreenCenterPosition(),
        new Rect2(_camera.LimitLeft, _camera.LimitTop, _camera.LimitRight - _camera.LimitLeft, _camera.LimitBottom - _camera.LimitTop));

    /// <summary>Explicit fresh retreat-capable cargo-world selection with its own save; never an upgrade of another world.</summary>
    internal bool CargoRetreatProfile { get; init; }

    /// <summary>Expected fresh genesis of an exported retreat-capable cargo fixture; requires its explicit existing save.</summary>
    internal string? CargoRetreatFixtureGenesisPath { get; init; }

    /// <summary>Starts opening the cargo world when this game selects it; false for every other selection.</summary>
    private bool OpenCargo()
    {
        if (CargoRetreatFixtureGenesisPath is { } retreatGenesis)
        {
            _opening = SavePathOverride is { } retreatSave
                ? FarmClientSession.OpenCargoRetreatFixtureAsync(retreatSave, retreatGenesis)
                : Task.FromException<FarmClientSession>(new InvalidOperationException("The retreat fixture requires its existing save."));
            return true;
        }
        if (CargoRetreatProfile)
        {
            string retreatPath = SavePathOverride ?? ProjectSettings.GlobalizePath("user://worlds/openfantasy-cargo-retreat.sqlite");
            _opening = FarmClientSession.OpenCargoRetreatAsync(retreatPath, OpenFantasy.Hosting.Content.Retreat.CargoRetreatWorldAdmission.SeedText, "Traveler");
            return true;
        }
        if (CargoFixtureGenesisPath is { } genesis)
        {
            _opening = SavePathOverride is { } fixtureSave
                ? FarmClientSession.OpenCargoFixtureAsync(fixtureSave, genesis)
                : Task.FromException<FarmClientSession>(new InvalidOperationException("The cargo fixture requires its existing save."));
            return true;
        }
        if (!CargoProfile) return false;
        string save = SavePathOverride ?? ProjectSettings.GlobalizePath("user://worlds/openfantasy-cargo.sqlite");
        _opening = FarmClientSession.OpenCargoAsync(save, NativeCargoWorld.SeedText, "Traveler");
        return true;
    }

    /// <summary>Binds the opened cargo world's merchant appearances and stall places to the canvas and the panel.</summary>
    private void AttachCargo(FarmClientSession session)
    {
        if (!session.IsRegional) return;
        _merchantAppearances = new CargoMerchantAppearances(session.CargoStalls);
        _canvas.BindMerchants(_merchantAppearances);
        _market.StallPlaces = session.CargoStalls.ToDictionary(stall => stall.MarketId, stall => stall.PlaceName);
    }

    /// <summary>Whether the current published view has no supported regional observation; the last frame stays shown.</summary>
    internal bool IsRegionalViewUnsupported { get; private set; }

    /// <summary>Published views that could not be presented regionally, and how many recovered afterwards.</summary>
    internal (int Unsupported, int Recovered) RegionalFallbacks => (_unsupportedViews, _recoveredViews);

    private int _unsupportedViews;
    private int _recoveredViews;

    /// <summary>
    /// Builds the cargo frame from the observation published with this same view: only actors, resources, structures
    /// and work targets on the observed floor inside observed regions are presented, so terrain, water, actors and
    /// offers come from one root and no frame reads the authority. An unsupported observation is recoverable: the last
    /// coherent frame stays on screen, the HUD says why, input and offers keep working, and the next supported view
    /// presents normally again. Nothing is drawn at a guessed position.
    /// </summary>
    private bool PresentCargoCanvas(FarmClientSession session, LocalWorldView view)
    {
        LocalActorView own = view.Actors.Single(actor => actor.ActorId == view.PlayerActorId);
        if (view.Regional is not { } observation || !observation.Contains(own.Feet))
        {
            if (!IsRegionalViewUnsupported)
            {
                IsRegionalViewUnsupported = true;
                _unsupportedViews++;
                _hud.Message("This spot cannot be shown on one supported floor, so the last view is kept. Your save is unchanged; "
                    + "step back onto open ground to continue.");
            }
            return true;
        }
        if (IsRegionalViewUnsupported)
        {
            IsRegionalViewUnsupported = false;
            _recoveredViews++;
            _hud.Message("The view is current again.");
        }
        var projection = new CargoViewProjection(observation);
        Dictionary<EntityId, NativeCargoStall> merchants = session.CargoStalls
            .GroupBy(stall => stall.MerchantActorId).ToDictionary(group => group.Key, group => group.First());
        IEnumerable<FarmActorFrame> actors = view.Actors.Where(actor => projection.Admits(actor.Feet)).Select(actor =>
        {
            bool merchant = merchants.TryGetValue(actor.ActorId, out NativeCargoStall? stall);
            FarmSpriteKind kind = actor.ActorId == session.WrenActorId ? FarmSpriteKind.Wren
                : merchant ? FarmSpriteKind.Merchant
                : view.EncounterLifecycle?.Entries.Any(entry => entry.TargetActorId == actor.ActorId) == true
                    ? FarmSpriteKind.Wolf : FarmSpriteKind.Squire;
            return new FarmActorFrame(actor.ActorId, actor.DisplayName, actor.Feet, actor.ActorId == view.PlayerActorId, kind,
                projection)
            {
                AppearanceKey = merchant ? stall!.MerchantSourceId : null,
                IsIncapacitated = actor.IsIncapacitated, IsSelected = _targets?.Selected?.Id == actor.ActorId,
                CurrentHp = actor.ActorId == view.PlayerActorId ? view.Progression?.Hp
                    : view.EncounterActors.FirstOrDefault(value => value.ActorId == actor.ActorId)?.CurrentHp
            };
        });
        _canvas.Present(new FarmCanvasFrame(view.Calendar, actors, projection,
            view.GatheringNodes.Where(node => projection.Admits(node.Feet)).Select(node => new FarmGatherNodeFrame(node.NodeId,
                node.DisplayName, node.Feet, node.RequiredTool, node.Visibility)),
            view.Construction.Where(project => project.IsCompleted && projection.Admits(project.SiteFeet)).Select(project =>
                new FarmStructureFrame(project.ProjectId, project.DisplayName, project.SiteFeet)),
            view.Worksite?.ExcavationTargets.Where(target => observation.RegionAt(target.Cell.X, target.Cell.Y) is not null)));
        Rect2 viewport = GetViewport().GetVisibleRect();
        Rect2 field = _hud.FieldViewportBounds;
        _cargoCameraFraming = new CargoCameraFraming(projection.Bounds,
            new CargoCanvasRect(viewport.Position.X, viewport.Position.Y, viewport.Size.X, viewport.Size.Y),
            new CargoCanvasRect(field.Position.X, field.Position.Y, field.Size.X, field.Size.Y),
            new CargoCanvasPoint(_camera.Zoom.X, _camera.Zoom.Y));
        CargoCanvasRect limits = _cargoCameraFraming.Limits;
        _camera.LimitLeft = checked((int)Math.Floor(limits.X));
        _camera.LimitTop = checked((int)Math.Floor(limits.Y));
        _camera.LimitRight = checked((int)Math.Ceiling(limits.Right));
        _camera.LimitBottom = checked((int)Math.Ceiling(limits.Bottom));
        return true;
    }

    /// <summary>
    /// A left click on the cargo field picks the stall of the merchant drawn there, through the frame's own projection.
    /// It only chooses which stall the wheel and panel address; nothing is asked, shown or spent. The router decides
    /// first: like choosing a target, a pick belongs to focused, ready, ordinary exploration, never to a failed or
    /// closing game, an opening, a conversation or reading hold, an encounter, a pause or any open modal.
    /// </summary>
    private bool PickCargoStall(InputEvent input)
    {
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } || _session is null
            || _failed || _closing is not null || !PermitsStallPick() || _canvas.Regional is not { } projection) return false;
        Vector2 local = _canvas.GetLocalMousePosition();
        LocalWorldView view = _session.World.ReadView();
        if (CargoStallPick.Pick(projection, view, new CargoCanvasPoint(local.X, local.Y)) is not { } marketId) return false;
        _marketDraft.Begin(view, marketId, null);
        _hud.Message("Chose " + _market.StallLabel(view, marketId) + ". V opens its stall actions on the Market page.");
        Present(_session.World.ReadView());
        return true;
    }

    /// <summary>The router's own exploration check for non-committing selection, applied before any pick.</summary>
    internal bool PermitsStallPick()
    {
        FarmInputSituation situation = Situation();
        return _input.Layer(situation) == FarmInputLayer.World && _input.Permits(situation, FarmSemanticAction.NextTarget);
    }
}
