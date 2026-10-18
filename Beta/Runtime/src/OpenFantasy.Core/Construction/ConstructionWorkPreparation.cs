using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Construction;

/// <summary>
/// Detached exact-source work evaluation. Its candidate still requires selected-step ordering, earned
/// time, physical/world admission, historical identity checks and atomic publication by the authority.
/// </summary>
public sealed class ConstructionWorkPreparation
{
    private readonly DetachedConstructionDecisionSource _source;
    private readonly CountedAuthorityState _inventory;
    private readonly ProductionSnapshot _production;
    private readonly ConstructionSnapshot _construction;
    private readonly bool _advancing;
    private readonly bool _held;

    private ConstructionWorkPreparation(WorldSnapshot world, WorldActionCommand command,
        ConstructionSiteEvidence site, bool isAdvancingStep, bool held)
    {
        if (world.Production is null || world.Construction is null
            || command.Intent is not WorkConstructionIntent intent
            || world.Construction.FindProject(intent.ProjectId) is not { } project)
        {
            throw new ArgumentException("Detached work requires the actual admitted production and construction owners.", nameof(world));
        }
        if (world.Production.IsContinued && world.Production.Continuation?.SupportsCompleteWorld != true)
        {
            throw new ArgumentException("Detached work cannot bypass archived material qualification.", nameof(world));
        }
        if (world.Production.Continuation is { } continuation
            && (continuation.Excavation is not null || continuation.TerrainRepresentation is not null))
        {
            throw new ArgumentException("Detached construction requires a terrain-free material context; retained terrain continuations need separate qualification.", nameof(world));
        }
        site.ValidateCurrent(world.Space);
        site.ValidateProject(project);
        ExpectedWorldHash = world.ContentHash;
        Command = command;
        Site = site;
        _source = new(world, command);
        _inventory = world.Inventory;
        _production = world.Production;
        _construction = world.Construction;
        _advancing = isAdvancingStep;
        _held = held;
    }

    public ContentHash ExpectedWorldHash { get; }
    public WorldActionCommand Command { get; }
    public ConstructionSiteEvidence Site { get; }

    public static ConstructionWorkPreparation Capture(WorldSnapshot world, WorldActionCommand command,
        ConstructionSiteEvidence site, bool isAdvancingStep, bool held)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(site);
        return new(world, command, site, isAdvancingStep, held);
    }

    /// <summary>Exact-source candidate only. Repeated evaluation neither earns nor commits another step.</summary>
    public ConstructionWorkResult Evaluate(Func<EntityId, bool>? historicallyOccupied = null) =>
        ConstructionWorkKernel.Plan(_source, _inventory, _production, _construction, Site, Command,
            _advancing, _held, historicallyOccupied, false, null);

    /// <summary>Conservative complete-root freshness check, serialized with publication by the owner.</summary>
    public void ValidateCurrent(WorldSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.ContentHash != ExpectedWorldHash)
        {
            throw new InvalidOperationException("The prepared construction source changed.");
        }
    }
}
