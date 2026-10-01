using System.Collections.ObjectModel;
using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client.Presentation;

/// <summary>Public resource appearance without hidden harvest budgets or unissued source intervals.</summary>
public sealed record FarmGatherNodeFrame(EntityId NodeId, string DisplayName, SubunitPosition Feet,
    GatherToolKind RequiredToolKind, GatherNodeVisibility Visibility);

/// <summary>Visible completed geometry; no unfinished project is presented as installed material.</summary>
public sealed record FarmStructureFrame(EntityId ProjectId, string DisplayName, SubunitPosition Feet);

public enum FarmSpriteKind
{
    Wren = 1,
    Squire = 2,
    Wolf = 3,
    /// <summary>A retained source merchant composed from its own source appearance; requires an appearance key.</summary>
    Merchant = 4
}

/// <summary>Permitted public appearance data for one displayed actor, without private simulation state.</summary>
public sealed record FarmActorFrame
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    public FarmActorFrame(EntityId actorId, string displayName, SubunitPosition feet, bool isOwnActor, FarmSpriteKind spriteKind,
        bool multilevel = false)
        : this(actorId, displayName, feet, isOwnActor, spriteKind, multilevel, null)
    {
    }

    /// <summary>A cargo-world actor, admitted only on the frame's observed floor and regions.</summary>
    internal FarmActorFrame(EntityId actorId, string displayName, SubunitPosition feet, bool isOwnActor, FarmSpriteKind spriteKind,
        Cargo.CargoViewProjection regional) : this(actorId, displayName, feet, isOwnActor, spriteKind, false, regional)
    {
    }

    private FarmActorFrame(EntityId actorId, string displayName, SubunitPosition feet, bool isOwnActor, FarmSpriteKind spriteKind,
        bool multilevel, Cargo.CargoViewProjection? regional)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (displayName.Length > 128 || _strictUtf8.GetByteCount(displayName) > 128
            || displayName.Any(char.IsControl) || !Enum.IsDefined(spriteKind))
        {
            throw new ArgumentException("The actor's public presentation fields exceed this view's profile.");
        }
        if (regional is not null)
        {
            if (!regional.Admits(feet))
                throw new ArgumentOutOfRangeException(nameof(feet), "This cargo view supports only actors on its observed floor and regions.");
        }
        else if (multilevel)
        {
            WorksiteCoordinates.Validate(feet);
        }
        else
        {
            FarmCanvasCoordinates.Validate(feet);
        }
        ActorId = actorId;
        DisplayName = displayName;
        Feet = feet;
        IsOwnActor = isOwnActor;
        SpriteKind = spriteKind;
        IsMultilevel = multilevel;
        Regional = regional;
    }

    /// <summary>The cargo frame's projection, or null on the fixed farm and worksite grids.</summary>
    internal Cargo.CargoViewProjection? Regional { get; }

    /// <summary>The retained source identity whose appearance a <see cref="FarmSpriteKind.Merchant"/> sprite composes.</summary>
    public string? AppearanceKey { get; init; }

    public EntityId ActorId { get; }
    public string DisplayName { get; }
    public SubunitPosition Feet { get; }
    public bool IsOwnActor { get; }
    public FarmSpriteKind SpriteKind { get; }
    public bool IsMultilevel { get; }
    public bool IsIncapacitated { get; init; }
    public bool IsSelected { get; init; }
    public int? CurrentHp { get; init; }
}

/// <summary>A copied public projection. Its construction does not authorize observation or expose a host snapshot.</summary>
public sealed class FarmCanvasFrame
{
    public const int MaximumActors = 64;

    public FarmCanvasFrame(WorldInstant calendar, IEnumerable<FarmActorFrame> actors,
        IEnumerable<FarmGatherNodeFrame>? gatheringNodes = null, IEnumerable<FarmStructureFrame>? structures = null,
        LocalWorksiteView? worksite = null, LocalLiquidView? liquids = null)
        : this(calendar, actors, gatheringNodes, structures, worksite, liquids, null)
    {
    }

    /// <summary>
    /// A cargo-world frame: every actor, resource and structure must lie on the observed floor and regions. The player's
    /// own work targets travel with it so their cells and cues use the same projection as the ground they change.
    /// </summary>
    internal FarmCanvasFrame(WorldInstant calendar, IEnumerable<FarmActorFrame> actors, Cargo.CargoViewProjection regional,
        IEnumerable<FarmGatherNodeFrame>? gatheringNodes = null, IEnumerable<FarmStructureFrame>? structures = null,
        IEnumerable<LocalExcavationView>? workTargets = null)
        : this(calendar, actors, gatheringNodes, structures, null, null, regional)
    {
        LocalExcavationView[] targets = workTargets?.ToArray() ?? [];
        if (targets.Length > 32 || targets.Select(target => target.SiteId).Distinct().Count() != targets.Length)
        {
            throw new ArgumentException("The visible work targets exceed their finite appearance profile.", nameof(workTargets));
        }
        RegionalWorkTargets = Array.AsReadOnly(targets);
    }

