using System.Collections.ObjectModel;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking.ObservationViews;

internal static class ConstructionObservationConverters
{
    internal static void AddTo(JsonSerializerOptions options)
    {
        options.Converters.Add(new ObservationValueConverter<LocalConstructionWorkView, ConstructionWorkData>(
            ConstructionWorkData.From, value => value.ToView()));
        options.Converters.Add(new ObservationValueConverter<LocalPublicStructureView, PublicStructureData>(
            value => new(value.StructureId, value.Origin, value.Members), value => new(value.StructureId, value.Origin, value.Members)));
    }
}

internal sealed record ConstructionWorkData(EntityId ActorId, LocalConstructionView Project, ulong ProjectRevision, ulong WorkPerAction,
    bool IsFunded, bool CanAct, bool CanManipulate, bool IsAtWorkStance, bool IsSiteReachable,
    LocalConstructionWorkBlocker Blocker, ReadOnlyCollection<WorkConstructionIntent> Choices)
{
    internal static ConstructionWorkData From(LocalConstructionWorkView value) => new(value.ActorId, value.Project, value.ProjectRevision, value.WorkPerAction,
        value.IsFunded, value.CanAct, value.CanManipulate, value.IsAtWorkStance, value.IsSiteReachable, value.Blocker, value.Choices);
    internal LocalConstructionWorkView ToView() => new(ActorId, Project, ProjectRevision, WorkPerAction, IsFunded, CanAct, CanManipulate,
        IsAtWorkStance, IsSiteReachable, Blocker, Choices);
}

internal sealed record PublicStructureData(EntityId StructureId, SubunitPosition Origin,
    ReadOnlyCollection<LocalPublicStructureMemberView> Members);
