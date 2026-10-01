using OpenFantasy.Core;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>
/// One explicit participant history lookup at a time for one subject kind. An identical outstanding request
/// shares its task and a different one is refused; nothing is cached or retried. The owning session admits
/// requests under its input lock and awaits the outstanding lookup before it releases storage.
/// </summary>
internal sealed class LocalInspectionSlot<TResult>(string busyMessage)
{
    private EntityId _subject;
    private Task<TResult>? _lookup;

    /// <summary>The latest admitted lookup, finished or not.</summary>
    internal Task? Latest => _lookup;

    internal Task<TResult> Admit(EntityId subject, Func<EntityId, Task<TResult>> start)
    {
        ArgumentNullException.ThrowIfNull(start);
        if (_lookup is { IsCompleted: false } outstanding)
        {
            return _subject == subject ? outstanding : throw new AuthorityDispatchException(busyMessage);
        }
        _subject = subject;
        _lookup = start(subject);
        return _lookup;
    }
}
