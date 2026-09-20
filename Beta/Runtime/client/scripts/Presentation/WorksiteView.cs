using Godot;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client.Presentation;

/// <summary>Owns cached blockout nodes; only a new authorized publication can replace their geometry.</summary>
internal sealed class WorksiteView
{
    private readonly Node2D _layer;
    private readonly WorksiteTerrainView _terrain;
    private readonly Dictionary<MemberKey, MemberDisplay> _members = [];
    private LocalWorksiteView? _presented;

    internal WorksiteView(Node2D canvas, Node2D actorLayer)
    {
        _layer = actorLayer;
        _terrain = new WorksiteTerrainView { Name = "CommittedTerrain", ZIndex = 1 };
        canvas.AddChild(_terrain);
    }

    internal void Present(LocalWorksiteView? view, long ownFeetHeight)
    {
        if (!ReferenceEquals(view, _presented))
        {
            var included = view?.Members.Select(member => new MemberKey(member.StructureId, member.MemberIndex)).ToHashSet() ?? [];
            foreach (MemberKey key in _members.Keys.Where(key => !included.Contains(key)).ToArray())
            {
                _members[key].View.Visible = false;
                _members[key].View.QueueFree();
                _members.Remove(key);
            }
            _presented = view;
            _terrain.Visible = view is not null;
            if (view is not null)
            {
                _terrain.Setup(view);
                foreach (LocalWorksiteMemberView observed in view.Members)
                {
                    var key = new MemberKey(observed.StructureId, observed.MemberIndex);
                    if (_members.TryGetValue(key, out MemberDisplay? retained))
                    {
                        if (retained.Observation != observed)
                        {
                            retained.View.Setup(observed);
                            _members[key] = new(observed, retained.View);
                        }
                    }
                    else
                    {
                        var member = new WorksiteMemberView { Name = "Member_" + observed.StructureId + "_" + observed.MemberIndex };
                        member.Setup(observed);
                        _layer.AddChild(member);
                        _members.Add(key, new(observed, member));
                    }
                }
            }
        }
        foreach (MemberDisplay member in _members.Values)
        {
            member.View.PresentLevel(ownFeetHeight);
        }
    }

    private readonly record struct MemberKey(EntityId StructureId, uint MemberIndex);
    private sealed record MemberDisplay(LocalWorksiteMemberView Observation, WorksiteMemberView View);
}
