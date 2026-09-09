namespace OpenFantasy.Core.Scheduling;

/// <summary>
/// Fixed scheduler priorities. Priority 20 is reserved for separately ordered accepted commands,
/// which cannot enter this due-work queue.
/// </summary>
public enum ScheduledWorkPhase
{
    StateTransition = 10,
    Observation = 30,
    Planning = 40,
    FollowUp = 50
}
