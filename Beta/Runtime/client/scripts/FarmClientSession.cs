using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Content.Retreat;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;
using OpenFantasy.Hosting.Simulation.Retreat;
using OpenFantasy.Hosting.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.Client;

namespace OpenFantasy.Client;

/// <summary>Authentication and content binding at startup; gameplay UI receives participant projections only.</summary>
internal sealed partial class FarmClientSession : IAsyncDisposable
{
    private const ushort CoordinatedProfileVersion = 27;
    /// <summary>The one interpretation a retained coordinated world may continue under after its explicit transition.</summary>
    internal const ushort RetainedGiftTargetProfileVersion = 30;
    private readonly TrustedCommandDelivery _delivery;
    private readonly FarmActionOffers _offers;
    private readonly LocalWorldSession? _ownedLocalWorld;
    private readonly ConditionalWeakTable<FarmActionOffer, OfferedAction> _issuedOffers = new();

    private sealed class OfferedAction
    {
        internal WorldActionCommand? Command { get; set; }
        internal Task<WorldActionReceipt>? Submission { get; set; }
    }

    /// <summary>
    /// Passive observation of this client's only world-command boundary, for disposable probes and tests. Raised once per
    /// <see cref="SubmitOffer(FarmActionOffer)"/> call that reached the world session, after the command was handed over,
    /// with the exact offer, command and receipt task. Observers cannot change, retry or reselect anything, and the session
    /// keeps no submission history beyond its existing per-offer registration; normal play attaches no observer.
    /// </summary>
    internal event Action<FarmSubmission>? Submitted;

    /// <summary>
    /// The first exception an observer threw. Observation stays passive: the command was already handed to the world,
    /// so its receipt task is still returned to the caller, and the failure stays visible here instead of rejecting it.
    /// </summary>
    internal Exception? ObservationFailure { get; private set; }

    internal FarmClientSession(LocalWorldSession world, TrustedCommandDelivery delivery,
        EntityId wrenActorId, EntityId woodDefinitionId, EntityId? berryDefinitionId)
        : this(new OwnedLocalPlayerSession(world), delivery, wrenActorId, woodDefinitionId, berryDefinitionId)
    {
        _ownedLocalWorld = world;
    }

    internal FarmClientSession(IPlayerSession world, TrustedCommandDelivery delivery,
        EntityId wrenActorId, EntityId woodDefinitionId, EntityId? berryDefinitionId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(delivery);
        World = world;
        _delivery = delivery;
        WrenActorId = wrenActorId;
        WoodDefinitionId = woodDefinitionId;
        BerryDefinitionId = berryDefinitionId;
        _offers = new FarmActionOffers(delivery, wrenActorId, woodDefinitionId, berryDefinitionId);
    }

    internal IPlayerSession World { get; }
    internal bool IsRemote { get; private init; }
    internal EntityId WrenActorId { get; }
    internal EntityId WoodDefinitionId { get; }
    internal EntityId? BerryDefinitionId { get; }

    /// <summary>
    /// Whether this world is presented regionally. Its published views then carry the bounded terrain observation from
    /// their own root; every other world keeps the fixed farm grid.
    /// </summary>
    internal bool IsRegional { get; private init; }

    /// <summary>Read-only travel advice for disposable cargo probes; it never moves or submits anything.</summary>
    internal LocalRouteAdvice? RouteAdvice { get; private init; }

    /// <summary>The cargo world's stall labels and merchant appearances; empty elsewhere.</summary>
    internal IReadOnlyList<NativeCargoStall> CargoStalls { get; private init; } = [];

    internal static async Task<FarmClientSession> OpenNetworkAsync(string invitationPath, CancellationToken token = default)
    {
        await using var input = File.OpenRead(invitationPath);
        if (input.Length is <= 0 or > PlayerInvitationCodec.MaximumBytes)
            throw new InvalidDataException("The player invitation has an invalid size.");
        byte[] bytes = new byte[checked((int)input.Length)];
        await input.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
        if (input.ReadByte() != -1) throw new InvalidDataException("The invitation changed while being read.");
        PlayerConnectionInvitation invitation = PlayerInvitationCodec.Decode(bytes);
        PlayerProtocolClient client = await PlayerWebSocketConnector.ConnectAsync(invitation, token).ConfigureAwait(false);
        return await AttachNetworkAsync(client, token).ConfigureAwait(false);
    }

    /// <summary>Accepts an already protected protocol connection; used by the normal connector and bounded native fixtures.</summary>
    internal static async Task<FarmClientSession> AttachNetworkAsync(PlayerProtocolClient client, CancellationToken token = default)
    {
        RemotePlayerSession session = await RemotePlayerSession.OpenAsync(client, token).ConfigureAwait(false);
        try
        {
            PlayerPresentationBootstrap binding = session.Presentation;
            return new FarmClientSession(session, session.Welcome.Delivery, binding.WrenActorId,
                binding.WoodDefinitionId, binding.BerryDefinitionId)
            {
                IsRemote = true,
                IsRegional = binding.IsRegional,
                CargoStalls = binding.CargoStalls
            };
        }
        catch { await session.DisposeAsync().ConfigureAwait(false); throw; }
    }