    /// <summary>The player's own work targets in a cargo frame; empty on the fixed grids, which carry the worksite view.</summary>
    internal ReadOnlyCollection<LocalExcavationView> RegionalWorkTargets { get; } = Array.AsReadOnly(Array.Empty<LocalExcavationView>());

    private FarmCanvasFrame(WorldInstant calendar, IEnumerable<FarmActorFrame> actors,
        IEnumerable<FarmGatherNodeFrame>? gatheringNodes, IEnumerable<FarmStructureFrame>? structures,
        LocalWorksiteView? worksite, LocalLiquidView? liquids, Cargo.CargoViewProjection? regional)
    {
        ArgumentNullException.ThrowIfNull(actors);
        var selected = new SortedDictionary<EntityId, FarmActorFrame>();
        foreach (FarmActorFrame actor in actors)
        {
            ArgumentNullException.ThrowIfNull(actor);
            if (actor.IsMultilevel != (worksite is not null || liquids is not null) || actor.Regional != regional)
            {
                throw new ArgumentException("Actor projection mode must match the frame's explicit geometry mode.", nameof(actors));
            }
            if (selected.Count == MaximumActors || !selected.TryAdd(actor.ActorId, actor))
            {
                throw new ArgumentException("A farm view requires unique visible actor identities within its bound.", nameof(actors));
            }
        }
        if (selected.Values.Count(actor => actor.IsOwnActor) != 1)
        {
            throw new ArgumentException("A ready farm view requires exactly one own actor.", nameof(actors));
        }
        Calendar = calendar;
        Actors = Array.AsReadOnly(selected.Values.ToArray());
        var nodes = new SortedDictionary<EntityId, FarmGatherNodeFrame>();
        void Validate(SubunitPosition feet)
        {
            if (regional is null) FarmCanvasCoordinates.Validate(feet);
            else if (!regional.Admits(feet))
                throw new ArgumentOutOfRangeException(nameof(feet), "This cargo view supports only objects on its observed floor and regions.");
        }
        foreach (var node in gatheringNodes ?? [])
        {
            ArgumentNullException.ThrowIfNull(node);
            Validate(node.Feet);
            if (nodes.Count == 32 || !nodes.TryAdd(node.NodeId, node) || selected.ContainsKey(node.NodeId)
                || string.IsNullOrWhiteSpace(node.DisplayName) || node.DisplayName.Length > 128
                || node.DisplayName.Any(char.IsControl) || !Enum.IsDefined(node.RequiredToolKind)
                || !Enum.IsDefined(node.Visibility) || node.Visibility == GatherNodeVisibility.Absent)
            {
                throw new ArgumentException("The visible resource projection violates its bounded appearance profile.");
            }
        }
        GatheringNodes = Array.AsReadOnly(nodes.Values.ToArray());
        var installed = new SortedDictionary<EntityId, FarmStructureFrame>();
        foreach (FarmStructureFrame structure in structures ?? [])
        {
            ArgumentNullException.ThrowIfNull(structure);
            Validate(structure.Feet);
            if (installed.Count == 32 || !installed.TryAdd(structure.ProjectId, structure)
                || selected.ContainsKey(structure.ProjectId) || nodes.ContainsKey(structure.ProjectId)
                || string.IsNullOrWhiteSpace(structure.DisplayName) || structure.DisplayName.Length > 128
                || structure.DisplayName.Any(char.IsControl))
            {
                throw new ArgumentException("The visible structure projection exceeds its finite appearance profile.");
            }
        }
        Structures = Array.AsReadOnly(installed.Values.ToArray());
        Worksite = worksite;
        Liquids = liquids;
        Regional = regional;
    }

    /// <summary>The cargo frame's single projection, or null on the fixed farm and worksite grids.</summary>
    internal Cargo.CargoViewProjection? Regional { get; }

    public WorldInstant Calendar { get; }
    public ReadOnlyCollection<FarmActorFrame> Actors { get; }
    public ReadOnlyCollection<FarmGatherNodeFrame> GatheringNodes { get; }
    public ReadOnlyCollection<FarmStructureFrame> Structures { get; }
    public LocalWorksiteView? Worksite { get; }
    public LocalLiquidView? Liquids { get; }
    public bool IsMultilevel => Worksite is not null || Liquids is not null;
}