    internal Task<LocalHistoryVerification> VerifyRecordedHistoryAsync() => _ownedLocalWorld?.VerifyRecordedHistoryAsync()
        ?? throw new InvalidOperationException("Saved-world verification belongs to the host, not this remote player.");

    internal static Task<FarmClientSession> OpenAsync(string savePath, string seedText, string playerName,
        bool gatheringProfile, bool productionProfile = false, string? upgradeSourcePath = null,
        bool questProfile = false, string? gatheringUpgradeSourcePath = null,
        bool constructionProfile = false, string? productionUpgradeSourcePath = null) => Task.Run(() =>
    {
        WorldSnapshot genesis = gatheringProfile ? GatheringFarmAdmission.CreateEmbedded(seedText, playerName)
            : FarmWorldAdmission.CreateEmbedded(seedText, playerName);
        SqliteWorldAuthority authority = FarmSaveOpener.Open(savePath, genesis, productionProfile, upgradeSourcePath,
            questProfile, gatheringUpgradeSourcePath, constructionProfile, productionUpgradeSourcePath);
        return Attach(authority);
    });

    /// <summary>An explicit fresh-content entry; it never upgrades or opens a different farm profile.</summary>
    internal static Task<FarmClientSession> OpenWorksiteAsync(string savePath, string seedText, string playerName) => Task.Run(() =>
    {
        WorldSnapshot genesis = WorksiteFarmAdmission.CreateEmbedded(seedText, playerName);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(savePath))!);
        SqliteWorldAuthority authority = File.Exists(savePath)
            ? SqliteWorldAuthority.OpenExisting(savePath) : SqliteWorldAuthority.CreateNew(savePath, genesis);
        try
        {
            FarmSaveOpener.ValidateSelection(authority.InspectForHost().State, genesis);
        }
        catch
        {
            authority.Dispose();
            throw;
        }
        return Attach(authority);
    });

    /// <summary>Opens the finite authored watercourse without migrating an existing farm selection.</summary>
    internal static Task<FarmClientSession> OpenWatercourseAsync(string savePath, string seedText, string playerName) => Task.Run(() =>
    {
        WorldSnapshot genesis = WatercourseFarmAdmission.CreateEmbedded(seedText, playerName);
        return OpenSelection(savePath, genesis);
    });

    /// <summary>Opens the earned terrain breach as its own explicitly selected world.</summary>
    internal static Task<FarmClientSession> OpenBreachAsync(string savePath, string seedText, string playerName) => Task.Run(() =>
        OpenSelection(savePath, BreachFarmAdmission.CreateEmbedded(seedText, playerName)));

    /// <summary>Opens the source-backed encounter journey as a separate explicit save selection.</summary>
    internal static Task<FarmClientSession> OpenEncounterAsync(string savePath, string seedText, string playerName) => Task.Run(() =>
        OpenSelection(savePath, EncounterJourneyAdmission.CreateEmbedded(seedText, playerName)));

    /// <summary>Opens the care-enabled journey explicitly, without upgrading an earlier encounter save.</summary>
    internal static Task<FarmClientSession> OpenCareAsync(string savePath, string seedText, string playerName) => Task.Run(() =>
        OpenSelection(savePath, CareJourneyAdmission.CreateEmbedded(seedText, playerName)));

    internal static Task<FarmClientSession> OpenRestorativeAsync(string savePath, string seedText, string playerName) => Task.Run(() =>
        OpenSelection(savePath, RestorativeJourneyAdmission.CreateEmbedded(seedText, playerName)));

    /// <summary>Explicit saved-gift world selection; no older save is upgraded or relabeled.</summary>
    internal static Task<FarmClientSession> OpenGiftsAsync(string savePath, string seedText, string playerName) => Task.Run(() =>
        OpenSelection(savePath, WorldMaterialProposalAdmission.CreateFresh(
            RestorativeJourneyAdmission.CreateEmbedded(seedText, playerName)), gifts: true));

    /// <summary>
    /// Explicit fresh market-world selection with Wren's opt-in stall answers and her existing gift preference.
    /// An existing save opens only when it is this same selected world; nothing is upgraded, reset or relabeled.
    /// </summary>
    internal static Task<FarmClientSession> OpenMarketsAsync(string savePath, string seedText, string playerName) => Task.Run(() =>
        OpenSelection(savePath, NativeTradingWorld.CreateEmbedded(seedText, playerName), gifts: true, trading: true));

    /// <summary>Continues an earned market fixture from its existing save; it never creates or rewinds a world.</summary>
    internal static Task<FarmClientSession> OpenMarketFixtureAsync(string savePath, string expectedGenesisPath) => Task.Run(() =>
    {
        if (!File.Exists(savePath)) throw new FileNotFoundException("The market fixture requires its existing save.", savePath);
        byte[] expected = File.ReadAllBytes(expectedGenesisPath);
        WorldSnapshot genesis = WorldSnapshotCodec.Decode(expected);
        if (genesis.ProfileVersion != WorldMarketAdmission.ProfileVersion || genesis.Revision != 0 || genesis.AdvancingSteps != 0
            || NativeTradingWorld.FindStall(genesis) is null)
            throw new InvalidDataException("The market fixture requires its fresh native market genesis.");
        SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(savePath, expected);
        try { FarmSaveOpener.ValidateSelection(authority.InspectForHost().State, genesis); }
        catch { authority.Dispose(); throw; }
        return Attach(authority, QuestCompanion, gifts: true, trading: true);
    });

    /// <summary>
    /// Explicit fresh selection of the two-market cargo world with both merchants' opt-in stall answers. An existing
    /// save opens only when it is this same selected world (same world, branch and admission); nothing is upgraded,
    /// reset or relabeled, and no other world's save is ever read as a cargo world.
    /// </summary>
    internal static Task<FarmClientSession> OpenCargoAsync(string savePath, string seedText, string playerName) => Task.Run(() =>
    {
        WorldSnapshot genesis = NativeCargoWorld.CreateEmbedded(seedText, playerName);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(savePath))!);
        SqliteWorldAuthority authority = File.Exists(savePath)
            ? SqliteWorldAuthority.OpenExisting(savePath, WorldSnapshotCodec.Encode(genesis))
            : SqliteWorldAuthority.CreateNew(savePath, genesis);
        try { FarmSaveOpener.ValidateSelection(authority.InspectForHost().State, genesis); }
        catch { authority.Dispose(); throw; }
        return AttachCargo(authority);
    });

    /// <summary>
    /// Continues an exported cargo fixture from its existing save. The expected genesis must be a fresh cargo-world
    /// admission at commit and step zero, and complete recovery must select the save; nothing is created or rewound.
    /// </summary>
    internal static Task<FarmClientSession> OpenCargoFixtureAsync(string savePath, string expectedGenesisPath) => Task.Run(() =>
    {
        if (!File.Exists(savePath)) throw new FileNotFoundException("The cargo fixture requires its existing save.", savePath);
        byte[] expected = File.ReadAllBytes(expectedGenesisPath);
        WorldSnapshot genesis = WorldSnapshotCodec.Decode(expected);
        if (genesis.ProfileVersion != WorldMarketAdmission.ProfileVersion || genesis.Revision != 0 || genesis.AdvancingSteps != 0
            || !NativeCargoWorld.IsCargoWorld(genesis))
            throw new InvalidDataException("The cargo fixture requires its fresh cargo-world genesis.");
        SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(savePath, expected);
        try { FarmSaveOpener.ValidateSelection(authority.InspectForHost().State, genesis); }
        catch { authority.Dispose(); throw; }
        return AttachCargo(authority);
    });

    /// <summary>
    /// Explicit fresh selection of the retreat-capable cargo world (profile 34). An existing save opens only when it is
    /// this same selected world; a cargo save of the earlier interpretation is never upgraded or read as this one.
    /// </summary>
    internal static Task<FarmClientSession> OpenCargoRetreatAsync(string savePath, string seedText, string playerName) => Task.Run(() =>
    {
        WorldSnapshot genesis = CargoRetreatWorldAdmission.CreateEmbedded(seedText, playerName);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(savePath))!);
        SqliteWorldAuthority authority = File.Exists(savePath)
            ? SqliteWorldAuthority.OpenExisting(savePath, WorldSnapshotCodec.Encode(genesis))
            : SqliteWorldAuthority.CreateNew(savePath, genesis);
        try { FarmSaveOpener.ValidateSelection(authority.InspectForHost().State, genesis); }
        catch { authority.Dispose(); throw; }
        return AttachCargo(authority, retreat: true);
    });

    /// <summary>Continues an exported retreat-capable cargo fixture; the genesis must be its fresh profile-34 admission.</summary>
    internal static Task<FarmClientSession> OpenCargoRetreatFixtureAsync(string savePath, string expectedGenesisPath) => Task.Run(() =>
    {
        if (!File.Exists(savePath)) throw new FileNotFoundException("The retreat fixture requires its existing save.", savePath);
        byte[] expected = File.ReadAllBytes(expectedGenesisPath);
        WorldSnapshot genesis = WorldSnapshotCodec.Decode(expected);
        if (genesis.ProfileVersion != WorldEncounterRetreatAdmission.ProfileVersion || genesis.Revision != 0
            || genesis.AdvancingSteps != 0 || !NativeCargoWorld.IsCargoWorld(genesis))
            throw new InvalidDataException("The retreat fixture requires its fresh retreat-capable cargo genesis.");
        SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(savePath, expected);
        try { FarmSaveOpener.ValidateSelection(authority.InspectForHost().State, genesis); }
        catch { authority.Dispose(); throw; }
        return AttachCargo(authority, retreat: true);
    });

    /// <summary>
    /// Explicit fresh selection of the threatened cargo world: the retreat-capable cargo world with the retained camp's
    /// Bandit Chief. An existing save opens only when it is this same selected world; the plain cargo or retreat world's
    /// save is never upgraded or read as this one, and this save is never read as theirs.
    /// </summary>
    internal static Task<FarmClientSession> OpenCargoThreatAsync(string savePath, string seedText, string playerName) => Task.Run(() =>
    {
        WorldSnapshot genesis = RoadThreatWorldAdmission.CreateEmbedded(seedText, playerName);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(savePath))!);
        SqliteWorldAuthority authority = File.Exists(savePath)
            ? SqliteWorldAuthority.OpenExisting(savePath, WorldSnapshotCodec.Encode(genesis))
            : SqliteWorldAuthority.CreateNew(savePath, genesis);
        try { FarmSaveOpener.ValidateSelection(authority.InspectForHost().State, genesis); }
        catch { authority.Dispose(); throw; }
        return AttachCargo(authority, retreat: true);
    });

    /// <summary>Continues an exported threatened cargo fixture; the genesis must be its fresh admission with the chief.</summary>
    internal static Task<FarmClientSession> OpenCargoThreatFixtureAsync(string savePath, string expectedGenesisPath) => Task.Run(() =>
    {
        if (!File.Exists(savePath)) throw new FileNotFoundException("The road-threat fixture requires its existing save.", savePath);
        byte[] expected = File.ReadAllBytes(expectedGenesisPath);
        WorldSnapshot genesis = WorldSnapshotCodec.Decode(expected);
        if (genesis.ProfileVersion != WorldEncounterRetreatAdmission.ProfileVersion || genesis.Revision != 0
            || genesis.AdvancingSteps != 0 || RoadThreatWorldAdmission.Find(genesis, CargoCircuitSources.LoadEmbedded(),
                RoadThreatSources.LoadEmbedded()) is null)
            throw new InvalidDataException("The road-threat fixture requires its fresh threatened cargo genesis.");
        SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(savePath, expected);
        try { FarmSaveOpener.ValidateSelection(authority.InspectForHost().State, genesis); }
        catch { authority.Dispose(); throw; }
        return AttachCargo(authority, retreat: true);
    });

    /// <summary>
    /// Explicit fresh selection of the outcome-completion cargo world (profile 35): the threatened cargo world with the
    /// world-owned completion law. An existing save opens only when it is this same selected world; a threatened,
    /// retreat or plain cargo save is never upgraded or read as this one, and this save is never read as theirs.
    /// </summary>
    internal static Task<FarmClientSession> OpenCargoOutcomesAsync(string savePath, string seedText, string playerName) => Task.Run(() =>
    {
        WorldSnapshot genesis = CargoOutcomeWorldAdmission.CreateEmbedded(seedText, playerName);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(savePath))!);
        SqliteWorldAuthority authority = File.Exists(savePath)
            ? SqliteWorldAuthority.OpenExisting(savePath, WorldSnapshotCodec.Encode(genesis))
            : SqliteWorldAuthority.CreateNew(savePath, genesis);
        try { FarmSaveOpener.ValidateSelection(authority.InspectForHost().State, genesis); }
        catch { authority.Dispose(); throw; }
        return AttachCargo(authority, retreat: true, outcomes: true);
    });

    /// <summary>
    /// Continues an exported outcome-completion fixture; the genesis must be a fresh profile-35 admission of the
    /// threatened cargo world (a declared calibration keeps its own admission identity and is reported by the fixture).
    /// </summary>
    internal static Task<FarmClientSession> OpenCargoOutcomesFixtureAsync(string savePath, string expectedGenesisPath) => Task.Run(() =>
    {
        if (!File.Exists(savePath)) throw new FileNotFoundException("The outcome fixture requires its existing save.", savePath);
        byte[] expected = File.ReadAllBytes(expectedGenesisPath);
        WorldSnapshot genesis = WorldSnapshotCodec.Decode(expected);
        if (genesis.ProfileVersion != WorldEncounterOutcomeAdmission.ProfileVersion || genesis.Revision != 0
            || genesis.AdvancingSteps != 0 || CargoOutcomeWorldAdmission.Find(genesis, CargoCircuitSources.LoadEmbedded(),
                RoadThreatSources.LoadEmbedded()) is null)
            throw new InvalidDataException("The outcome fixture requires its fresh outcome-completion cargo genesis.");
        SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(savePath, expected);
        try { FarmSaveOpener.ValidateSelection(authority.InspectForHost().State, genesis); }
        catch { authority.Dispose(); throw; }
        return AttachCargo(authority, retreat: true, outcomes: true);
    });

    /// <summary>
    /// Opens only an explicitly exported disposition fixture and one of its already admitted human participants.
    /// Complete journal recovery selects the existing save; this path never changes controllers or advances admission.
    /// </summary>
    internal static Task<FarmClientSession> OpenCareDispositionFixtureAsync(string savePath, string expectedGenesisPath,
        EntityId patientActorId, EntityId helperActorId, EntityId controlledActorId) => Task.Run(() =>
    {
        if (!File.Exists(savePath)) throw new FileNotFoundException("The care disposition fixture requires its existing save.", savePath);
        byte[] expected = File.ReadAllBytes(expectedGenesisPath);
        WorldSnapshot genesis = WorldSnapshotCodec.Decode(expected);
        if (genesis.ProfileVersion != WorldCareDispositionAdmission.ProfileVersion || genesis.Revision != 0
            || genesis.AdvancingSteps != 0 || genesis.Care is not { SupportsDispositions: true, Revision: 0 }
            || patientActorId == helperActorId || controlledActorId != patientActorId && controlledActorId != helperActorId)
            throw new InvalidDataException("The fixture requires a fresh profile 38 genesis and one named care participant.");
        SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(savePath, expected);
        try
        {
            WorldSnapshot current = authority.InspectForHost().State;
            FarmSaveOpener.ValidateSelection(current, genesis);
            foreach (WorldSnapshot state in new[] { genesis, current })
            {
                if (state.ProfileVersion != WorldCareDispositionAdmission.ProfileVersion
                    || !state.Care!.Agreements.Any(agreement => agreement.PatientActorId == patientActorId
                        && agreement.HelperActorId == helperActorId))
                    throw new InvalidDataException("The named participants do not share this fixture's care agreement.");
                foreach (EntityId actor in new[] { patientActorId, helperActorId })
                {
                    if (state.GetResident(actor).ControllerKind != ResidentControllerKind.Player
                        || state.Controllers?.Get(actor).Mode != WorldControllerMode.Human)
                        throw new InvalidDataException("Both care participants must already have admitted human control.");
                }
            }
            ActorControlBinding control = current.Inventory.Controls.Single(value => value.ActorId == controlledActorId);
            var delivery = new TrustedCommandDelivery(control.PrincipalId, control.SessionId);
            EntityId wood = current.Gathering!.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Oak)
                .Definition.YieldDefinition.DefinitionId;
            EntityId berries = current.Gathering.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Berry)
                .Definition.YieldDefinition.DefinitionId;
            var world = new LocalWorldSession(authority, controlledActorId, delivery, Array.Empty<LocalMaterialGiftPolicy>(),
                LocalTradingConfiguration.None, new LocalRegionObserver(controlledActorId), LocalRetreatConfiguration.None);
            return new FarmClientSession(world, delivery, helperActorId, wood, berries) { IsRegional = true };
        }
        catch
        {
            authority.Dispose();
            throw;
        }
    });

    /// <summary>The cargo session: the quest companion, the cargo trading advice and the bounded region observer.</summary>
    private static FarmClientSession AttachCargo(SqliteWorldAuthority authority, bool retreat = false, bool outcomes = false)
    {
        try
        {
            WorldSnapshot current = authority.InspectForHost().State;
            if (!NativeCargoWorld.IsCargoWorld(current)
                || (current.ProfileVersion == WorldEncounterRetreatAdmission.ProfileVersion) != (retreat && !outcomes)
                || (current.ProfileVersion == WorldEncounterOutcomeAdmission.ProfileVersion) != outcomes)
                throw new InvalidDataException("This save is not the selected cargo world.");
            return AttachCargoSession(authority, retreat || outcomes);
        }
        catch
        {
            authority.Dispose();
            throw;
        }
    }

    internal static Task<FarmClientSession> OpenGiftFixtureAsync(string savePath, string expectedGenesisPath) => Task.Run(() =>
    {
        if (!File.Exists(savePath)) throw new FileNotFoundException("Gift inspection requires its existing save.", savePath);
        byte[] expected = File.ReadAllBytes(expectedGenesisPath);
        WorldSnapshot genesis = WorldSnapshotCodec.Decode(expected);
        if (genesis.ProfileVersion != 30 || genesis.Revision != 0 || genesis.AdvancingSteps != 0)
            throw new InvalidDataException("The gift fixture requires its fresh saved-proposal genesis.");
        SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(savePath, expected);
        try { FarmSaveOpener.ValidateSelection(authority.InspectForHost().State, genesis); }
        catch { authority.Dispose(); throw; }
        return Attach(authority, QuestCompanion, gifts: true);
    });

    private static FarmClientSession AttachCargoSession(SqliteWorldAuthority authority, bool enableRetreat)
    {
        WorldSnapshot current = authority.InspectForHost().State;
        WorldResident player = current.Residents.Single(person => person.ControllerKind == ResidentControllerKind.Player);
        WorldResident wren = QuestCompanion(current, player);
        ActorControlBinding control = current.Inventory.Controls.Single(value => value.ActorId == player.ActorId);
        var delivery = new TrustedCommandDelivery(control.PrincipalId, control.SessionId);
        EntityId wood = current.Gathering!.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Oak)
            .Definition.YieldDefinition.DefinitionId;
        EntityId? berries = current.Gathering.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Berry)
            .Definition.YieldDefinition.DefinitionId;
        // The session publishes the regional observation with each view; frames never read the authority.
        // The retreat world configures no NPC retreat: its wolves keep fighting exactly as in the cargo world.
        var world = enableRetreat
            ? new LocalWorldSession(authority, player.ActorId, delivery, Array.Empty<LocalMaterialGiftPolicy>(),
                NativeCargoWorld.Configuration(current), new LocalRegionObserver(player.ActorId), LocalRetreatConfiguration.None)
            : new LocalWorldSession(authority, player.ActorId, delivery, Array.Empty<LocalMaterialGiftPolicy>(),
                NativeCargoWorld.Configuration(current), new LocalRegionObserver(player.ActorId));
        return new FarmClientSession(world, delivery, wren.ActorId, wood, berries)
        {
            IsRegional = true,
            RouteAdvice = new LocalRouteAdvice(authority, player.ActorId),
            CargoStalls = NativeCargoWorld.Stalls(current)
        };
    }

    /// <summary>
    /// Continues a prepared coordinated fixture from its existing save. The expected fresh genesis
    /// selects that save after complete recovery; this entry never creates, upgrades or rewinds a world.
    /// </summary>
    internal static Task<FarmClientSession> OpenCoordinatedFixtureAsync(string savePath, string expectedGenesisPath) => Task.Run(() =>
    {
        if (!File.Exists(savePath))
        {
            throw new FileNotFoundException("The coordinated fixture requires its existing save.", savePath);
        }
        byte[] expected = File.ReadAllBytes(expectedGenesisPath);
        WorldSnapshot genesis = WorldSnapshotCodec.Decode(expected);
        if (genesis.ProfileVersion != CoordinatedProfileVersion || genesis.Revision != 0 || genesis.AdvancingSteps != 0)
        {
            throw new InvalidDataException("The coordinated fixture requires a fresh coordinated genesis at commit and step zero.");
        }
        SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(savePath, expected);
        try
        {
            FarmSaveOpener.ValidateSelection(authority.InspectForHost().State, genesis);
        }
        catch
        {
            authority.Dispose();
            throw;
        }
        return Attach(authority, CareCompanion);
    });

    /// <summary>
    /// Continues a lived coordinated world that an explicit, separately published transition already
    /// moved to the saved-gift interpretation. The supplied original genesis selects the existing save;
    /// complete recovery authenticates that one transition. Opening never transitions, creates or resets.
    /// </summary>
    internal static Task<FarmClientSession> OpenRetainedGiftsAsync(string savePath, string originalGenesisPath,
        ushort targetProfileVersion) => Task.Run(() =>
    {
        if (targetProfileVersion != RetainedGiftTargetProfileVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(targetProfileVersion),
                "A retained coordinated world continues only under the saved-gift interpretation.");
        }
        if (!File.Exists(savePath))
        {
            throw new FileNotFoundException("The retained gift world requires its existing transitioned save.", savePath);
        }
        byte[] expected = File.ReadAllBytes(originalGenesisPath);
        WorldSnapshot genesis = WorldSnapshotCodec.Decode(expected);
        if (genesis.ProfileVersion != CoordinatedProfileVersion || genesis.Revision != 0 || genesis.AdvancingSteps != 0)
        {
            throw new InvalidDataException("The retained gift world requires its original fresh coordinated genesis.");
        }
        SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(savePath, expected);
        try
        {
            ValidateRetainedTransition(authority.InspectForHost().State, genesis);
        }
        catch
        {
            authority.Dispose();
            throw;
        }
        return Attach(authority, CareCompanion, gifts: true);
    });

    /// <summary>
    /// Recovery has already authenticated the genesis and every commit; a coordinated genesis with a
    /// saved-gift head therefore passed exactly one replayed transition. The original world, branch and
    /// inherited content admission must still be the selected ones.
    /// </summary>
    private static void ValidateRetainedTransition(WorldSnapshot current, WorldSnapshot genesis)
    {
        if (current.WorldId != genesis.WorldId || current.BranchId != genesis.BranchId
            || current.AdmissionVersion != genesis.AdmissionVersion)
        {
            throw new InvalidDataException("This save is not the selected retained world.");
        }
        if (current.ProfileVersion != RetainedGiftTargetProfileVersion || current.Coordination?.Proposals is null)
        {
            throw new InvalidDataException("This retained world has not been explicitly transitioned to saved gifts.");
        }
    }

    private static FarmClientSession OpenSelection(string savePath, WorldSnapshot genesis, bool gifts = false, bool trading = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(savePath))!);
        SqliteWorldAuthority authority = File.Exists(savePath)
            ? SqliteWorldAuthority.OpenExisting(savePath) : SqliteWorldAuthority.CreateNew(savePath, genesis);
        try
        {
            FarmSaveOpener.ValidateSelection(authority.InspectForHost().State, genesis);
        }
        catch
        {
            authority.Dispose();
            throw;
        }
        return Attach(authority, QuestCompanion, gifts, trading);
    }

    private static FarmClientSession Attach(SqliteWorldAuthority authority) => Attach(authority, QuestCompanion);

    private static WorldResident QuestCompanion(WorldSnapshot current, WorldResident player)
    {
        EntityId? questGiver = current.Quests?.Definitions.Select(quest => (EntityId?)quest.GiverActorId).Distinct().SingleOrDefault();
        return questGiver is { } giver
            ? current.GetResident(giver)
            : current.Residents.Single(person => person.ControllerKind == ResidentControllerKind.Host);
    }

    /// <summary>
    /// The coordinated fixture has reciprocal quests, so its givers do not name one companion.
    /// The player's own assistance permission names the helper, whether or not it remains active.
    /// </summary>
    private static WorldResident CareCompanion(WorldSnapshot current, WorldResident player) => current.GetResident(
        current.Care!.Ordinary!.Permissions.Single(permission => permission.PatientActorId == player.ActorId).HelperActorId);

    /// <summary>
    /// The companion's explicit native medicine preference toward the controlled player. It is Host
    /// advice supplied again on every opening, never saved state, consent or treatment permission.
    /// </summary>
    internal static LocalMaterialGiftPolicy GiftPolicy(WorldSnapshot current, EntityId giverActorId, EntityId partnerActorId)
    {
        var self = current.Care!.Ordinary!.Definitions.Single(value =>
            value.DeliveryMode == OpenFantasy.Core.Care.RestorativeDeliveryMode.SelfUse);
        return new(giverActorId, partnerActorId, self.DefinitionId, self.Version,
            offerLifetimeSteps: LocalMaterialProposalPreparation.OfferLifetimeSteps);
    }

    private static FarmClientSession Attach(SqliteWorldAuthority authority,
        Func<WorldSnapshot, WorldResident, WorldResident> selectCompanion, bool gifts = false, bool trading = false)
    {
        try
        {
            WorldSnapshot current = authority.InspectForHost().State;
            WorldResident player = current.Residents.Single(person => person.ControllerKind == ResidentControllerKind.Player);
            WorldResident wren = selectCompanion(current, player);
            ActorControlBinding control = current.Inventory.Controls.Single(value => value.ActorId == player.ActorId);
            var delivery = new TrustedCommandDelivery(control.PrincipalId, control.SessionId);
            EntityId wood = current.Gathering?.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Oak)
                .Definition.YieldDefinition.DefinitionId ?? current.Inventory.Sources.Single().Definition.DefinitionId;
            EntityId? berries = current.Gathering?.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Berry)
                .Definition.YieldDefinition.DefinitionId;
            LocalMaterialGiftPolicy[] policies = gifts ? [GiftPolicy(current, wren.ActorId, player.ActorId)] : [];
            // Opt-in stall answers are Host advice resupplied on every opening, like the gift preference.
            var world = trading
                ? new LocalWorldSession(authority, player.ActorId, delivery, policies, NativeTradingPolicy.Configuration(current))
                : new LocalWorldSession(authority, player.ActorId, delivery, policies);
            return new FarmClientSession(world, delivery, wren.ActorId, wood, berries);
        }
        catch
        {
            authority.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Queues walking stop and captures the latest published view. Remote transport adds no synchronization barrier;
    /// the captured offers retain their actual revisions and may be rejected normally if the world has moved on.
    /// </summary>
    internal LocalWorldView CaptureWheelView()
    {
        if (SelectedSession is { SupportsActorSelection: true } selected)
            selected.SetSelectedMovement(selected.ReadSelectedObservation().Selection, 0, 0);
        else World.SetMovement(0, 0);
        return World.ReadView();
    }

    /// <summary>Read-only: selecting targets, lots and quest operations from a snapshot changes nothing.</summary>
    internal IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> CreateOffers(LocalWorldView view, FarmTarget? selectedTarget = null,
        FarmGiftSelection? giftSelection = null, FarmMarketDraft? marketDraft = null)
    {
        if (ReadSelection() is { } observed)
        {
            if (observed.Selection.ActorId != view.PlayerActorId || observed.HomeView.WorldId != view.WorldId
                || observed.HomeView.BranchId != view.BranchId)
                throw new InvalidOperationException("Read the selected character's scoped offers before choosing an action.");
            return CreateOffers(observed with { HomeView = view }, selectedTarget, giftSelection, marketDraft);
        }
        IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> offers = _offers.Create(view, selectedTarget, giftSelection, marketDraft);
        foreach (FarmActionOffer offer in offers.Values)
        {
            _issuedOffers.Add(offer, new OfferedAction());
        }
        return offers;
    }

    /// <summary>
    /// Sends the offer's captured intent unchanged. Blocked, foreign-scope and closed-conversation
    /// offers are refused here before any command exists; stale revisions go to the host's ordinary rejection.
    /// </summary>
    internal Task<WorldActionReceipt> SubmitOffer(FarmActionOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        if (!offer.CanExecute)
        {
            throw new InvalidOperationException(offer.BlockingReason);
        }
        if (offer.OpensPanel)
        {
            throw new InvalidOperationException("That choice opens a panel; it submits no world action.");
        }
        // Control identities persist in saves; this weak registration binds the presentation to
        // this particular client lifetime without retaining every discarded per-frame preview.
        if (!_issuedOffers.TryGetValue(offer, out OfferedAction? issued))
        {
            throw new InvalidOperationException("That action belongs to another client session.");
        }
        WorldActionCommand command;
        Task<WorldActionReceipt> submission;
        bool retry;
        lock (issued)
        {
            // Retrying the same presentation returns the same command outcome, even after its
            // first effect changed the world. A fresh choice requires a newly presented offer.
            retry = issued.Submission is not null;
            if (!retry)
            {
                LocalSelectedPlayerObservation? selected = offer.Scope.Selection is not null
                    ? SelectedSession?.ReadSelectedObservation() : null;
                LocalWorldView current = selected?.HomeView ?? World.ReadView();
                if (offer.Scope.Selection is not null ? selected is null || !offer.Scope.BelongsTo(_delivery, selected)
                    : !offer.Scope.BelongsTo(_delivery, current))
                {
                    throw new InvalidOperationException("That action belongs to another session, world or branch.");
                }
                LocalDialogueView? dialogue = selected?.ControlledView is { } controlled
                    ? controlled.Conversation?.Dialogue : current.Dialogue;
                if (offer.DialogueId is { } conversation && dialogue?.DialogueId != conversation)
                {
                    throw new InvalidOperationException("That conversation has ended; the action no longer applies.");
                }
                issued.Command = offer.ToCommand(offer.CommandId ?? NewCommandId());
                issued.Submission = offer.Scope.Selection is { } selection
                    ? SelectedSession!.SubmitSelectedAction(selection, issued.Command) : World.SubmitAction(issued.Command);
            }
            command = issued.Command!;
            submission = issued.Submission!;
        }
        if (Submitted is { } observers)
        {
            Observe(observers, new FarmSubmission(offer, command, submission, retry));
        }
        return submission;
    }

    private void Observe(Action<FarmSubmission> observers, FarmSubmission submission)
    {
        foreach (Action<FarmSubmission> observer in observers.GetInvocationList().Cast<Action<FarmSubmission>>())
        {
            try { observer(submission); }
            catch (Exception error) { ObservationFailure ??= error; }
        }
    }

    /// <summary>
    /// Asks a stall's merchant for terms. The request is nonbinding session input: it reserves and promises nothing,
    /// and the merchant answers, if at all, at a later advancing cutoff while world time runs.
    /// </summary>
    internal Task<LocalTradeInquiryView> AskAsync(LocalTradeInquiry inquiry) => World.SubmitTradeInquiryAsync(inquiry);

    /// <summary>Cancels a waiting request; an offer already issued keeps its own world window.</summary>
    internal Task<LocalTradeInquiryView?> CancelRequestAsync() => World.CancelTradeInquiryAsync();

    internal Task<WorldActionReceipt> Speak(LocalWorldView view) => SubmitOffer(view, FarmSemanticAction.Speak);
    internal Task<WorldActionReceipt> GiveWood(LocalWorldView view) => SubmitOffer(view, FarmSemanticAction.Give);
    internal Task<WorldActionReceipt> Quest(LocalWorldView view) => SubmitOffer(view, FarmSemanticAction.Give);
    internal Task<WorldActionReceipt> CloseDialogue(LocalWorldView view) => SubmitOffer(view, FarmSemanticAction.CloseDialogue);
    internal Task<WorldActionReceipt> CraftRations(LocalWorldView view) => SubmitOffer(view, FarmSemanticAction.Craft);
    internal Task<WorldActionReceipt> Gather(LocalWorldView view) => SubmitOffer(view, FarmSemanticAction.Gather);

    internal ulong WoodQuantity(LocalWorldView view) => view.PlayerInventory.Lots
        .Where(lot => lot.DefinitionId == WoodDefinitionId).Aggregate(0UL, (sum, lot) => checked(sum + lot.Quantity));

    internal ulong BerryQuantity(LocalWorldView view) => view.PlayerInventory.Lots
        .Where(lot => lot.DefinitionId == BerryDefinitionId).Aggregate(0UL, (sum, lot) => checked(sum + lot.Quantity));

    internal static LocalGatherNodeView? NearestGatherNode(LocalWorldView view)
    {
        var feet = view.Actors.Single(actor => actor.ActorId == view.PlayerActorId).Feet;
        return view.GatheringNodes.Where(node => node.RemainingSwings > 0)
            .OrderBy(node => DistanceSquared(node, feet.X, feet.Y)).ThenBy(node => node.NodeId).FirstOrDefault();
    }

    internal static bool CanReachGatherNode(LocalWorldView view, LocalGatherNodeView node)
    {
        var feet = view.Actors.Single(actor => actor.ActorId == view.PlayerActorId).Feet;
        return feet.Z == node.Feet.Z && DistanceSquared(node, feet.X, feet.Y) <= 448L * 448;
    }

    private static Int128 DistanceSquared(LocalGatherNodeView node, long x, long y)
    {
        Int128 dx = (Int128)node.Feet.X - x;
        Int128 dy = (Int128)node.Feet.Y - y;
        return dx * dx + dy * dy;
    }

    /// <summary>Convenience wrappers read the same offers the HUD would present for that view.</summary>
    private Task<WorldActionReceipt> SubmitOffer(LocalWorldView view, FarmSemanticAction action)
    {
        if (!CreateOffers(view).TryGetValue(action, out FarmActionOffer? offer))
        {
            throw new InvalidOperationException("This world does not admit that action.");
        }
        return SubmitOffer(offer);
    }

    private static EntityId NewCommandId() => EntityId.FromBytes(RandomNumberGenerator.GetBytes(EntityId.ByteLength));

    public ValueTask DisposeAsync() => World.DisposeAsync();
}
